using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class StoryContentLoader
{
    public static List<EnemyData> AllEnemies { get; private set; }
    public static List<StoryEventData> AllEvents { get; private set; }
    public static List<ChapterConfig> AllChapters { get; private set; }
    public static List<EnemyPool> AllEnemyPools { get; private set; }
    public static List<EventPool> AllEventPools { get; private set; }
    public static List<MapTheme> AllThemes { get; private set; }

    public static bool IsLoaded { get; private set; } = false;

    public static void LoadAllContent()
    {
        if (IsLoaded)
        {
            Debug.Log("Контент уже загружен");
            return;
        }

        Debug.Log("Загрузка контента сюжета...");

        var enemiesWrapper = LoadJSONFile<EnemyDataWrapper>("enemies.json");
        AllEnemies = enemiesWrapper?.enemies ?? new List<EnemyData>();
        Debug.Log($"Загружено врагов: {AllEnemies.Count}");

        var eventsWrapper = LoadJSONFile<StoryEventDataWrapper>("events.json");
        AllEvents = eventsWrapper?.events ?? new List<StoryEventData>();
        Debug.Log($"Загружено событий: {AllEvents.Count}");

        var chaptersWrapper = LoadJSONFile<ChapterConfigWrapper>("chapters.json");
        AllChapters = chaptersWrapper?.chapters ?? new List<ChapterConfig>();
        Debug.Log($"Загружено глав: {AllChapters.Count}");

        var enemyPoolsWrapper = LoadJSONFile<EnemyPoolWrapper>("enemyPools.json");
        AllEnemyPools = enemyPoolsWrapper?.pools ?? new List<EnemyPool>();

        var eventPoolsWrapper = LoadJSONFile<EventPoolWrapper>("eventPools.json");
        AllEventPools = eventPoolsWrapper?.pools ?? new List<EventPool>();

        var themesWrapper = LoadJSONFile<MapThemeWrapper>("mapThemes.json");
        AllThemes = themesWrapper?.themes ?? new List<MapTheme>();

        IsLoaded = true;
        Debug.Log("Контент сюжета загружен!");
    }

    public static EnemyData GetEnemyById(string enemyId)
    {
        if (!IsLoaded) LoadAllContent();
        return AllEnemies?.Find(e => e.id == enemyId);
    }

    public static StoryEventData GetEventById(string eventId)
    {
        if (!IsLoaded) LoadAllContent();
        return AllEvents?.Find(e => e.id == eventId);
    }

    public static EnemyData GetRandomBoss(string bossPoolId)
    {
        if (!IsLoaded) LoadAllContent();

        var pool = AllEnemyPools?.Find(p => p.poolId == bossPoolId);
        if (pool == null || pool.bossIds.Count == 0)
        {
            Debug.LogError($"Пул боссов не найден: {bossPoolId}");
            return null;
        }

        string bossId = pool.bossIds[Random.Range(0, pool.bossIds.Count)];
        return GetEnemyById(bossId);
    }

    public static EnemyData GetRandomEnemy(string enemyPoolId)
    {
        if (!IsLoaded) LoadAllContent();

        var pool = AllEnemyPools?.Find(p => p.poolId == enemyPoolId);
        if (pool == null || pool.enemyIds.Count == 0)
        {
            Debug.LogError($"Пул врагов не найден: {enemyPoolId}");
            return null;
        }

        string enemyId = pool.enemyIds[Random.Range(0, pool.enemyIds.Count)];
        return GetEnemyById(enemyId);
    }

    public static StoryEventData GetRandomEvent(string eventPoolId)
    {
        if (!IsLoaded) LoadAllContent();

        var pool = AllEventPools?.Find(p => p.poolId == eventPoolId);
        if (pool == null || pool.eventIds.Count == 0)
        {
            Debug.LogError($"Пул событий не найден: {eventPoolId}");
            return null;
        }

        string eventId = pool.eventIds[Random.Range(0, pool.eventIds.Count)];
        return GetEventById(eventId);
    }

    public static MapTheme GetThemeById(string themeId)
    {
        if (!IsLoaded) LoadAllContent();
        return AllThemes?.Find(t => t.themeId == themeId);
    }

    static T LoadJSONFile<T>(string fileName) where T : class
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"Файл не найден: {path}");
            return null;
        }

        string json = File.ReadAllText(path);
        T data = JsonUtility.FromJson<T>(json);

        if (data == null)
        {
            Debug.LogError($"Не удалось распарсить {fileName}");
        }

        return data;
    }

    public static void ClearContent()
    {
        AllEnemies = null;
        AllEvents = null;
        AllChapters = null;
        AllEnemyPools = null;
        AllEventPools = null;
        AllThemes = null;
        IsLoaded = false;
    }
}

[System.Serializable]
public class EnemyDataWrapper
{
    public List<EnemyData> enemies;
}

[System.Serializable]
public class StoryEventDataWrapper
{
    public List<StoryEventData> events;
}

[System.Serializable]
public class ChapterConfigWrapper
{
    public List<ChapterConfig> chapters;
}

[System.Serializable]
public class EnemyPoolWrapper
{
    public List<EnemyPool> pools;
}

[System.Serializable]
public class EventPoolWrapper
{
    public List<EventPool> pools;
}

[System.Serializable]
public class MapThemeWrapper
{
    public List<MapTheme> themes;
}