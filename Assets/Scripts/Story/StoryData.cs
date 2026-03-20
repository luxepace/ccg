using System.Collections.Generic;
using UnityEngine;

public enum StoryNodeType
{
    START,
    ENEMY,
    EVENT,
    BOSS,
    REST,
    CHAPTER_END
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
    public bool isActive;

    public StoryChapter(int index, string name)
    {
        chapterIndex = index;
        chapterName = name;
        nodes = new List<StoryNode>();
        isCompleted = false;
        isUnlocked = (index == 0);
        isActive = (index == 0);
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
public class ChapterConfig
{
    public int chapterIndex;
    public string chapterName;
    public int minNodes;
    public int maxNodes;

    // ОСТАВЛЯЕМ ТОЛЬКО ОДНО ПОЛЕ ДЛЯ ПУЛА
    public string enemyPoolId;
    public string eventPoolId;

    // bossPoolId УДАЛЕН

    public float enemyNodeChance;
    public float eventNodeChance;
    public float restNodeChance;
    public string themeId;
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

    // Основные параметры шума
    public float noiseScale = 50f;
    public float heightMultiplier = 25f;

    // === НОВЫЕ ПОЛЯ (Добавь их обязательно!) ===
    public int octaves = 4;
    public float persistence = 0.5f;

    // Параметры формы рельефа
    public float peakThreshold = 0.5f;
    public float peakSharpness = 2.5f;
    public int smoothingPasses = 1;
    public float waterLevel = 5f;
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

    // НОВЫЕ ПОЛЯ: Храним конфигурацию прямо в сохранении
    public string chapterName;      // Чтобы отображать правильное название (например, "Густая чаща")
    public string enemyPoolId;      // Чтобы знать, каких врагов спавнить
    public string eventPoolId;      // Для событий
    public string bossPoolId;       // Для босса (теперь это то же самое, что enemyPoolId, но оставим для совместимости)
    public string themeId;          // Тема ландшафта

    // Статистика и структура
    public List<NodeSaveData> nodes;
    public bool isCompleted;
    public bool isUnlocked;
    public bool isActive;
}

[System.Serializable]
public class NodeSaveData
{
    public int nodeId;
    public int layer;
    public StoryNodeType type;

    // Сохраняем только X и Z. Y будет вычислен при загрузке.
    public float posX;
    public float posZ;

    // Оставляем поле position для совместимости, но при сохранении игнорируем Y,
    // а при загрузке восстанавливаем Y через Terrain.
    [System.NonSerialized] public float tempY;

    public bool isVisited;
    public bool isUnlocked;
    public bool isSkipped;

    // Можно вообще убрать Vector3 position и использовать отдельные поля, 
    // но чтобы не ломать остальной код парсинга, проще оставить Vector3, 
    // но понимать, что Y в файле - это "мусор", который будет перезаписан.
    public Vector3 position;

    public string enemyId;
    public string eventId;
    public List<int> connectedNodeIds;
}

[System.Serializable]
public class DecorationConfig
{
    public string prefabId;
    public string prefabPath;

    // Для 3D объектов
    public float minScale;
    public float maxScale;
    public float density;
    public bool isClustered;
    public int clusterCount;
    public int objectsPerCluster;
    public float clusterRadius;
    public float heightOffset;
    public float minHeight;
    public float maxHeight;

    // Для рисованных объектов (вода, горы)
    public Color paintColor;

    // === ДОБАВЬТЕ ЭТУ СТРОКУ ===
    public Color secondaryColor;
    // ===========================

    public float minSize;
    public float maxSize;
    public float length; // Для рек
    public int count;
    public string shape; // "circle", "blob", "line", "mountain"

    public List<string> allowedThemes;
    public bool avoidPaths;
    public bool avoidNodes;
    public bool isPainted;
}

[System.Serializable]
public class DecorationConfigWrapper
{
    public List<DecorationConfig> decorations;
}

// Класс складки, доступный всем скриптам
[System.Serializable]
public class FoldLine
{
    public float startX, startZ;
    public float endX, endZ;
    public int type; // 1 или -1
    public float width;
    public float strength;
}

// Классы для сохранения (нужны для JsonUtility)
[System.Serializable]
public class FoldSaveData
{
    public List<FoldLineSave> folds;
}

[System.Serializable]
public class FoldLineSave
{
    public float startX, startZ, endX, endZ;
    public int type;
    public float width, strength;
}


[System.Serializable]
public class PaintedDecorationConfig : DecorationConfigBase
{
    public Color paintColor;
    public float minSize;
    public float maxSize;
    public float length; // Для рек
    public int count;
    public string shape; // "circle" или "line"
    public bool avoidNodes;
    public bool isPainted = true;
}

// Базовый класс для общих полей (если у вас его нет, создайте или используйте существующий MapDecoration)
[System.Serializable]
public class DecorationConfigBase
{
    public string prefabId;
    public List<string> allowedThemes;
    // Остальные поля могут быть опциональными для разных типов
}

[System.Serializable]
public class PaintedWaterObject
{
    public string shape;        // "blob", "circle", "line"
    public Vector2 center;      // Центр объекта
    public Vector2 endPoint;    // Для линий (конец)
    public float radius;        // Радиус для blob/circle или половина ширины для line
    public float width;         // Полная ширина для линии
    public Color color;

    // Критически важные поля для шума (форма озера)
    public float noiseOffsetX;
    public float noiseOffsetY;
}

[System.Serializable]
public class MapDecoration
{
    public string prefabId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}

[System.Serializable]
public class MapDecorationListWrapper
{
    public List<MapDecoration> items;
}

[System.Serializable]
public class PathData
{
    public Vector2 start;
    public Vector2 end;
    public PathData(Vector2 s, Vector2 e) { start = s; end = e; }
}
