using System.Collections.Generic;
using UnityEngine;

public enum StoryNodeType
{
    START,
    ENEMY,
    EVENT,
    BOSS,
    REST
}

public enum EnemyConditionType
{
    NONE,
    START_WITH_LESS_HP,
    MAX_HAND_SIZE_4,
    REDUCED_MANA,
    ENEMY_BUFF
}

[System.Serializable]
public class EnemyData
{
    public string id;
    public string enemyName;
    public string avatarPath;
    public int enemyHP;
    public int enemyMaxHP;
    public List<string> enemyDeck;
    public EnemyConditionType condition;
    public int rewardGold;
    public string rewardCardId;
    public bool isBoss;
    public string themeId;
    public List<string> quotes;
}

[System.Serializable]
public class StoryEventData
{
    public string id;
    public string eventTitle;
    [TextArea(3, 5)] public string eventDescription;
    public EventType type;
    public string rewardCardId;
    public int rewardGold;
    public int healAmount;
}

public enum EventType
{
    GET_RANDOM_CARD,
    GET_GOLD,
    HEAL_HERO,
    NOTHING,
    CHOOSE_ONE_OF_THREE
}

[System.Serializable]
public class StoryNode
{
    public int nodeId;
    public int chapterIndex;
    public int layer;
    public StoryNodeType type;
    public Vector3 position;
    public List<int> connectedNodeIds;
    public List<int> previousNodeIds;

    public bool isVisited;
    public bool isUnlocked;
    public bool isSkipped;

    public string enemyId;
    public string eventId;
    public GameObject nodeVisual;

    public StoryNode(int id, int chapter, int nodeLayer, StoryNodeType nodeType, Vector3 pos)
    {
        nodeId = id;
        chapterIndex = chapter;
        layer = nodeLayer;
        type = nodeType;
        position = pos;
        connectedNodeIds = new List<int>();
        previousNodeIds = new List<int>();
        isVisited = false;
        isUnlocked = (nodeType == StoryNodeType.START && id == 0);
        isSkipped = false;
    }
}

[System.Serializable]
public class StoryChapter
{
    public int chapterIndex;
    public string chapterName;
    public List<StoryNode> nodes;
    public int startNodeId;
    public int bossNodeId;

    public string bossPoolId;
    public string enemyPoolId;
    public string eventPoolId;
    public string themeId;

    public bool isCompleted;
    public bool isUnlocked;

    public StoryChapter(int index, string name)
    {
        chapterIndex = index;
        chapterName = name;
        nodes = new List<StoryNode>();
        isCompleted = false;
        isUnlocked = (index == 0);
    }
}

[System.Serializable]
public class EnemyPool
{
    public string poolId;
    public List<string> enemyIds;
    public List<string> bossIds;
}

[System.Serializable]
public class EventPool
{
    public string poolId;
    public List<string> eventIds;
}

[System.Serializable]
public class ChapterConfig
{
    public int chapterIndex;
    public string chapterName;
    public int minNodes;
    public int maxNodes;
    public string enemyPoolId;
    public string eventPoolId;
    public string bossPoolId;
    public float enemyNodeChance;
    public float eventNodeChance;
    public float restNodeChance;
}

[System.Serializable]
public class MapTheme
{
    public string themeId;
    public string themeName;
    public string terrainTexture;
    public string skyboxMaterial;
    public Color ambientColor;
    public string[] decorationPrefabs;
}

[System.Serializable]
public class StorySaveData
{
    public int currentChapter;
    public List<int> completedChapters;
    public List<string> collectedCardIds;
    public int playerGold;
    public int playerHP;
    public List<ChapterSaveData> chapters;

    public StorySaveData()
    {
        currentChapter = 0;
        completedChapters = new List<int>();
        collectedCardIds = new List<string>();
        playerGold = 0;
        playerHP = 30;
        chapters = new List<ChapterSaveData>();
    }
}

[System.Serializable]
public class ChapterSaveData
{
    public int chapterIndex;
    public string bossId;
    public string themeId;
    public List<NodeSaveData> nodes;
    public bool isCompleted;
    public bool isUnlocked;
}

[System.Serializable]
public class NodeSaveData
{
    public int nodeId;
    public int layer;
    public StoryNodeType type;
    public bool isVisited;
    public bool isUnlocked;
    public bool isSkipped;
    public Vector3 position;
    public string enemyId;
    public string eventId;
    public List<int> connectedNodeIds;
}