using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

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

    [Header("UI Победы")]
    public GameObject victoryPanelPrefab; // Сюда перетащить префаб в Инспекторе
    private GameObject activeVictoryPanel;

    [Header("UI Загрузки")]
    public GameObject loadingPanel;
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
        else
        {
            StartNewGame();
        }
        
    }

    void InitializeSettings()
    {
        if (generationSettings == null)
        {
            generationSettings = new StoryMapGenerator.GenerationSettings
            {
                nodeSpacingY = 3f,
                nodeSpacingZ = 15f,
                startMinZ = 15f,
                startMaxZ = 30f,
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
        Debug.Log("[StoryMap] Начало новой игры. Выбор случайных вариантов глав...");

        DeleteSave();
        if (MapGenerator3D.Instance != null)
            MapGenerator3D.Instance.DeleteAllChapterMaps();

        StoryContentLoader.ClearContent();
        StoryContentLoader.LoadAllContent();

        if (StoryContentLoader.AllChapters == null || StoryContentLoader.AllChapters.Count == 0)
        {
            Debug.LogError("[StoryMap] Нет конфигурации глав!");
            return;
        }

        // === ШАГ 1: ВЫБОР СЛУЧАЙНЫХ ВАРИАНТОВ ДЛЯ КАЖДОГО УРОВНЯ ===

        var chaptersByLevel = new Dictionary<int, List<ChapterConfig>>();

        foreach (var config in StoryContentLoader.AllChapters)
        {
            if (!chaptersByLevel.ContainsKey(config.chapterIndex))
            {
                chaptersByLevel[config.chapterIndex] = new List<ChapterConfig>();
            }
            chaptersByLevel[config.chapterIndex].Add(config);
        }

        List<ChapterConfig> selectedConfigs = new List<ChapterConfig>();
        int maxLevel = chaptersByLevel.Keys.Count > 0 ? chaptersByLevel.Keys.Max() : 0;

        for (int i = 0; i <= maxLevel; i++)
        {
            if (chaptersByLevel.ContainsKey(i) && chaptersByLevel[i].Count > 0)
            {
                ChapterConfig chosenConfig = chaptersByLevel[i][Random.Range(0, chaptersByLevel[i].Count)];
                selectedConfigs.Add(chosenConfig);
                Debug.Log($"[StoryMap] Уровень {i}: Выбран вариант '{chosenConfig.chapterName}' (Тема: {chosenConfig.themeId})");
            }
            else
            {
                Debug.LogWarning($"[StoryMap] Не найдено конфигов для уровня {i}!");
            }
        }

        if (selectedConfigs.Count == 0) return;

        // === ШАГ 2: ГЕНЕРАЦИЯ КАРТЫ ТОЛЬКО ДЛЯ ВЫБРАННЫХ ГЛАВ ===

        CurrentChapters = new List<StoryChapter>();
        SaveData = new StorySaveData();
        int globalNodeIdCounter = 0;
        int previousBossNodeId = -1;

        foreach (var config in selectedConfigs)
        {
            Debug.Log($"[StoryMap] Генерация выбранной главы: {config.chapterName} (Индекс: {config.chapterIndex})");

            // 1. Генерация и загрузка ландшафта ЭТОЙ главы в Unity
            if (MapGenerator3D.Instance != null)
            {
                MapGenerator3D.Instance.GenerateAndSaveSingleChapterData(config.chapterIndex, config.themeId);
                MapGenerator3D.Instance.ApplyThemeAndLoadChapter(config.chapterIndex, config.themeId);
            }

            // 2. Генерация нодов
            StoryChapter chapter = StoryMapGenerator.GenerateSingleChapter(config, generationSettings, ref globalNodeIdCounter, previousBossNodeId);

            if (chapter != null)
            {
                chapter.themeId = config.themeId;

                StoryNode bossNode = chapter.nodes.Find(n => n.type == StoryNodeType.BOSS);
                if (bossNode != null) previousBossNodeId = bossNode.nodeId;

                CurrentChapters.Add(chapter);

                // 3. Отрисовка путей сразу же
                DrawPathsForChapter(chapter);

                Debug.Log($"[StoryMap] Глава {config.chapterIndex} полностью сгенерирована.");
            }
            else
            {
                Debug.LogError($"[StoryMap] Ошибка генерации главы {config.chapterIndex}");
            }
        }

        Debug.Log("[StoryMap] Все главы сгенерированы. Сохранение структуры...");
        SaveChapters();
        SaveMap();

        // === ШАГ 3: ИНИЦИАЛИЗАЦИЯ ПЕРВОЙ ГЛАВЫ ===
        if (CurrentChapters.Count > 0)
        {
            CurrentChapter = CurrentChapters[0];
            ReloadCurrentChapterVisuals();
        }

        Debug.Log("[StoryMap] Новая игра готова.");
        loadingPanel.SetActive(false);

    }

    void DrawPathsForChapter(StoryChapter chapter)
    {
        if (MapDecorationManager.Instance == null) return;

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

        MapDecorationManager.Instance.DrawPathsOnTexture(paths, chapter.chapterIndex);
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

            StoryChapter chapterToDraw = CurrentChapters.FirstOrDefault(ch => ch.isUnlocked && !ch.isCompleted);
            if (chapterToDraw == null)
                chapterToDraw = CurrentChapters.FirstOrDefault(ch => ch.chapterIndex == SaveData.currentChapter) ?? CurrentChapters.Last();

            CurrentChapter = chapterToDraw;
            ReloadCurrentChapterVisuals();

            Debug.Log("=== Загрузка завершена ===");
            loadingPanel.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Ошибка загрузки: " + e.Message);
            StartNewGame();
        }
    }

    void ReloadCurrentChapterVisuals()
    {
        if (CurrentChapter == null) return;

        if (MapGenerator3D.Instance != null)
        {
            MapGenerator3D.Instance.ApplyThemeAndLoadChapter(CurrentChapter.chapterIndex, CurrentChapter.themeId);
        }

        SnapNodesToTerrain(CurrentChapter);

        if (MapDecorationManager.Instance != null)
        {
            MapDecorationManager.Instance.LoadAndApplySavedPaths(CurrentChapter.chapterIndex);
            MapDecorationManager.Instance.LoadAndSpawnDecorations(CurrentChapter.chapterIndex);
        }

        DrawCurrentChapter(CurrentChapter);
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
            string name = string.IsNullOrEmpty(chData.chapterName) ? $"Глава {chData.chapterIndex + 1}" : chData.chapterName;

            StoryChapter ch = new StoryChapter(chData.chapterIndex, name)
            {
                isCompleted = chData.isCompleted,
                isUnlocked = chData.isUnlocked,
                isActive = chData.isActive,
                themeId = tid,
                enemyPoolId = chData.enemyPoolId,
                eventPoolId = chData.eventPoolId,
                bossPoolId = chData.bossPoolId
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

        if (CurrentChapter != null)
            SaveData.currentChapter = CurrentChapter.chapterIndex;

        foreach (var ch in CurrentChapters)
        {
            ChapterSaveData cd = new ChapterSaveData
            {
                chapterIndex = ch.chapterIndex,
                chapterName = ch.chapterName,
                enemyPoolId = ch.enemyPoolId,
                eventPoolId = ch.eventPoolId,
                bossPoolId = ch.bossPoolId,
                themeId = ch.themeId,
                isCompleted = ch.isCompleted,
                isUnlocked = ch.isUnlocked,
                isActive = ch.isActive,
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

        // Если это финал главы, не пытаемся ничего разблокировать (ошибки не будет)
        if (cur.type == StoryNodeType.CHAPTER_END) return;

        StoryChapter ch = CurrentChapters[cur.chapterIndex];
        int unlockedCount = 0;

        foreach (var n in ch.nodes)
        {
            if (n.layer == cur.layer + 1 && cur.connectedNodeIds.Contains(n.nodeId))
            {
                n.isUnlocked = true;
                unlockedCount++;
            }
        }

        if (unlockedCount == 0)
        {
            Debug.LogWarning($"[Unlock] Не найдено соединений для узла {cur.nodeId}.");
        }
    }

    public void ProceedToNextChapter()
    {
        if (CurrentChapter == null) return;

        int currentIndex = CurrentChapters.IndexOf(CurrentChapter);

        if (currentIndex >= 0 && currentIndex < CurrentChapters.Count - 1)
        {
            loadingPanel.SetActive(true);
            Debug.Log($"[StoryMap] Переход к главе: {CurrentChapters[currentIndex + 1].chapterName}");

            // 1. Подготавливаем данные следующей главы
            CurrentChapter.isCompleted = true;
            CurrentChapter.isActive = false;

            StoryChapter nextCh = CurrentChapters[currentIndex + 1];
            nextCh.isUnlocked = true;
            nextCh.isActive = true;
            CurrentChapter = nextCh;

            // 2. Находим старт и разблокируем его
            StoryNode start = nextCh.nodes.Find(n => n.type == StoryNodeType.START);
            if (start != null)
            {
                CurrentNode = start;
                start.isUnlocked = true;
                UnlockNextLayerNodes(start);
            }

            SaveChapters();
            SaveMap();

            // === КРИТИЧЕСКИ ВАЖНЫЙ ПОРЯДОК ДЛЯ ВИЗУАЛА ===

            // А. Сначала очищаем старые визуальные ноды
            StoryMapVisual vis = FindObjectOfType<StoryMapVisual>();
            if (vis != null)
            {
                vis.ClearVisuals();
            }

            // Б. ЗАГРУЖАЕМ НОВЫЙ ЛАНДШАФТ (Высоты меняются здесь)
            if (MapGenerator3D.Instance != null)
            {
                MapGenerator3D.Instance.ApplyThemeAndLoadChapter(CurrentChapter.chapterIndex, CurrentChapter.themeId);
            }

            // В. СРАЗУ ЖЕ ПЕРЕСЧИТЫВАЕМ ВЫСОТЫ НОД в памяти под новый ландшафт
            // Это гарантирует, что координаты верны ДО создания префабов
            SnapNodesToTerrain(CurrentChapter);

            // Г. Только теперь создаем новые визуальные ноды на основе исправленных координат
            if (vis != null)
            {
                vis.InitializeAndDisplay(CurrentChapter);
                vis.RefreshAllNodeVisuals();
            }

            // Д. Загружаем текстуру с путями поверх нового ландшафта
            if (MapDecorationManager.Instance != null)
            {
                MapDecorationManager.Instance.LoadAndApplySavedPaths(CurrentChapter.chapterIndex);
                MapDecorationManager.Instance.LoadAndSpawnDecorations(CurrentChapter.chapterIndex);
            }

            Debug.Log("[StoryMap] Глава обновлена. Ноды должны стоять на поверхности.");
            loadingPanel.SetActive(false);
        }
        else
        {
            Debug.Log("[StoryMap] Это была последняя глава. Игра пройдена!");
            FinishGame();
        }
    }

    // === НОВЫЙ МЕТОД: ФИНАЛ ИГРЫ ===
    // === МЕТОД: ФИНАЛ ИГРЫ С ОЧИСТКОЙ СОХРАНЕНИЙ ===
    public void FinishGame()
    {
        Debug.Log("==================================================");
        Debug.Log("=== ПОЗДРАВЛЯЕМ! ВЫ ПРОШЛИ ИГРУ ДО КОНЦА! ===");
        Debug.Log("==================================================");
        Debug.Log("[FinishGame] Очистка сохранений для нового прохождения...");

        // 1. Очищаем прогресс игрока (HP, Gold, Колода)
        if (PlayerProgressionManager.Instance != null)
        {
            PlayerProgressionManager.Instance.DeleteSave();
            Debug.Log("[FinishGame] Данные игрока сброшены.");
        }

        // 2. Очищаем сохранение структуры карты (storyMapSave.json)
        DeleteSave();
        Debug.Log("[FinishGame] Файл storyMapSave.json удален.");

        // 3. Очищаем файлы ландшафтов и текстур глав (map_chapter_X.json, .png)
        if (MapGenerator3D.Instance != null)
        {
            MapGenerator3D.Instance.DeleteAllChapterMaps();
            Debug.Log("[FinishGame] Файлы ландшафтов и текстур удалены.");
        }

        Debug.Log("[FinishGame] Все сохранения очищены. Игра готова к новому запуску.");

        // 4. Показываем экран победы
        ShowVictoryScreen();
    }

    // === НОВЫЙ МЕТОД: ПОКАЗ ЭКРАНА ПОБЕДЫ ===
    void ShowVictoryScreen()
    {
        if (victoryPanelPrefab == null)
        {
            Debug.LogError("[Victory] Префаб экрана победы не назначен в инспекторе StoryMapManager!");
            return;
        }

        // Находим Canvas (MainCanvas)
        GameObject canvasObj = GameObject.Find("UI_HUD_Canvas");
        if (canvasObj == null)
        {
            Debug.LogError("[Victory] Не найден объект MainCanvas на сцене!");
            return;
        }

        // Создаем экземпляр префаба внутри Canvas
        activeVictoryPanel = Instantiate(victoryPanelPrefab, canvasObj.transform);

        // Убеждаемся, что он активен
        if (!activeVictoryPanel.activeSelf)
            activeVictoryPanel.SetActive(true);

        // Растягиваем на весь экран
        RectTransform rt = activeVictoryPanel.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling(); // Поверх всего
        }

        Debug.Log("[Victory] Экран победы показан!");
    }

    // Метод для кнопки "В меню" (если добавите кнопку на панель)
    public void ReturnToMainMenu()
    {
        Debug.Log("Возврат в главное меню...");
        // UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    [ContextMenu("Regenerate Map")]
    public void RegenerateMap()
    {
        DeleteSave();
        if (MapGenerator3D.Instance != null) MapGenerator3D.Instance.DeleteAllChapterMaps();
        StartNewGame();
    }
}