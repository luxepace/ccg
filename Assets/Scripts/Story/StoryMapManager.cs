using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class StoryMapManager : MonoBehaviour
{
    public static StoryMapManager Instance;

    [Header("Настройки генерации")]
    public StoryMapGenerator.GenerationSettings generationSettings;
    [Header("Сохранение")]
    public string saveFileName = "storyMapSave.json";

    [Header("Данные")]
    public List<StoryChapter> CurrentChapters;
    public StorySaveData SaveData;
    public StoryChapter CurrentChapter;
    public StoryNode CurrentNode;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        InitializeSettings();
        StoryContentLoader.LoadAllContent();
        StartCoroutine(InitMapRoutine());
    }

    private System.Collections.IEnumerator InitMapRoutine()
    {
        yield return null;
        if (HasSavedMap()) LoadMap();
        else StartNewGame();
    }

    void InitializeSettings()
    {
        if (generationSettings == null)
        {
            generationSettings = new StoryMapGenerator.GenerationSettings
            {
                nodeSpacingY = 3f,
                nodeSpacingZ = 15f,
                startMinZ = 15f,  // Уменьшенная зона старта (ближе к экрану)
                startMaxZ = 30f,  // Было 40, стало 30
                minNodeDistance = 25f,
                maxBranches = 3,
                connectionChance = 0.7f,
                minNodesPerChapter = 10,
                maxNodesPerChapter = 15
            };
        }
    }

    public void StartNewGame()
    {
        Debug.Log("[StoryMap] Новая игра.");
        DeleteSave();
        if (MapGenerator3D.Instance != null) MapGenerator3D.Instance.DeleteAllChapterMaps();

        StoryContentLoader.ClearContent();
        StoryContentLoader.LoadAllContent();

        // 1. Генерируем ландшафт всех глав
        if (MapGenerator3D.Instance != null && StoryContentLoader.AllChapters != null)
        {
            MapGenerator3D.Instance.GenerateAndSaveAllChapterMaps(StoryContentLoader.AllChapters);
        }

        // 2. Применяем ландшафт первой главы
        if (StoryContentLoader.AllChapters.Count > 0 && MapGenerator3D.Instance != null)
        {
            var ch = StoryContentLoader.AllChapters[0];
            MapGenerator3D.Instance.ApplyThemeAndLoadChapter(ch.chapterIndex, ch.themeId);
        }

        // 3. Генерируем ноды
        GenerateNewMapStructure();

        // 4. РИСУЕМ ТРОПИНКИ (и сохраняем их в PNG)
        DrawPathsAndDecorationsOnCurrentChapter();

        // 5. Сохраняем и показываем
        SaveChapters();
        SaveMap();

        if (CurrentChapters.Count > 0)
        {
            CurrentChapter = CurrentChapters[0];
            DrawCurrentChapter(CurrentChapter);
        }
    }

    void GenerateNewMapStructure()
    {
        Debug.Log("Генерация структуры узлов...");
        CurrentChapters = StoryMapGenerator.GenerateFullMap(generationSettings);
        SaveData = new StorySaveData();
    }

    public void LoadMap()
    {
        Debug.Log("=== Загрузка карты ===");
        string path = GetSavePath();

        try
        {
            if (!File.Exists(path)) { StartNewGame(); return; }

            string json = File.ReadAllText(path);
            SaveData = JsonUtility.FromJson<StorySaveData>(json);
            if (SaveData == null || SaveData.chapters.Count == 0) { StartNewGame(); return; }

            RestoreChapters();
            if (CurrentChapters.Count == 0) return;

            // Находим активную главу
            StoryChapter chapterToDraw = null;
            foreach (var ch in CurrentChapters)
            {
                if (ch.isUnlocked && !ch.isCompleted)
                {
                    if (chapterToDraw == null || ch.chapterIndex > chapterToDraw.chapterIndex)
                        chapterToDraw = ch;
                }
            }
            if (chapterToDraw == null) chapterToDraw = CurrentChapters[CurrentChapters.Count - 1];
            CurrentChapter = chapterToDraw;

            // ШАГ 1: Применяем ландшафт ЭТОЙ главы
            if (MapGenerator3D.Instance != null && !string.IsNullOrEmpty(CurrentChapter.themeId))
            {
                Debug.Log($"[Load] Применение темы ландшафта: {CurrentChapter.themeId} ");
                MapGenerator3D.Instance.ApplyThemeAndLoadChapter(CurrentChapter.chapterIndex, CurrentChapter.themeId);
            }

            // ШАГ 2: МГНОВЕННАЯ ЗАГРУЗКА ТРОПИНОК ИЗ ФАЙЛА
            if (MapDecorationManager.Instance != null)
            {
                MapDecorationManager.Instance.LoadAndApplySavedPaths(CurrentChapter.chapterIndex);
            }

            // ШАГ 3: КОРРЕКЦИЯ ПОЗИЦИЙ И ОТРИСОВКА
            SnapNodesToTerrain(CurrentChapter);
            DrawCurrentChapter(CurrentChapter);

            Debug.Log("=== Загрузка завершена ===");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Ошибка загрузки: " + e.Message);
            StartNewGame();
        }
    }

    void SnapNodesToTerrain(StoryChapter chapter)
    {
        if (MapGenerator3D.Instance == null) return;
        MapTheme theme = StoryContentLoader.GetThemeById(chapter.themeId);
        float waterLevel = (theme != null) ? theme.waterLevel : 5f;

        foreach (var node in chapter.nodes)
        {
            float groundH = MapGenerator3D.GetTerrainHeightAt(node.position.x, node.position.z);
            float targetY = (groundH < waterLevel) ? (waterLevel + 1.5f + 3.0f) : (groundH + 3.0f);
            node.position = new Vector3(node.position.x, targetY, node.position.z);
        }
    }

    void RestoreChapters()
    {
        CurrentChapters = new List<StoryChapter>();
        foreach (var chData in SaveData.chapters)
        {
            string tid = string.IsNullOrEmpty(chData.themeId) ? "forest" : chData.themeId;
            StoryChapter ch = new StoryChapter(chData.chapterIndex, $"Chapter {chData.chapterIndex + 1}")
            {
                isCompleted = chData.isCompleted,
                isUnlocked = chData.isUnlocked,
                themeId = tid
            };
            foreach (var nData in chData.nodes)
            {
                StoryNode n = new StoryNode(nData.nodeId, chData.chapterIndex, nData.layer, nData.type, nData.position)
                {
                    isVisited = nData.isVisited,
                    isUnlocked = nData.isUnlocked,
                    isSkipped = nData.isSkipped,
                    enemyId = nData.enemyId,
                    eventId = nData.eventId,
                    connectedNodeIds = nData.connectedNodeIds
                };
                ch.nodes.Add(n);
            }
            CurrentChapters.Add(ch);
        }
    }

    void DrawCurrentChapter(StoryChapter ch)
    {
        if (ch == null) return;
        StoryMapVisual visual = FindObjectOfType<StoryMapVisual>();
        if (visual == null) return;

        visual.ClearVisuals();
        visual.DisplayChapter(ch);

        // Восстановление прогресса
        StoryNode last = null;
        int maxL = -1;
        foreach (var n in ch.nodes)
        {
            if (n.isVisited && n.layer > maxL) { maxL = n.layer; last = n; }
        }
        if (last == null) last = ch.nodes.Find(n => n.type == StoryNodeType.START);

        if (last != null)
        {
            CurrentNode = last;
            UnlockNextLayerNodes(last);
            visual.RefreshAllNodeVisuals();
        }
    }

    void SaveChapters()
    {
        if (SaveData.chapters == null) SaveData.chapters = new List<ChapterSaveData>();
        SaveData.chapters.Clear();
        if (CurrentChapter != null) SaveData.currentChapter = CurrentChapter.chapterIndex;

        foreach (var ch in CurrentChapters)
        {
            ChapterSaveData cd = new ChapterSaveData
            {
                chapterIndex = ch.chapterIndex,
                isCompleted = ch.isCompleted,
                isUnlocked = ch.isUnlocked,
                nodes = new List<NodeSaveData>()
            };
            foreach (var n in ch.nodes)
            {
                cd.nodes.Add(new NodeSaveData
                {
                    nodeId = n.nodeId,
                    layer = n.layer,
                    type = n.type,
                    position = n.position,
                    enemyId = n.enemyId,
                    eventId = n.eventId,
                    connectedNodeIds = new List<int>(n.connectedNodeIds),
                    isVisited = n.isVisited,
                    isUnlocked = n.isUnlocked,
                    isSkipped = n.isSkipped
                });
            }
            SaveData.chapters.Add(cd);
        }
    }

    void SaveMap()
    {
        SaveChapters();
        string json = JsonUtility.ToJson(SaveData, true);
        string path = GetSavePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
    }

    public bool HasSavedMap() => File.Exists(GetSavePath());
    string GetSavePath() => Path.Combine(Application.persistentDataPath, saveFileName);
    public void DeleteSave() { if (File.Exists(GetSavePath())) File.Delete(GetSavePath()); }

    public StoryNode GetNodeById(int chIdx, int nId)
    {
        if (chIdx >= 0 && chIdx < CurrentChapters.Count)
            return CurrentChapters[chIdx].nodes.Find(n => n.nodeId == nId);
        return null;
    }

    public void MarkNodeVisited(int chIdx, int nId)
    {
        StoryNode node = GetNodeById(chIdx, nId);
        if (node != null)
        {
            if (CurrentNode != null && CurrentNode.nodeId != nId) CurrentNode.isVisited = true;
            node.isVisited = true;
            CurrentNode = node;

            // Пропуск других на этом слое
            StoryChapter ch = CurrentChapters[chIdx];
            foreach (var n in ch.nodes)
            {
                if (n.layer == node.layer && n.nodeId != nId)
                {
                    n.isSkipped = true;
                    n.isUnlocked = false;
                }
            }

            UnlockNextLayerNodes(node);
            SaveMap();
        }
    }

    void UnlockNextLayerNodes(StoryNode cur)
    {
        if (cur == null) return;
        StoryChapter ch = CurrentChapters[cur.chapterIndex];
        foreach (var n in ch.nodes)
        {
            if (n.layer == cur.layer + 1 && cur.connectedNodeIds.Contains(n.nodeId))
                n.isUnlocked = true;
        }
    }

    void DrawPathsAndDecorationsOnCurrentChapter()
    {
        if (MapDecorationManager.Instance == null || CurrentChapters == null || CurrentChapters.Count == 0) return;
        if (MapGenerator3D.Instance == null) return;

        StoryChapter chapter = CurrentChapters[0]; // Рисуем для первой главы

        // 1. Собираем пути
        List<PathData> paths = new List<PathData>();
        foreach (var node in chapter.nodes)
        {
            foreach (int connectedId in node.connectedNodeIds)
            {
                StoryNode targetNode = chapter.nodes.Find(n => n.nodeId == connectedId);
                if (targetNode != null)
                {
                    paths.Add(new PathData(new Vector2(node.position.x, node.position.z),
                                            new Vector2(targetNode.position.x, targetNode.position.z)));
                }
            }
        }

        // 2. Получаем текущие складки (если нужно, но новый метод берет их сам из Instance)
        // List<FoldLine> folds = MapGenerator3D.Instance.GetCurrentFolds();

        // 3. === ИСПРАВЛЕНИЕ ЗДЕСЬ ===
        // Передаем пути И индекс главы
        MapDecorationManager.Instance.DrawPathsOnTexture(paths, chapter.chapterIndex);

        Debug.Log($"[MapManager] Тропинки нарисованы для главы {chapter.chapterIndex}");
    }

    public void ProceedToNextChapter()
    {
        if (CurrentChapter == null) return;
        int nextIdx = CurrentChapter.chapterIndex + 1;

        if (nextIdx < CurrentChapters.Count)
        {
            CurrentChapter.isCompleted = true;
            CurrentChapter.isActive = false;

            StoryChapter nextCh = CurrentChapters[nextIdx];
            nextCh.isUnlocked = true;
            nextCh.isActive = true;
            CurrentChapter = nextCh;

            StoryNode start = nextCh.nodes.Find(n => n.type == StoryNodeType.START);
            if (start != null)
            {
                CurrentNode = start;
                start.isUnlocked = true;
                UnlockNextLayerNodes(start);
            }

            SaveChapters();
            SaveMap();

            StoryMapVisual vis = FindObjectOfType<StoryMapVisual>();
            if (vis != null)
            {
                vis.ClearVisuals();
                vis.InitializeAndDisplay(CurrentChapter);
            }

            if (MapGenerator3D.Instance != null)
            {
                MapGenerator3D.Instance.ApplyThemeAndLoadChapter(CurrentChapter.chapterIndex, CurrentChapter.themeId);
                // Для новой главы нужно тоже загрузить пути, если они уже сгенерированы
                // Но так как мы генерируем все главы сразу при старте игры, текстура должна быть в файле.
                MapDecorationManager.Instance.LoadAndApplySavedPaths(CurrentChapter.chapterIndex);
            }

            SnapNodesToTerrain(CurrentChapter);
        }
    }

    [ContextMenu("Regenerate Map")]
    public void RegenerateMap()
    {
        DeleteSave();
        if (MapGenerator3D.Instance != null) MapGenerator3D.Instance.DeleteAllChapterMaps();
        StartNewGame();
    }
}