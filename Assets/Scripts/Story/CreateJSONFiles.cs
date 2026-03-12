using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class CreateJSONFiles : MonoBehaviour
{
    [ContextMenu("Create Clean JSON Files")]
    public void CreateFiles()
    {
        string path = Application.streamingAssetsPath;

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        CreateEnemiesJSON(path);
        CreateEventsJSON(path);
        CreateChaptersJSON(path);
        CreateEnemyPoolsJSON(path);
        CreateEventPoolsJSON(path);
        CreateMapThemesJSON(path);

        Debug.Log("Все JSON файлы созданы успешно!");
    }

    void CreateEnemiesJSON(string path)
    {
        List<EnemyData> enemies = new List<EnemyData>
        {
            new EnemyData
            {
                id = "bandit_1",
                enemyName = "Бандит",
                avatarPath = "",
                enemyHP = 25,
                enemyMaxHP = 25,
                enemyDeck = new List<string>(),
                condition = EnemyConditionType.NONE,
                rewardGold = 15,
                rewardCardId = "",
                isBoss = false,
                themeId = ""
            },
            new EnemyData
            {
                id = "boss_river",
                enemyName = "Бог Реки",
                avatarPath = "",
                enemyHP = 50,
                enemyMaxHP = 50,
                enemyDeck = new List<string>(),
                condition = EnemyConditionType.START_WITH_LESS_HP,
                rewardGold = 100,
                rewardCardId = "",
                isBoss = true,
                themeId = "river"
            }
        };

        EnemyDataWrapper wrapper = new EnemyDataWrapper { enemies = enemies };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(Path.Combine(path, "enemies.json"), json);
        Debug.Log("Создан enemies.json");
    }

    void CreateEventsJSON(string path)
    {
        List<StoryEventData> events = new List<StoryEventData>
        {
            new StoryEventData
            {
                id = "merchant",
                eventTitle = "Таинственный торговец",
                eventDescription = "Ты встретил торговца...",
                type = EventType.GET_RANDOM_CARD,
                rewardCardId = "",
                rewardGold = 0,
                healAmount = 0
            }
        };

        StoryEventDataWrapper wrapper = new StoryEventDataWrapper { events = events };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(Path.Combine(path, "events.json"), json);
        Debug.Log("Создан events.json");
    }

    void CreateChaptersJSON(string path)
    {
        List<ChapterConfig> chapters = new List<ChapterConfig>
        {
            new ChapterConfig
            {
                chapterIndex = 0,
                chapterName = "Глава 1: Начало пути",
                minNodes = 10,
                maxNodes = 12,
                enemyPoolId = "chapter_1_enemies",
                eventPoolId = "chapter_1_events",
                bossPoolId = "chapter_1_enemies",
                enemyNodeChance = 0.6f,
                eventNodeChance = 0.3f,
                restNodeChance = 0.1f
            }
        };

        ChapterConfigWrapper wrapper = new ChapterConfigWrapper { chapters = chapters };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(Path.Combine(path, "chapters.json"), json);
        Debug.Log("Создан chapters.json");
    }

    void CreateEnemyPoolsJSON(string path)
    {
        List<EnemyPool> pools = new List<EnemyPool>
        {
            new EnemyPool
            {
                poolId = "chapter_1_enemies",
                enemyIds = new List<string> { "bandit_1" },
                bossIds = new List<string> { "boss_river" }
            }
        };

        EnemyPoolWrapper wrapper = new EnemyPoolWrapper { pools = pools };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(Path.Combine(path, "enemyPools.json"), json);
        Debug.Log("Создан enemyPools.json");
    }

    void CreateEventPoolsJSON(string path)
    {
        List<EventPool> pools = new List<EventPool>
        {
            new EventPool
            {
                poolId = "chapter_1_events",
                eventIds = new List<string> { "merchant" }
            }
        };

        EventPoolWrapper wrapper = new EventPoolWrapper { pools = pools };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(Path.Combine(path, "eventPools.json"), json);
        Debug.Log("Создан eventPools.json");
    }

    void CreateMapThemesJSON(string path)
    {
        List<MapTheme> themes = new List<MapTheme>
        {
            new MapTheme
            {
                themeId = "river",
                themeName = "Речная долина",
                terrainTexture = "",
                skyboxMaterial = "",
                ambientColor = new Color(0.6f, 0.7f, 0.8f, 1f),
                decorationPrefabs = new string[0]
            }
        };

        MapThemeWrapper wrapper = new MapThemeWrapper { themes = themes };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(Path.Combine(path, "mapThemes.json"), json);
        Debug.Log("Создан mapThemes.json");
    }
}