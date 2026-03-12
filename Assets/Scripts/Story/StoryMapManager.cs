using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Unity-контроллер для управления картой сюжета
/// Вызывает генератор, сохраняет/загружает данные
/// </summary>
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
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeSettings();
        StoryContentLoader.LoadAllContent();

        if (HasSavedMap())
        {
            LoadMap();
        }
        else
        {
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
                nodeSpacingZ = 12f,    // Расстояние вдоль карты (Z)
                startZ = 30f,          // Начало по Z (ближний край)
                minNodeDistance = 25f, // Минимальное расстояние между узлами
                maxBranches = 3,
                connectionChance = 0.7f,
                minNodesPerChapter = 10,
                maxNodesPerChapter = 15
            };
        }
    }

    /// <summary>
    /// Генерация новой карты
    /// </summary>
    public void GenerateNewMap()
    {
        Debug.Log("Генерация новой карты сюжета...");

        CurrentChapters = StoryMapGenerator.GenerateFullMap(generationSettings);
        SaveData = new StorySaveData();

        SaveChapters();
        SaveMap();

        Debug.Log($"Сгенерировано {CurrentChapters.Count} глав");

        if (CurrentChapters.Count > 0)
        {
            StoryMapVisual visual = FindObjectOfType<StoryMapVisual>();
            if (visual != null)
            {
                visual.DisplayChapter(CurrentChapters[0]);
            }
        }
    }

    /// <summary>
    /// Загрузка карты из файла
    /// </summary>
    public void LoadMap()
    {
        Debug.Log("Загрузка сохранённой карты...");

        string path = GetSavePath();
        string json = File.ReadAllText(path);

        SaveData = JsonUtility.FromJson<StorySaveData>(json);

        if (SaveData == null || SaveData.chapters == null)
        {
            Debug.LogError("Не удалось загрузить данные карты!");
            GenerateNewMap();
            return;
        }

        RestoreChapters();

        if (CurrentChapters.Count > 0)
        {
            StoryMapVisual visual = FindObjectOfType<StoryMapVisual>();
            if (visual != null)
            {
                visual.DisplayChapter(CurrentChapters[0]);
            }
        }
    }

    /// <summary>
    /// Сохранение глав в SaveData
    /// </summary>
    void SaveChapters()
    {
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
                NodeSaveData nodeData = new NodeSaveData
                {
                    nodeId = node.nodeId,
                    layer = node.layer,                    // ИСПРАВЛЕНО: сохраняем layer
                    type = node.type,
                    isVisited = node.isVisited,
                    isUnlocked = node.isUnlocked,
                    isSkipped = node.isSkipped,            // ИСПРАВЛЕНО: сохраняем isSkipped
                    position = node.position,
                    enemyId = node.enemyId,
                    eventId = node.eventId,
                    connectedNodeIds = new List<int>(node.connectedNodeIds)
                };
                chapterData.nodes.Add(nodeData);
            }

            SaveData.chapters.Add(chapterData);
        }
    }

    /// <summary>
    /// Восстановление глав из SaveData
    /// </summary>
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
                    nodeData.layer,                        // ИСПРАВЛЕНО: добавлен параметр layer
                    nodeData.type,
                    nodeData.position
                )
                {
                    isVisited = nodeData.isVisited,
                    isUnlocked = nodeData.isUnlocked,
                    isSkipped = nodeData.isSkipped,        // ИСПРАВЛЕНО: восстанавливаем isSkipped
                    enemyId = nodeData.enemyId,
                    eventId = nodeData.eventId,
                    connectedNodeIds = nodeData.connectedNodeIds
                };

                chapter.nodes.Add(node);
            }

            CurrentChapters.Add(chapter);
        }
    }

    /// <summary>
    /// Сохранение в файл
    /// </summary>
    void SaveMap()
    {
        Debug.Log("Сохранение карты сюжета...");

        string json = JsonUtility.ToJson(SaveData, true);
        string path = GetSavePath();

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
        Debug.Log($"Карта сохранена в {path}");
    }

    /// <summary>
    /// Проверка наличия сохранения
    /// </summary>
    bool HasSavedMap()
    {
        string path = GetSavePath();
        return File.Exists(path);
    }

    string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, saveFileName);
    }

    /// <summary>
    /// Удалить сохранение (для новой игры)
    /// </summary>
    public void DeleteSave()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Сохранение карты сюжета удалено");
        }
    }

    /// <summary>
    /// Получить узел по ID
    /// </summary>
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
        StoryNode node = GetNodeById(chapterIndex, nodeId);
        if (node != null)
        {
            node.isVisited = true;
            CurrentNode = node;

            MarkSameLayerAsSkipped(chapterIndex, node.layer, nodeId);
            UnlockNextLayerNodes(node);

            SaveMap();
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

    /// <summary>
    /// Начать новую игру (сброс прогресса)
    /// </summary>
    public void StartNewGame()
    {
        DeleteSave();
        StoryContentLoader.ClearContent();
        GenerateNewMap();
    }

    [ContextMenu("Regenerate Map")]
    public void RegenerateMap()
    {
        DeleteSave();
        GenerateNewMap();
    }
}