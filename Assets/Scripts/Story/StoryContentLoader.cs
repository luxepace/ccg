using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class StoryContentLoader
{
    // Основные данные
    public static List<EnemyData> AllEnemies { get; private set; }
    public static List<StoryEventData> AllEvents { get; private set; }

    // НОВЫЙ СПИСОК: События привалов
    public static List<StoryEventData> AllRestEvents { get; private set; }

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

        // 1. Загрузка врагов
        var enemiesWrapper = LoadJSONFile<EnemyDataWrapper>("enemies.json");
        AllEnemies = enemiesWrapper?.enemies ?? new List<EnemyData>();
        Debug.Log($"Загружено врагов: {AllEnemies.Count}");

        // 2. Загрузка основных событий (квесты, торговцы)
        var eventsWrapper = LoadJSONFile<StoryEventDataWrapper>("events.json");
        AllEvents = eventsWrapper?.events ?? new List<StoryEventData>();
        Debug.Log($"Загружено событий: {AllEvents.Count}");

        // 3. НОВОЕ: Загрузка событий привалов
        var restEventsWrapper = LoadJSONFile<RestEventDataWrapper>("rest_events.json");
        AllRestEvents = restEventsWrapper?.events ?? new List<StoryEventData>();
        Debug.Log($"Загружено событий привалов: {AllRestEvents.Count}");

        // 4. Загрузка глав
        var chaptersWrapper = LoadJSONFile<ChapterConfigWrapper>("chapters.json");
        AllChapters = chaptersWrapper?.chapters ?? new List<ChapterConfig>();
        Debug.Log($"Загружено глав: {AllChapters.Count}");

        // 5. Загрузка пулов
        var enemyPoolsWrapper = LoadJSONFile<EnemyPoolWrapper>("enemyPools.json");
        AllEnemyPools = enemyPoolsWrapper?.pools ?? new List<EnemyPool>();

        var eventPoolsWrapper = LoadJSONFile<EventPoolWrapper>("eventPools.json");
        AllEventPools = eventPoolsWrapper?.pools ?? new List<EventPool>();

        var themesWrapper = LoadJSONFile<MapThemeWrapper>("mapThemes.json");
        AllThemes = themesWrapper?.themes ?? new List<MapTheme>();

        IsLoaded = true;
        Debug.Log("Загрузка контента завершена!");
    }

    // --- Методы поиска ---

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

    // НОВЫЙ МЕТОД: Поиск события привала по ID
    // В файле StoryContentLoader.cs
    public static StoryEventData GetAnyEventById(string eventId)
    {
        if (!IsLoaded) LoadAllContent();

        // 1. Ищем в основных событиях
        StoryEventData eventData = AllEvents?.Find(e => e.id == eventId);
        if (eventData != null) return eventData;

        // 2. Если не нашли, ищем в привалах
        eventData = AllRestEvents?.Find(e => e.id == eventId);
        if (eventData != null) return eventData;

        // 3. Если совсем не нашли
        Debug.LogError($"[StoryContentLoader] Событие '{eventId}' не найдено ни в основных событиях, ни в привалах!");
        return null;
    }

    public static StoryEventData GetRestEventById(string restEventId)
    {
        if (!IsLoaded) LoadAllContent();

        if (AllRestEvents == null || AllRestEvents.Count == 0)
        {
            Debug.LogError("[StoryContentLoader] Список AllRestEvents пуст! Проверь загрузку rest_events.json.");
            return null;
        }

        StoryEventData eventData = AllRestEvents.Find(e => e.id == restEventId);

        if (eventData == null)
        {
            Debug.LogError($"[StoryContentLoader] Событие привала '{restEventId}' не найдено в списке из {AllRestEvents.Count} элементов.");
            // Для отладки выведем все доступные ID
            foreach (var ev in AllRestEvents)
            {
                Debug.Log($"Доступный ID привала: {ev.id}");
            }
            return null;
        }

        return eventData;
    }

    public static EnemyData GetRandomBoss(string bossPoolId)
    {
        if (!IsLoaded) LoadAllContent();
        var pool = AllEnemyPools?.Find(p => p.poolId == bossPoolId);
        if (pool == null || pool.bossIds.Count == 0) return null;
        string bossId = pool.bossIds[Random.Range(0, pool.bossIds.Count)];
        return GetEnemyById(bossId);
    }

    public static EnemyData GetRandomEnemy(string enemyPoolId)
    {
        if (!IsLoaded) LoadAllContent();
        var pool = AllEnemyPools?.Find(p => p.poolId == enemyPoolId);
        if (pool == null || pool.enemyIds.Count == 0) return null;
        string enemyId = pool.enemyIds[Random.Range(0, pool.enemyIds.Count)];
        return GetEnemyById(enemyId);
    }

    public static StoryEventData GetRandomEvent(string eventPoolId)
    {
        if (!IsLoaded) LoadAllContent();
        var pool = AllEventPools?.Find(p => p.poolId == eventPoolId);
        if (pool == null || pool.eventIds.Count == 0) return null;
        string eventId = pool.eventIds[Random.Range(0, pool.eventIds.Count)];
        return GetEventById(eventId);
    }

    // НОВЫЙ МЕТОД: Получить случайный привал (если нужно, хотя лучше использовать GetRestEventById после выбора веса)
    public static StoryEventData GetRandomRestEvent()
    {
        if (!IsLoaded) LoadAllContent();
        if (AllRestEvents == null || AllRestEvents.Count == 0) return null;
        return AllRestEvents[Random.Range(0, AllRestEvents.Count)];
    }

    public static MapTheme GetThemeById(string themeId)
    {
        if (!IsLoaded) LoadAllContent();
        return AllThemes?.Find(t => t.themeId == themeId);
    }

    // --- Вспомогательные методы загрузки ---

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
            Debug.LogError($"Не удалось парсить {fileName}");
        }

        return data;
    }

    public static void ClearContent()
    {
        AllEnemies = null;
        AllEvents = null;
        AllRestEvents = null; // Очистка привалов
        AllChapters = null;
        AllEnemyPools = null;
        AllEventPools = null;
        AllThemes = null;
        IsLoaded = false;
    }
}

// --- Классы-обертки для JSON ---

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

// НОВАЯ ОБЕРТКА: Для файла rest_events.json
[System.Serializable]
public class RestEventDataWrapper
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