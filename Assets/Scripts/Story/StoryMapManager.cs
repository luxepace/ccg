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
        Debug.Log("Генерация новой карты...");

        CurrentChapters = StoryMapGenerator.GenerateFullMap(generationSettings);
        SaveData = new StorySaveData();

        SaveChapters();
        SaveMap();

        Debug.Log("Сгенерировано " + CurrentChapters.Count + " глав");

        if (CurrentChapters.Count > 0)
        {
            StoryMapVisual visual = FindObjectOfType<StoryMapVisual>();
            if (visual != null)
            {
                // Отображаем первую главу
                visual.DisplayChapter(CurrentChapters[0]);

                // Находим стартовый узел
                StoryNode startNode = CurrentChapters[0].nodes.Find(n => n.type == StoryNodeType.START);

                if (startNode != null)
                {
                    CurrentNode = startNode;

                    // Разблокируем узлы следующего слоя от старта
                    UnlockNextLayerNodes(CurrentNode);

                    // Сохраняем состояние
                    SaveMap();

                    // Обновляем визуал (цвета, замки)
                    visual.RefreshAllNodeVisuals();
                }
                else
                {
                    Debug.LogError("Стартовый узел не найден в сгенерированной главе!");
                }
            }
        }
    }

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

                StoryNode lastVisitedNode = null;
                foreach (var node in CurrentChapters[0].nodes)
                {
                    if (node.isVisited && (lastVisitedNode == null || node.layer > lastVisitedNode.layer))
                    {
                        lastVisitedNode = node;
                    }
                }

                if (lastVisitedNode == null)
                {
                    lastVisitedNode = CurrentChapters[0].nodes.Find(n => n.type == StoryNodeType.START);
                }

                CurrentNode = lastVisitedNode;
                UnlockNextLayerNodes(CurrentNode);

                visual.RefreshAllNodeVisuals();
            }
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
                NodeSaveData nodeData = new NodeSaveData
                {
                    nodeId = node.nodeId,
                    layer = node.layer,
                    type = node.type,
                    isVisited = node.isVisited,
                    isUnlocked = node.isUnlocked,
                    isSkipped = node.isSkipped,
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
        Debug.Log("Сохранение данных карты...");
        string json = JsonUtility.ToJson(SaveData, true);
        string path = GetSavePath();
        Debug.Log("Файл сохранён: " + path);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
        Debug.Log("Карта сохранена в " + path);
    }

    bool HasSavedMap()
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
        StoryNode node = GetNodeById(chapterIndex, nodeId);
        if (node != null)
        {
            // ГЛАВНОЕ ИСПРАВЛЕНИЕ:
            // Если у нас уже был текущий узел (например, стартовый), и мы переходим на новый,
            // то СТАРЫЙ узел должен стать посещенным.
            if (CurrentNode != null && CurrentNode.nodeId != nodeId)
            {
                CurrentNode.isVisited = true;
            }

            // Теперь помечаем новый узел как посещенный и делаем его текущим
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