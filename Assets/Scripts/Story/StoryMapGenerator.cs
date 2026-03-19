using System.Collections.Generic;
using UnityEngine;

public static class StoryMapGenerator
{
    [System.Serializable]
    public class GenerationSettings
    {
        public float nodeSpacingY = 3f;
        public float nodeSpacingX = 4f;
        public float nodeSpacingZ = 12f;
        public float startZ = 30f;
        public float minNodeDistance = 40f;
        public int maxBranches = 3;
        public float connectionChance = 0.7f;
        public int minNodesPerChapter = 10;
        public int maxNodesPerChapter = 15;

        // Зоны для новой логики
        public float startMinZ = 10f;
        public float startMaxZ = 25f;
        public float mapMinX = 10f;
        public float mapMaxX = 190f;
        public float mapMaxZ = 190f;

        public float nodeHeightOffset = 3.0f; // Отступ ноды над поверхностью бумаги
    }

    public static List<StoryChapter> GenerateFullMap(GenerationSettings settings = null)
    {
        if (settings == null) settings = new GenerationSettings();

        if (!StoryContentLoader.IsLoaded)
        {
            Debug.LogError("Контент не загружен!");
            return null;
        }

        // Проверка готовности ландшафта (бумаги)
        if (MapGenerator3D.Instance == null || !MapGenerator3D.Instance.IsTerrainReady())
        {
            Debug.LogError("[StoryMapGen] ОШИБКА: Ландшафт не готов!");
            return null;
        }

        List<StoryChapter> allChapters = new List<StoryChapter>();
        int globalNodeId = 0;
        int previousBossNodeId = -1;

        foreach (var config in StoryContentLoader.AllChapters)
        {
            Debug.Log($"[Gen] Генерация главы: {config.chapterName}");

            StoryChapter chapter = GenerateChapter(config, settings, ref globalNodeId, previousBossNodeId);

            if (chapter != null)
            {
                chapter.themeId = config.themeId;

                if (previousBossNodeId >= 0 && chapter.nodes.Count > 0)
                {
                    StoryNode startNode = chapter.nodes.Find(n => n.type == StoryNodeType.START);
                    if (startNode != null) { }
                }

                StoryNode bossNode = chapter.nodes.Find(n => n.type == StoryNodeType.BOSS);
                if (bossNode != null)
                {
                    previousBossNodeId = bossNode.nodeId;
                }

                allChapters.Add(chapter);
            }
        }

        Debug.Log($"[Gen] Всего сгенерировано глав: {allChapters.Count}");
        return allChapters;
    }

    static StoryChapter GenerateChapter(ChapterConfig config, GenerationSettings settings,
                                    ref int globalNodeId, int previousBossNodeId)
    {
        StoryChapter chapter = new StoryChapter(config.chapterIndex, config.chapterName);
        chapter.enemyPoolId = config.enemyPoolId;
        chapter.eventPoolId = config.eventPoolId;
        chapter.bossPoolId = config.bossPoolId;
        chapter.themeId = config.themeId;

        Debug.Log($"[Gen] Тема: {config.themeId}. Генерация на карте.");

        int nodesInChapter = Random.Range(config.minNodes, config.maxNodes + 1);

        // --- СТАРТОВАЯ ТОЧКА ---
        float startZPos = Random.Range(settings.startMinZ, settings.startMaxZ);
        float startXPos = Random.Range(settings.mapMinX, settings.mapMaxX);

        float startH = MapGenerator3D.GetTerrainHeightAt(startXPos, startZPos);
        Vector3 startPos = new Vector3(startXPos, startH + settings.nodeHeightOffset, startZPos);

        StoryNode startNode = new StoryNode(globalNodeId++, config.chapterIndex, 0, StoryNodeType.START, startPos);
        chapter.nodes.Add(startNode);

        List<StoryNode> previousLayerNodes = new List<StoryNode> { startNode };

        // === ИСПРАВЛЕНИЕ 1: УВЕЛИЧИВАЕМ РАССТОЯНИЕ МЕЖДУ СЛОЯМИ ===
        // Было: 0.75f длины карты. Стало: 0.85f, чтобы слои были дальше друг от друга.
        float maxRouteLength = (settings.mapMaxZ - settings.startMaxZ) * 0.85f;

        // Делим на (nodesInChapter - 1), чтобы равномерно распределить слои
        float zStep = maxRouteLength / Mathf.Max(1, nodesInChapter - 2);

        for (int layer = 1; layer < nodesInChapter; layer++)
        {
            float baseZ = settings.startMaxZ + ((layer - 1) * zStep);

            List<StoryNode> currentLayerNodes = new List<StoryNode>();

            int nodesInLayer = (layer == nodesInChapter - 1) ? 1 : Random.Range(1, settings.maxBranches + 1);

            for (int i = 0; i < nodesInLayer; i++)
            {
                Vector3 position = Vector3.zero;
                bool validPosition = false;
                int attempts = 0;

                do
                {
                    float mapWidth = 200f;
                    float margin = mapWidth * 0.15f;
                    float xPos = Random.Range(margin, mapWidth - margin);

                    float zPos = baseZ + Random.Range(-5f, 5f);
                    zPos = Mathf.Clamp(zPos, settings.startMinZ, settings.mapMaxZ - 10f);

                    float groundHeight = MapGenerator3D.GetTerrainHeightAt(xPos, zPos);
                    position = new Vector3(xPos, groundHeight + settings.nodeHeightOffset, zPos);
                    validPosition = true;

                    // === ИСПРАВЛЕНИЕ: ПРОВЕРКА ДИСТАНЦИИ ТОЛЬКО ПО X и Z ===
                    // Создаем вектор только с горизонтальными координатами для сравнения
                    Vector2 newPos2D = new Vector2(position.x, position.z);

                    if (validPosition)
                    {
                        foreach (var existingNode in currentLayerNodes)
                        {
                            Vector2 existingPos2D = new Vector2(existingNode.position.x, existingNode.position.z);
                            // Увеличил минимальную дистанцию до 30 (было 25)
                            if (Vector2.Distance(newPos2D, existingPos2D) < 30f)
                            {
                                validPosition = false;
                                break;
                            }
                        }
                    }

                    if (validPosition)
                    {
                        foreach (var existingNode in previousLayerNodes)
                        {
                            Vector2 existingPos2D = new Vector2(existingNode.position.x, existingNode.position.z);
                            // Увеличил коэффициент до 0.8 (было 0.6)
                            if (Vector2.Distance(newPos2D, existingPos2D) < 30f * 0.8f)
                            {
                                validPosition = false;
                                break;
                            }
                        }
                    }

                    attempts++;
                } while (!validPosition && attempts < 60);

                if (!validPosition)
                {
                    Debug.LogWarning($"[Gen] Не найдено место для слоя {layer}. Ставим аварийно.");
                    // Аварийная позиция тоже должна быть подальше
                    float safeX = 100f + (i * 40f);
                    if (safeX > 180f) safeX = 20f + (i * 10f);

                    float safeZ = baseZ;
                    float h = MapGenerator3D.GetTerrainHeightAt(safeX, safeZ);
                    position = new Vector3(safeX, h + settings.nodeHeightOffset, safeZ);
                }

                StoryNodeType nodeType = DetermineNodeType(config, layer == nodesInChapter - 1);

                StoryNode node = new StoryNode(globalNodeId++, config.chapterIndex, layer, nodeType, position);

                FillNodeData(node, config);
                ConnectNodes(node, previousLayerNodes, settings.connectionChance);

                chapter.nodes.Add(node);
                currentLayerNodes.Add(node);

                if (nodeType == StoryNodeType.BOSS)
                {
                    chapter.bossNodeId = node.nodeId;
                    EnemyData boss = StoryContentLoader.GetEnemyById(node.enemyId);
                    if (boss != null && !string.IsNullOrEmpty(boss.themeId))
                    {
                        chapter.themeId = boss.themeId;
                    }
                }
            }

            currentLayerNodes.Sort((a, b) => a.position.x.CompareTo(b.position.x));
            EnsureAllNodesHaveConnection(previousLayerNodes, currentLayerNodes);

            previousLayerNodes = currentLayerNodes;
        }

        // ... (остальной код метода без изменений: Финишная точка и возврат) ...

        // --- ФИНИШНАЯ ТОЧКА ---
        StoryNode bossNode = chapter.nodes.Find(n => n.type == StoryNodeType.BOSS);

        if (bossNode != null)
        {
            float endZ = bossNode.position.z + settings.nodeSpacingZ;
            if (endZ > settings.mapMaxZ - 5f) endZ = settings.mapMaxZ - 5f;

            float endX = bossNode.position.x;
            float h = MapGenerator3D.GetTerrainHeightAt(endX, endZ);
            Vector3 endPos = new Vector3(endX, h + settings.nodeHeightOffset, endZ);

            StoryNode endNode = new StoryNode(globalNodeId++, config.chapterIndex, bossNode.layer + 1, StoryNodeType.CHAPTER_END, endPos);

            bossNode.connectedNodeIds.Add(endNode.nodeId);
            endNode.previousNodeIds.Add(bossNode.nodeId);
            endNode.eventId = $"chapter_end_{config.chapterIndex}";

            chapter.nodes.Add(endNode);
        }

        return chapter;
    }

    // Функция FindDryPositionStrict УДАЛЕНА, так как вода больше не используется

    static void EnsureAllNodesHaveConnection(List<StoryNode> previousLayer, List<StoryNode> currentLayer)
    {
        if (currentLayer.Count == 0 || previousLayer.Count == 0)
            return;

        var sortedPrevious = new List<StoryNode>(previousLayer);
        sortedPrevious.Sort((a, b) => a.position.x.CompareTo(b.position.x));

        var sortedCurrent = new List<StoryNode>(currentLayer);
        sortedCurrent.Sort((a, b) => a.position.x.CompareTo(b.position.x));

        foreach (var prevNode in sortedPrevious)
        {
            bool hasConnectionToCurrentLayer = false;

            foreach (int connectedId in prevNode.connectedNodeIds)
            {
                if (currentLayer.Exists(n => n.nodeId == connectedId))
                {
                    hasConnectionToCurrentLayer = true;
                    break;
                }
            }

            if (!hasConnectionToCurrentLayer)
            {
                StoryNode nearestNextNode = sortedCurrent[0];
                float minDistance = Mathf.Abs(prevNode.position.x - sortedCurrent[0].position.x);

                for (int i = 1; i < sortedCurrent.Count; i++)
                {
                    float distance = Mathf.Abs(prevNode.position.x - sortedCurrent[i].position.x);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestNextNode = sortedCurrent[i];
                    }
                }

                prevNode.connectedNodeIds.Add(nearestNextNode.nodeId);
                nearestNextNode.previousNodeIds.Add(prevNode.nodeId);
            }
        }
    }

    static StoryNodeType DetermineNodeType(ChapterConfig config, bool isLastNode)
    {
        if (isLastNode)
            return StoryNodeType.BOSS;

        float rand = Random.value;

        if (rand < config.enemyNodeChance)
            return StoryNodeType.ENEMY;
        else if (rand < config.enemyNodeChance + config.eventNodeChance)
            return StoryNodeType.EVENT;
        else if (rand < config.enemyNodeChance + config.eventNodeChance + config.restNodeChance)
            return StoryNodeType.REST;

        return StoryNodeType.ENEMY;
    }

    static void FillNodeData(StoryNode node, ChapterConfig config)
    {
        switch (node.type)
        {
            case StoryNodeType.ENEMY:
                EnemyData enemy = StoryContentLoader.GetRandomEnemy(config.enemyPoolId);
                if (enemy != null)
                {
                    node.enemyId = enemy.id;
                }
                break;

            case StoryNodeType.BOSS:
                EnemyData boss = StoryContentLoader.GetRandomBoss(config.bossPoolId);
                if (boss != null)
                {
                    node.enemyId = boss.id;
                }
                break;

            case StoryNodeType.EVENT:
                StoryEventData eventData = StoryContentLoader.GetRandomEvent(config.eventPoolId);
                if (eventData != null)
                {
                    node.eventId = eventData.id;
                }
                break;

            case StoryNodeType.REST:
                node.eventId = "rest_event";
                break;
        }
    }

    static void ConnectNodes(StoryNode node, List<StoryNode> previousLayer, float chance)
    {
        if (previousLayer.Count == 0) return;

        var sortedPrevious = new List<StoryNode>(previousLayer);
        sortedPrevious.Sort((a, b) => a.position.x.CompareTo(b.position.x));

        bool connected = false;

        // === НОВАЯ ЛОГИКА: Предпочитаем "прямые" соединения ===

        // Сначала пробуем соединиться с ближайшим по X узлом (это даст вертикальные/прямые линии)
        StoryNode nearestByX = sortedPrevious[0];
        float minDiffX = Mathf.Abs(node.position.x - sortedPrevious[0].position.x);

        for (int i = 1; i < sortedPrevious.Count; i++)
        {
            float diffX = Mathf.Abs(node.position.x - sortedPrevious[i].position.x);
            if (diffX < minDiffX)
            {
                minDiffX = diffX;
                nearestByX = sortedPrevious[i];
            }
        }

        // С вероятностью 80% соединяемся с ближайшим по X (чтобы линии были прямыми)
        if (Random.value < 0.8f)
        {
            nearestByX.connectedNodeIds.Add(node.nodeId);
            node.previousNodeIds.Add(nearestByX.nodeId);
            connected = true;
        }
        else
        {
            // Иначе используем старую случайную логику для разнообразия
            foreach (var prevNode in sortedPrevious)
            {
                if (Random.value < chance)
                {
                    prevNode.connectedNodeIds.Add(node.nodeId);
                    node.previousNodeIds.Add(prevNode.nodeId);
                    connected = true;
                }
            }
        }

        // Если ничего не соединилось, принудительно соединяем с ближайшим по X
        if (!connected && sortedPrevious.Count > 0)
        {
            nearestByX.connectedNodeIds.Add(node.nodeId);
            node.previousNodeIds.Add(nearestByX.nodeId);
        }
    }
}