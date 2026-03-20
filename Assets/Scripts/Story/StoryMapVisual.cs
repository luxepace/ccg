using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class StoryMapVisual : MonoBehaviour
{
    [Header("Префаб узла")]
    public GameObject nodeBasePrefab;

    [Header("Контейнеры")]
    public Transform nodesContainer;
    // linesContainer больше не нужен для отрисовки, но оставим ссылку если вдруг
    public Transform linesContainer;

    private Dictionary<int, GameObject> nodeVisuals = new Dictionary<int, GameObject>();
    private Terrain terrain;

    private void Awake()
    {
        terrain = FindObjectOfType<Terrain>();
    }

    public void DisplayChapter(StoryChapter chapter)
    {
        if (chapter == null || chapter.nodes == null || chapter.nodes.Count == 0) return;

        Debug.Log($"[StoryMapVisual] Отрисовка {chapter.nodes.Count} узлов...");
        ClearVisuals();

        foreach (var node in chapter.nodes)
        {
            if (node == null) continue;
            CreateNodeVisual(node);
        }

        RefreshAllNodeVisuals();
        Debug.Log("[StoryMapVisual] Узлы отрисованы. Тропинки рисуются на текстуре отдельно.");
    }

    public void InitializeAndDisplay(StoryChapter chapter)
    {
        ClearVisuals();
        foreach (var node in chapter.nodes)
        {
            if (node == null) continue;
            CreateNodeVisual(node);
        }
        RefreshAllNodeVisuals();
    }

    void CreateNodeVisual(StoryNode node)
    {
        if (nodeBasePrefab == null || nodesContainer == null) return;

        float terrainHeight = GetTerrainHeight(node.position);
        // Ноды висят над землей
        Vector3 spawnPosition = new Vector3(node.position.x, terrainHeight + 5f, node.position.z);

        GameObject nodeVisual = Instantiate(nodeBasePrefab, nodesContainer);
        nodeVisual.transform.localPosition = spawnPosition;

        // Разворачиваем UI к камере
        Canvas canvas = nodeVisual.GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
        }

        NodeVisualController controller = nodeVisual.GetComponent<NodeVisualController>();
        if (controller != null)
        {
            controller.Init(node, this);
        }

        nodeVisuals[node.nodeId] = nodeVisual;
    }

    // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

    float GetTerrainHeight(Vector3 position)
    {
        if (terrain == null) terrain = FindObjectOfType<Terrain>();
        if (terrain == null) return 0;

        TerrainData data = terrain.terrainData;
        float nx = Mathf.Clamp01((position.x - terrain.transform.position.x) / data.size.x);
        float nz = Mathf.Clamp01((position.z - terrain.transform.position.z) / data.size.z);
        return data.GetInterpolatedHeight(nx, nz);
    }

    public void ClearVisuals()
    {
        foreach (var visual in nodeVisuals.Values)
        {
            if (visual != null) Destroy(visual);
        }
        nodeVisuals.Clear();

        if (linesContainer != null)
        {
            foreach (Transform child in linesContainer)
            {
                if (child != null) Destroy(child.gameObject);
            }
        }
    }

    public void UpdateNodeVisualState(StoryNode node)
    {
        if (nodeVisuals.ContainsKey(node.nodeId))
        {
            NodeVisualController ctrl = nodeVisuals[node.nodeId].GetComponent<NodeVisualController>();
            if (ctrl != null) ctrl.UpdateVisualState();
        }
    }

    public void RefreshAllNodeVisuals()
    {
        foreach (var visual in nodeVisuals.Values)
        {
            if (visual != null)
            {
                NodeVisualController ctrl = visual.GetComponent<NodeVisualController>();
                if (ctrl != null) ctrl.UpdateVisualState();
            }
        }
    }

    // === ЛОГИКА КЛИКОВ (Без изменений) ===
    public void OnNodeClicked(StoryNode node)
    {
        // ИЗМЕНЕНИЕ: Разрешаем клик, если узел разблокирован И НЕ посещен. 
        // Раньше было !node.isVisited, что правильно, но проверим isUnlocked.
        if (!node.isUnlocked || node.isVisited || node.type == StoryNodeType.START)
        {
            // Если вы кликаете на старт новой главы, он может быть уже "посещен" логически, 
            // но нам нужно разрешить выбор следующего шага. 
            // Однако старт обычно не кликабельный, он просто точка отсчета.
            return;
        }

        // Защита от клика на тот же узел
        if (StoryMapManager.Instance.CurrentNode != null &&
           (StoryMapManager.Instance.CurrentNode.nodeId == node.nodeId))
            return;

        // Передаем клик в менеджер
        StoryMapManager.Instance.MarkNodeVisited(node.chapterIndex, node.nodeId);

        // Обновляем вид этого узла сразу
        UpdateNodeVisualState(node);

        // Запускаем событие
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
            case StoryNodeType.CHAPTER_END:
                ShowChapterEndEvent(node);
                break;
        }
    }

    void StartBattle(StoryNode node)
    {
        BattleStats.Reset();
        PlayerProgressionManager.Instance.PrepareForBattle();
        EnemyData enemy = StoryContentLoader.GetEnemyById(node.enemyId);
        if (enemy == null) return;

        TempData.CurrentEnemy = enemy;
        TempData.CurrentNode = node;
        TempData.IsStoryMode = true;
        SceneManager.LoadScene("CardGame");
    }

    void ShowEvent(StoryNode node)
    {
        GameObject prefab = Resources.Load<GameObject>("Events/EventWindowPrefab");
        if (prefab == null) return;

        GameObject canvasObj = GameObject.Find("MainCanvas") ?? GameObject.Find("Canvas");
        if (canvasObj == null) return;

        GameObject instance = Instantiate(prefab, canvasObj.transform, false);
        RectTransform rt = instance.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();
        }

        EventManager mgr = instance.GetComponent<EventManager>();
        if (mgr != null) mgr.Init(node);
    }

    void Rest(StoryNode node)
    {
        // Логика отдыха (сокращенно, как была)
        string eventId = "rest_max_hp"; // Заглушка, используй свою логику рандома
        StoryNode restNode = new StoryNode(node.nodeId, node.chapterIndex, node.layer, StoryNodeType.EVENT, Vector3.zero);
        restNode.eventId = eventId;
        ShowEvent(restNode);
    }

    void ShowChapterEndEvent(StoryNode node)
    {
        StoryEventData data = StoryContentLoader.GetChapterEndEvent(node.chapterIndex);
        if (data == null) { OnChapterEndEventFinished(node); return; }

        GameObject prefab = Resources.Load<GameObject>("Events/EventWindowPrefab");
        if (prefab == null) return;

        GameObject canvasObj = GameObject.Find("MainCanvas") ?? GameObject.Find("Canvas");
        GameObject instance = Instantiate(prefab, canvasObj.transform, false);
        EventManager mgr = instance.GetComponent<EventManager>();
        if (mgr != null) mgr.InitWithData(data, () => OnChapterEndEventFinished(node));
        else OnChapterEndEventFinished(node);
    }

    void OnChapterEndEventFinished(StoryNode node)
    {
        StoryMapManager.Instance.MarkNodeVisited(node.chapterIndex, node.nodeId);
        StoryMapManager.Instance.ProceedToNextChapter();
    }
}