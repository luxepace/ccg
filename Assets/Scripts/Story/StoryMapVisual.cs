using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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
            Debug.LogError("[StoryMapVisual] Попытка отрисовать null главу!");
            return;
        }

        if (chapter.nodes == null || chapter.nodes.Count == 0)
        {
            Debug.LogError("[StoryMapVisual] В главе нет узлов для отрисовки!");
            return;
        }

        Debug.Log($"[StoryMapVisual] Начало отрисовки {chapter.nodes.Count} узлов...");

        ClearVisuals();

        int createdCount = 0;
        foreach (var node in chapter.nodes)
        {
            if (node == null) continue;
            CreateNodeVisual(node);
            createdCount++;
        }

        Debug.Log($"[StoryMapVisual] Создано визуальных объектов узлов: {createdCount}");

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
            Debug.LogError($"[CRITICAL] Node Base Prefab не назначен в инспекторе StoryMapVisual! Невозможно создать узел {node.nodeId}");
            return;
        }
        if (nodesContainer == null)
        {
            Debug.LogError($"[CRITICAL] Nodes Container не назначен в инспекторе StoryMapVisual!");
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
        Debug.Log($"[UI] Клик получен контроллером узла {node.nodeId}");

        // 1. ПРОВЕРКА: Заблокирован ли узел?
        if (!node.isUnlocked)
        {
            Debug.LogWarning($"[BLOCK] Узел {node.nodeId} заблокирован!");
            return;
        }

        // 2. ПРОВЕРКА: Это стартовый узел? (Он никогда не должен быть активным для клика)
        if (node.type == StoryNodeType.START)
        {
            Debug.Log("[IGNORE] Клик по стартовому узлу проигнорирован.");
            return;
        }

        // 3. ПРОВЕРКА: Игрок уже находится на этом узле?
        if (StoryMapManager.Instance != null && StoryMapManager.Instance.CurrentNode != null)
        {
            if (StoryMapManager.Instance.CurrentNode.nodeId == node.nodeId &&
                StoryMapManager.Instance.CurrentNode.layer == node.layer)
            {
                Debug.Log($"[IGNORE] Игрок уже находится на узле {node.nodeId}. Повторный вход запрещен.");
                return;
            }
        }

        // 4. ПРОВЕРКА: Узел уже посещен? (Нельзя ходить назад)
        // Разрешаем клик только если узел еще НЕ посещен.
        // Исключение: если у тебя есть механика возврата, то эту проверку можно убрать или усложнить.
        if (node.isVisited)
        {
            Debug.LogWarning($"[BLOCK] Узел {node.nodeId} уже посещен. Возврат назад невозможен.");
            return;
        }

        // === ЕСЛИ ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ, ЗАПУСКАЕМ ЛОГИКУ ===

        Debug.Log($"[CLICK] Обработка клика по узлу {node.nodeId} типа {node.type}");

        // 1. Сохраняем прогресс (помечаем узел как посещаемый прямо сейчас)
        if (StoryMapManager.Instance != null)
        {
            StoryMapManager.Instance.MarkNodeVisited(node.chapterIndex, node.nodeId);
        }
        else
        {
            Debug.LogError("[ERROR] StoryMapManager.Instance == NULL!");
            return;
        }

        // 2. Обновляем визуал (снимаем замок, подсвечиваем путь)
        UpdateNodeVisualState(node);

        // 3. Запускаем соответствующее событие
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

            default:
                Debug.LogWarning($"Неизвестный тип узла: {node.type}");
                break;
        }
    }

    // В файле StoryMapVisual.cs

    void StartBattle(StoryNode node)
    {
        Debug.Log($"[BATTLE] Начало боя с узлом {node.nodeId}");

        // 1. СБРОС СТАТИСТИКИ БОЯ
        BattleStats.Reset();

        // 2. ПОДГОТОВКА ИГРОКА (ЛЕЧЕНИЕ ДО МАКСИМУМА)
        // Это ключевое изменение: перед каждым боем ХП становится полным
        PlayerProgressionManager.Instance.PrepareForBattle();

        // 3. СОХРАНЕНИЕ ПРОГРЕССА КАРТЫ (помечаем узел как посещаемый)
        if (StoryMapManager.Instance != null)
        {
            StoryMapManager.Instance.MarkNodeVisited(node.chapterIndex, node.nodeId);
        }

        // 4. ЗАГРУЗКА ДАННЫХ ВРАГА
        EnemyData enemy = StoryContentLoader.GetEnemyById(node.enemyId);
        if (enemy == null)
        {
            Debug.LogError("Враг не найден!");
            return;
        }

        TempData.CurrentEnemy = enemy;
        TempData.CurrentNode = node;
        TempData.IsStoryMode = true;

        Debug.Log("Переход к бою в сцене...");
        UnityEngine.SceneManagement.SceneManager.LoadScene("CardGame");
    }

    void ShowEvent(StoryNode node)
    {
        Debug.Log($"[EVENT] Запуск события: {node.eventId}");

        // 1. Загружаем префаб
        GameObject eventPrefab = Resources.Load<GameObject>("Events/EventWindowPrefab");

        if (eventPrefab == null)
        {
            Debug.LogError("[ERROR] Префаб события не найден! Путь: Assets/Resources/Events/EventWindowPrefab");
            return;
        }

        // 2. Ищем ГЛАВНЫЙ Canvas сцены (обычно он один и называется "Canvas")
        // Мы НЕ используем nodesContainer или любой другой 3D объект как родителя!
        GameObject canvasObj = GameObject.Find("MainCanvas");

        if (canvasObj == null)
        {
            Debug.LogError("Главный Canvas 'MainCanvas' не найден! Проверь имя объекта в иерархии.");
            return;
        }

        Canvas mainCanvas = canvasObj.GetComponent<Canvas>();

        if (mainCanvas == null)
        {
            Debug.LogError("[ERROR] В сцене не найден ни один Canvas! UI не сможет отобразиться корректно.");
            return;
        }

        // 3. Создаем префаб ВНУТРИ главного Canvas
        // false означает, что мы не сохраняем мировую позицию/ротацию префаба, а берем локальные (что нам и нужно для UI)
        GameObject eventInstance = Instantiate(eventPrefab, mainCanvas.transform, false);

        // 4. СБРАСЫВАЕМ трансформацию, чтобы UI встал ровно по центру и на весь экран
        // Это критически важно, иначе он может улететь в сторону или стать микроскопическим
        RectTransform rectTransform = eventInstance.GetComponent<RectTransform>();

        if (rectTransform != null)
        {
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;

            // Если в префабе не настроены Anchors на Stretch-Stretch, можно сделать это здесь:
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
        else
        {
            // Если вдруг нет RectTransform (что странно для UI), пробуем обычный Transform
            eventInstance.transform.localPosition = Vector3.zero;
            eventInstance.transform.localRotation = Quaternion.identity;
            eventInstance.transform.localScale = Vector3.one;
        }

        // Поднимаем на самый верх иерархии Canvas, чтобы было поверх всего (кнопки, тултипы узлов)
        eventInstance.transform.SetAsLastSibling();

        Debug.Log("[OK] Окно события создано на главном Canvas.");

        // 5. Запускаем логику
        EventManager manager = eventInstance.GetComponent<EventManager>();
        if (manager != null)
        {
            manager.Init(node);
        }
        else
        {
            Debug.LogError("[ERROR] У префаба нет компонента EventManager!");
            Destroy(eventInstance);
        }
    }

    void Rest(StoryNode node)
    {
        Debug.Log("[REST] Анализ ситуации для выбора типа привала...");

        string chosenEventId = "";

        // ... (твоя логика сбора статистики HP, ходов и т.д. остается той же) ...
        // Определяем веса кандидатов (словарь ID -> вес)
        Dictionary<string, int> candidates = new Dictionary<string, int>();

        // Пример логики (упрощенно):
        int hpPercent = 100;
        if (BattleStats.PlayerMaxHP > 0)
            hpPercent = Mathf.FloorToInt((float)BattleStats.PlayerEndHP / BattleStats.PlayerMaxHP * 100f);

        if (hpPercent < 30)
        {
            candidates.Add("rest_max_hp", 80);
            candidates.Add("rest_exhaustion", 10);
            candidates.Add("rest_curse", 10);
        }
        else if (BattleStats.TurnsCount > 5)
        {
            candidates.Add("rest_mana_boost", 70);
            candidates.Add("rest_new_card", 20);
            candidates.Add("rest_rat_eats_card", 10);
        }
        else
        {
            // Равный шанс или небольшой перекос
            candidates.Add("rest_max_hp", 17);
            candidates.Add("rest_mana_boost", 17);
            candidates.Add("rest_new_card", 16);
            candidates.Add("rest_rat_eats_card", 17);
            candidates.Add("rest_exhaustion", 17);
            candidates.Add("rest_curse", 16);
        }

        // Выбираем ID по весам
        chosenEventId = GetWeightedRandomEvent(candidates);

        Debug.Log($"[REST] Выбрано событие привала: {chosenEventId}");

        // Ищем событие в НОВОМ списке allRestEvents
        StoryEventData restData = StoryContentLoader.GetRestEventById(chosenEventId);

        if (restData == null)
        {
            Debug.LogError($"[ERROR] Событие привала '{chosenEventId}' не найдено в rest_events.json! Запускаем дефолтное.");
            // Фоллбэк на первое доступное, если вдруг ошибка
            if (StoryContentLoader.AllRestEvents.Count > 0)
                restData = StoryContentLoader.AllRestEvents[0];
            else
                return; // Если совсем ничего нет
        }

        // Создаем узел, передавая обязательные параметры в конструктор
        StoryNode restNode = new StoryNode(
            node.nodeId,          // 1. ID (было node.chapterIndex - НЕВЕРНО)
            node.chapterIndex,    // 2. Глава (было node.nodeId - НЕВЕРНО)
            node.layer,           // 3. Слой
            StoryNodeType.EVENT,  // 4. Тип
            Vector3.zero          // 5. Позиция
        );

        restNode.eventId = chosenEventId;

        ShowEvent(restNode);
    }

    // Твой вспомогательный метод взвешенного рандома
    string GetWeightedRandomEvent(Dictionary<string, int> weights)
    {
        int totalWeight = 0;
        foreach (var w in weights.Values) totalWeight += w;
        int randomValue = Random.Range(0, totalWeight);
        int currentSum = 0;
        foreach (var kvp in weights)
        {
            currentSum += kvp.Value;
            if (randomValue < currentSum) return kvp.Key;
        }
        return weights.Keys.GetEnumerator().Current;
    }
}