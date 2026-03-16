using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Collections; // Нужно для IEnumerator

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
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ИСПОЛЬЗУЕМ OnEnable ЧТОБЫ РЕАГИРОВАТЬ НА КАЖДОЕ ВКЛЮЧЕНИЕ ОБЪЕКТА (В Т.Ч. ПРИ ЗАГРУЗКЕ СЦЕНЫ)
    void Start()
    {
        Debug.Log("[Manager] Start вызван. Инициализация...");

        InitializeSettings();
        StoryContentLoader.LoadAllContent();

        if (HasSavedMap())
        {
            Debug.Log("[Manager] Сохранение найдено. Загружаем...");
            LoadMap();
        }
        else
        {
            Debug.Log("[Manager] Сохранения нет. Генерируем...");
            GenerateNewMap();
        }
    }

    void InitializeSettings()
    {
        if (generationSettings == null)
        {
            generationSettings = new StoryMapGenerator.GenerationSettings
            {
                nodeSpacingY = 3f,
                nodeSpacingX = 4f,
                nodeSpacingZ = 15f,
                startZ = 10f,
                minNodeDistance = 25f,
                maxBranches = 3,
                connectionChance = 0.7f,
                minNodesPerChapter = 10,
                maxNodesPerChapter = 15
            };
        }
    }

    public void GenerateNewMap()
    {
        Debug.Log("Генерация новой карты сюжета...");
        CurrentChapters = StoryMapGenerator.GenerateFullMap(generationSettings);
        SaveData = new StorySaveData();

        SaveChapters();
        SaveMap();

        if (CurrentChapters.Count > 0)
        {
            DrawCurrentChapter();
        }
    }

    public void LoadMap()
    {
        Debug.Log("=== [START] Загрузка сохранённой карты сюжета... ===");

        string path = GetSavePath();

        try
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"Файл сохранения сюжета не найден: {path}");
                GenerateNewMap();
                return;
            }

            string json = File.ReadAllText(path);
            SaveData = JsonUtility.FromJson<StorySaveData>(json);

            if (SaveData == null || SaveData.chapters == null || SaveData.chapters.Count == 0)
            {
                Debug.LogError("Ошибка: Данные глав пусты или некорректны!");
                GenerateNewMap();
                return;
            }

            Debug.Log($"Успешно загружено глав: {SaveData.chapters.Count}");

            RestoreChapters();

            if (CurrentChapters.Count == 0)
            {
                Debug.LogError("Не удалось восстановить главы в памяти!");
                return;
            }

            // Рисуем загруженную главу
            DrawCurrentChapter();

            Debug.Log("=== [END] Карта сюжета успешно загружена ===");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Критическая ошибка при загрузке сюжета: " + e.Message);
            GenerateNewMap();
        }
    }

    // Вспомогательный метод для отрисовки текущей главы
    void DrawCurrentChapter()
    {
        if (CurrentChapters.Count == 0) return;

        StoryMapVisual visual = FindObjectOfType<StoryMapVisual>();
        if (visual == null)
        {
            Debug.LogError("StoryMapVisual не найден на сцене! Узлы не будут отображены.");
            return;
        }

        StoryChapter chapter = CurrentChapters[0];

        Debug.Log($"Отрисовка главы: {chapter.chapterName}, узлов: {chapter.nodes.Count}");

        visual.ClearVisuals();
        visual.DisplayChapter(chapter);

        // Восстанавливаем текущий узел (последний посещенный)
        // НОВЫЙ КОД: Ищем узел с максимальным номером слоя среди посещенных
        StoryNode lastVisited = null;
        int maxLayer = -1;

        foreach (var node in chapter.nodes)
        {
            if (node.isVisited)
            {
                // Если слой больше текущего максимума — запоминаем узел
                if (node.layer > maxLayer)
                {
                    maxLayer = node.layer;
                    lastVisited = node;
                }
                // Если слои равны, можно добавить дополнительную логику, но обычно достаточно номера слоя
            }
        }

        // Если ни один узел не посещен (совсем новая игра), берем стартовый
        if (lastVisited == null)
        {
            lastVisited = chapter.nodes.Find(n => n.type == StoryNodeType.START);
            Debug.LogWarning("Ни один узел не найден как посещенный. Сброс на старт.");
        }

        CurrentNode = lastVisited;
        Debug.Log($"[Load] Восстановлен прогресс. Последний узел: {CurrentNode.nodeId} (Слой {CurrentNode.layer})");

        if (lastVisited == null)
        {
            lastVisited = chapter.nodes.Find(n => n.type == StoryNodeType.START);
        }

        if (lastVisited != null)
        {
            CurrentNode = lastVisited;
            UnlockNextLayerNodes(CurrentNode);
            visual.RefreshAllNodeVisuals();
            Debug.Log($"Текущий узел установлен: {CurrentNode.type} (Layer {CurrentNode.layer})");
        }
    }

    void SaveChapters()
    {
        if (SaveData.chapters == null)
            SaveData.chapters = new List<ChapterSaveData>();

        SaveData.chapters.Clear();

        foreach (var chapter in CurrentChapters)
        {
            ChapterSaveData chapterData = new ChapterSaveData
            {
                chapterIndex = chapter.chapterIndex,
                isCompleted = chapter.isCompleted,
                isUnlocked = chapter.isUnlocked,
                nodes = new List<NodeSaveData>()
            };

            foreach (var node in chapter.nodes)
            {
                // Создаем объект сохранения
                NodeSaveData nodeData = new NodeSaveData
                {
                    nodeId = node.nodeId,
                    layer = node.layer,
                    type = node.type,
                    position = node.position,
                    enemyId = node.enemyId,
                    eventId = node.eventId,
                    connectedNodeIds = new List<int>(node.connectedNodeIds),

                    // Явно копируем флаги
                    isVisited = node.isVisited,
                    isUnlocked = node.isUnlocked,
                    isSkipped = node.isSkipped
                };

                // === ОТЛАДКА: Печатаем состояние ПЕРЕД добавлением в список ===
                if (node.isVisited)
                {
                    Debug.Log($"[SAVE DEBUG] Узел ID:{node.nodeId} (Слой {node.layer}) -> isVisited = TRUE");
                }
                else
                {
                    // Раскомментируйте, если хотите видеть и непосещенные
                    // Debug.Log($"[SAVE DEBUG] Узел ID:{node.nodeId} -> isVisited = FALSE");
                }

                chapterData.nodes.Add(nodeData);
            }
            // В конец метода SaveChapters, после цикла foreach
            int totalVisited = 0;
            foreach (var ch in CurrentChapters)
                foreach (var n in ch.nodes)
                    if (n.isVisited) totalVisited++;

            Debug.Log($"[SAVE SUMMARY] Всего посещенных узлов в списке CurrentChapters: {totalVisited}");
            SaveData.chapters.Add(chapterData);
        }

        Debug.Log($"[SAVE DEBUG] Всего глав подготовлено к сохранению: {SaveData.chapters.Count}");
    }

    void RestoreChapters()
    {
        CurrentChapters = new List<StoryChapter>();

        foreach (var chapterData in SaveData.chapters)
        {
            StoryChapter chapter = new StoryChapter(chapterData.chapterIndex, $"Chapter {chapterData.chapterIndex + 1}")
            {
                isCompleted = chapterData.isCompleted,
                isUnlocked = chapterData.isUnlocked
            };

            foreach (var nodeData in chapterData.nodes)
            {
                StoryNode node = new StoryNode(
                    nodeData.nodeId,
                    chapterData.chapterIndex,
                    nodeData.layer,
                    nodeData.type,
                    nodeData.position
                )
                {
                    isVisited = nodeData.isVisited,
                    isUnlocked = nodeData.isUnlocked,
                    isSkipped = nodeData.isSkipped,
                    enemyId = nodeData.enemyId,
                    eventId = nodeData.eventId,
                    connectedNodeIds = nodeData.connectedNodeIds
                };

                chapter.nodes.Add(node);
            }

            CurrentChapters.Add(chapter);
        }
    }

    void SaveMap()
    {
        // === КРИТИЧЕСКОЕ ИЗМЕНЕНИЕ ===
        // Сначала обновляем данные в объекте SaveData из актуального списка CurrentChapters
        SaveChapters();
        Debug.Log("[SAVE] Данные обновлены из CurrentChapters.");
        // ==============================

        Debug.Log("Сохранение данных карты...");

        string json = JsonUtility.ToJson(SaveData, true);
        string path = GetSavePath();

        Debug.Log("Файл сохранён: " + path);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);

        Debug.Log("Карта сохранена в " + path);

        // Опционально: выводим сводку после сохранения
        int visitedCount = 0;
        foreach (var ch in CurrentChapters)
            foreach (var n in ch.nodes)
                if (n.isVisited) visitedCount++;
        Debug.Log($"[SAVE SUMMARY] Всего посещенных узлов в файле: {visitedCount}");
    }

    public bool HasSavedMap()
    {
        string path = GetSavePath();
        return File.Exists(path);
    }

    string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, saveFileName);
    }

    public void DeleteSave()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Сохранённая карта удалена");
        }
    }

    public StoryNode GetNodeById(int chapterIndex, int nodeId)
    {
        if (chapterIndex >= 0 && chapterIndex < CurrentChapters.Count)
        {
            return CurrentChapters[chapterIndex].nodes.Find(n => n.nodeId == nodeId);
        }
        return null;
    }

    public void MarkNodeVisited(int chapterIndex, int nodeId)
    {
        Debug.Log($"[SAVE] Вызван MarkNodeVisited для узла {nodeId}");
        StoryNode node = GetNodeById(chapterIndex, nodeId);

        if (node != null)
        {
            // 1. Меняем флаги в памяти
            if (CurrentNode != null && CurrentNode.nodeId != nodeId)
            {
                CurrentNode.isVisited = true;
                Debug.Log($"[SAVE] Предыдущий узел {CurrentNode.nodeId} помечен как VISITED.");
            }

            node.isVisited = true;
            CurrentNode = node;
            Debug.Log($"[SAVE] Новый текущий узел {node.nodeId} помечен как VISITED.");

            MarkSameLayerAsSkipped(chapterIndex, node.layer, nodeId);
            UnlockNextLayerNodes(node);

            // 2. Просто сохраняем. SaveChapters() вызовется внутри автоматически.
            Debug.Log("[SAVE] Вызов SaveMap...");
            SaveMap();
            Debug.Log("[SAVE] Файл записан");
        }
        else
        {
            Debug.LogError($"[ERROR] Узел {nodeId} не найден!");
        }
    }

    void MarkSameLayerAsSkipped(int chapterIndex, int layer, int visitedNodeId)
    {
        StoryChapter chapter = CurrentChapters[chapterIndex];

        foreach (var node in chapter.nodes)
        {
            if (node.layer == layer && node.nodeId != visitedNodeId)
            {
                node.isSkipped = true;
                node.isUnlocked = false;
            }
        }
    }

    void UnlockNextLayerNodes(StoryNode currentNode)
    {
        if (currentNode == null) return;

        StoryChapter chapter = CurrentChapters[currentNode.chapterIndex];

        foreach (var node in chapter.nodes)
        {
            if (node.layer == currentNode.layer + 1 &&
                currentNode.connectedNodeIds.Contains(node.nodeId))
            {
                node.isUnlocked = true;
            }
        }
    }

    public void StartNewGame()
    {
        Debug.Log("[StoryMap] Начало новой игры. Удаление старых сохранений...");
        DeleteSave(); // Удаляем файл storyMapSave.json

        // Опционально: можно перегенерировать ландшафт, если он тоже сохраняется отдельно
        // MapGenerator3D.Instance?.DeleteSave(); 

        StoryContentLoader.ClearContent(); // Очищаем кэш контента, чтобы перечитать JSON (если они менялись)
        StoryContentLoader.LoadAllContent(); // Загружаем заново

        GenerateNewMap(); // Генерируем новую карту
    }

    [ContextMenu("Regenerate Map")]
    public void RegenerateMap()
    {
        DeleteSave();
        GenerateNewMap();
    }
}