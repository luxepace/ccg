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
        public float minNodeDistance = 25f;
        public int maxBranches = 3;
        public float connectionChance = 0.7f;
        public int minNodesPerChapter = 10;
        public int maxNodesPerChapter = 15;
    }

    public static List<StoryChapter> GenerateFullMap(GenerationSettings settings = null)
    {
        if (settings == null)
            settings = new GenerationSettings();

        if (!StoryContentLoader.IsLoaded)
        {
            Debug.LogError("Контент не загружен!");
            return null;
        }

        List<StoryChapter> chapters = new List<StoryChapter>();
        int globalNodeId = 0;
        int previousBossNodeId = -1;

        for (int i = 0; i < StoryContentLoader.AllChapters.Count; i++)
        {
            ChapterConfig config = StoryContentLoader.AllChapters[i];

            StoryChapter chapter = GenerateChapter(config, settings, ref globalNodeId, previousBossNodeId);

            if (previousBossNodeId >= 0 && chapter.nodes.Count > 0)
            {
                StoryNode startNode = chapter.nodes.Find(n => n.type == StoryNodeType.START);
                if (startNode != null)
                {
                    startNode.previousNodeIds.Add(previousBossNodeId);
                }
            }

            StoryNode bossNode = chapter.nodes.Find(n => n.type == StoryNodeType.BOSS);
            if (bossNode != null)
            {
                previousBossNodeId = bossNode.nodeId;
            }

            chapters.Add(chapter);
        }

        return chapters;
    }

    static StoryChapter GenerateChapter(ChapterConfig config, GenerationSettings settings,
                                        ref int globalNodeId, int previousBossNodeId)
    {
        StoryChapter chapter = new StoryChapter(config.chapterIndex, config.chapterName);
        chapter.enemyPoolId = config.enemyPoolId;
        chapter.eventPoolId = config.eventPoolId;
        chapter.bossPoolId = config.bossPoolId;

        int nodesInChapter = Random.Range(config.minNodes, config.maxNodes + 1);

        StoryNode startNode = new StoryNode(
            globalNodeId++,
            config.chapterIndex,
            0,
            StoryNodeType.START,
            new Vector3(100f, 0f, settings.startZ)
        );
        chapter.nodes.Add(startNode);

        List<StoryNode> previousLayerNodes = new List<StoryNode> { startNode };

        for (int layer = 1; layer < nodesInChapter; layer++)
        {
            float baseZ = settings.startZ + (layer * settings.nodeSpacingZ);
            List<StoryNode> currentLayerNodes = new List<StoryNode>();

            int nodesInLayer = (layer == nodesInChapter - 1) ? 1 : Random.Range(1, settings.maxBranches + 1);

            for (int i = 0; i < nodesInLayer; i++)
            {
                Vector3 position;
                int attempts = 0;
                bool validPosition = false;

                do
                {
                    float mapWidth = 200f;
                    float margin = mapWidth * 0.1f;
                    float xPos = Random.Range(margin, mapWidth - margin);
                    float zPos = baseZ + Random.Range(-10f, 10f);
                    zPos = Mathf.Clamp(zPos, settings.startZ + (layer * 10f), settings.startZ + (layer * 14f));

                    position = new Vector3(xPos, 0f, zPos);
                    validPosition = true;

                    foreach (var existingNode in currentLayerNodes)
                    {
                        float distance = Vector3.Distance(position, existingNode.position);
                        if (distance < settings.minNodeDistance)
                        {
                            validPosition = false;
                            break;
                        }
                    }

                    if (!validPosition)
                    {
                        foreach (var existingNode in previousLayerNodes)
                        {
                            float distance = Vector3.Distance(position, existingNode.position);
                            if (distance < settings.minNodeDistance * 0.7f)
                            {
                                validPosition = false;
                                break;
                            }
                        }
                    }

                    attempts++;
                } while (!validPosition && attempts < 20);

                if (!validPosition && attempts >= 20)
                {
                    float spread = (nodesInLayer - 1) * settings.minNodeDistance;
                    float startX = 100f - spread / 2f;
                    float xPos = startX + i * settings.minNodeDistance;
                    xPos = Mathf.Clamp(xPos, 30f, 170f);
                    float zPos = baseZ;
                    position = new Vector3(xPos, 0f, zPos);
                }

                StoryNodeType nodeType = DetermineNodeType(config, layer == nodesInChapter - 1);

                StoryNode node = new StoryNode(
                    globalNodeId++,
                    config.chapterIndex,
                    layer,
                    nodeType,
                    position
                );

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

        return chapter;
    }

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

    static void FillNodeData(StoryNode node, ChapterConfig config, bool forceBoss = false)
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
        }
    }

    static void ConnectNodes(StoryNode node, List<StoryNode> previousLayer, float chance)
    {
        if (previousLayer.Count == 0)
            return;

        var sortedPrevious = new List<StoryNode>(previousLayer);
        sortedPrevious.Sort((a, b) => a.position.x.CompareTo(b.position.x));

        bool connected = false;

        foreach (var prevNode in sortedPrevious)
        {
            if (Random.value < chance)
            {
                prevNode.connectedNodeIds.Add(node.nodeId);
                node.previousNodeIds.Add(prevNode.nodeId);
                connected = true;
            }
        }

        if (!connected && sortedPrevious.Count > 0)
        {
            StoryNode nearestPrev = sortedPrevious[0];
            float minDistance = Mathf.Abs(node.position.x - sortedPrevious[0].position.x);

            for (int i = 1; i < sortedPrevious.Count; i++)
            {
                float distance = Mathf.Abs(node.position.x - sortedPrevious[i].position.x);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestPrev = sortedPrevious[i];
                }
            }

            nearestPrev.connectedNodeIds.Add(node.nodeId);
            node.previousNodeIds.Add(nearestPrev.nodeId);
        }
    }
}