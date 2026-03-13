using UnityEngine;
using System.Collections.Generic;

public class StoryMapVisual : MonoBehaviour
{
    [Header("Префаб узла")]
    public GameObject nodeBasePrefab;

    [Header("Контейнеры")]
    public Transform nodesContainer;
    public Transform linesContainer;

    [Header("Линия")]
    public LineRenderer linePrefab;

    private Dictionary<int, GameObject> nodeVisuals = new Dictionary<int, GameObject>();
    private StoryMapManager mapManager;
    private Terrain terrain;


    private void Awake()
    {
        mapManager = StoryMapManager.Instance;
        terrain = FindObjectOfType<Terrain>();
    }

    // Изменён: не отображает визуалы до инициализации
    public void DisplayChapter(StoryChapter chapter)
    {
        if (chapter == null)
        {
            Debug.LogError("Chapter is null!");
            return;
        }

        ClearVisuals();

        foreach (var node in chapter.nodes)
        {
            CreateNodeVisual(node);
        }

        DrawConnections(chapter);
    }

    // Новый метод: инициализирует и отображает визуалы
    public void InitializeAndDisplay(StoryChapter chapter)
    {
        ClearVisuals();

        foreach (var node in chapter.nodes)
        {
            CreateNodeVisual(node); // создаёт визуал, но не обновляет статус
        }

        DrawConnections(chapter);

        // Обновляем ВСЕ визуалы ОДИН РАЗ после полной инициализации
        RefreshAllNodeVisuals();
    }

    void CreateNodeVisual(StoryNode node)
    {
        if (nodeBasePrefab == null)
        {
            Debug.LogError("Node prefab not assigned!");
            return;
        }

        float terrainHeight = GetTerrainHeight(node.position);
        Vector3 spawnPosition = new Vector3(node.position.x, terrainHeight + 5f, node.position.z);

        GameObject nodeVisual = Instantiate(nodeBasePrefab, nodesContainer);
        nodeVisual.transform.localPosition = spawnPosition;

        Canvas canvas = nodeVisual.GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
        }

        NodeVisualController controller = nodeVisual.GetComponent<NodeVisualController>();
        if (controller != null)
        {
            controller.Init(node, this);
            // УБРАЛИ: controller.UpdateVisualState();
        }

        nodeVisuals[node.nodeId] = nodeVisual;
        // УБРАЛИ: UpdateNodeVisualState(node);
    }

    void DrawConnections(StoryChapter chapter)
    {
        if (linePrefab == null)
        {
            Debug.LogWarning("LineRenderer prefab not assigned!");
            return;
        }

        foreach (var node in chapter.nodes)
        {
            foreach (int connectedId in node.connectedNodeIds)
            {
                if (nodeVisuals.ContainsKey(connectedId))
                {
                    CreateConnection(node, connectedId);
                }
            }
        }
    }

    void CreateConnection(StoryNode fromNode, int toNodeId)
    {
        GameObject fromVisual = nodeVisuals[fromNode.nodeId];
        GameObject toVisual = nodeVisuals[toNodeId];
        LineRenderer line = Instantiate(linePrefab, linesContainer);

        Vector3 startPos = fromVisual.transform.position;
        Vector3 endPos = toVisual.transform.position;

        int segments = 20;
        List<Vector3> linePoints = new List<Vector3>();

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = Vector3.Lerp(startPos, endPos, t);

            float terrainHeight = GetTerrainHeight(point);
            point.y = terrainHeight + 3f;

            linePoints.Add(point);
        }

        line.positionCount = linePoints.Count;
        line.SetPositions(linePoints.ToArray());
        line.useWorldSpace = true;
    }

    float GetTerrainHeight(Vector3 position)
    {
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("Terrain not found!");
            return 0;
        }

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        float normalizedX = (position.x - terrainPos.x) / terrainData.size.x;
        float normalizedZ = (position.z - terrainPos.z) / terrainData.size.z;

        normalizedX = Mathf.Clamp01(normalizedX);
        normalizedZ = Mathf.Clamp01(normalizedZ);

        int heightmapResolution = terrainData.heightmapResolution;
        int x = Mathf.FloorToInt(normalizedX * (heightmapResolution - 1));
        int y = Mathf.FloorToInt(normalizedZ * (heightmapResolution - 1));

        return terrainData.GetHeight(x, y);
    }

    public void ClearVisuals()
    {
        foreach (var visual in nodeVisuals.Values)
        {
            if (visual != null)
                Destroy(visual);
        }
        nodeVisuals.Clear();

        if (linesContainer != null)
        {
            foreach (Transform child in linesContainer)
            {
                if (child != null)
                    Destroy(child.gameObject);
            }
        }
    }

    public void UpdateNodeVisualState(StoryNode node)
    {
        if (nodeVisuals.ContainsKey(node.nodeId))
        {
            NodeVisualController controller = nodeVisuals[node.nodeId].GetComponent<NodeVisualController>();
            if (controller != null)
            {
                controller.UpdateVisualState();
            }
        }
    }

    public void RefreshAllNodeVisuals()
    {
        foreach (var visual in nodeVisuals.Values)
        {
            if (visual != null)
            {
                NodeVisualController controller = visual.GetComponent<NodeVisualController>();
                if (controller != null)
                {
                    controller.UpdateVisualState();
                }
            }
        }
    }

    public void OnNodeClicked(StoryNode node)
    {
        if (!node.isUnlocked || node.isVisited)
            return;

        StoryNode currentNode = mapManager.CurrentNode;

        if (currentNode != null && node.layer != currentNode.layer + 1)
        {
            Debug.Log("Узел не является следующим слоем!");
            return;
        }

        switch (node.type)
        {
            case StoryNodeType.ENEMY:
            case StoryNodeType.BOSS:
                StartBattle(node);
                break;
            case StoryNodeType.EVENT:
                ShowEvent(node);
                break;
            case StoryNodeType.REST:
                Rest(node);
                break;
            case StoryNodeType.START:
                break;
        }

        mapManager.MarkNodeVisited(node.chapterIndex, node.nodeId);
        RefreshAllNodeVisuals();
    }

    void StartBattle(StoryNode node)
    {
        Debug.Log($"Бой: враг {node.enemyId}");

        EnemyData enemy = StoryContentLoader.GetEnemyById(node.enemyId);
        if (enemy == null)
        {
            Debug.LogError("Враг не найден!");
            return;
        }

        TempData.CurrentEnemy = enemy;
        TempData.CurrentNode = node;

        Debug.Log("Переход к бою в сцене...");
    }

    void ShowEvent(StoryNode node)
    {
        Debug.Log($"Событие: {node.eventId}");

        StoryEventData eventData = StoryContentLoader.GetEventById(node.eventId);
        if (eventData == null)
        {
            Debug.LogError("Событие не найдено!");
            return;
        }

        TempData.CurrentEvent = eventData;
        TempData.CurrentNode = node;

        Debug.Log("Открытие UI события...");
    }

    void Rest(StoryNode node)
    {
        Debug.Log("Отдых: восстановление HP");
        Debug.Log("Здоровье восстановлено!");
    }
}