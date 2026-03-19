using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public static class TempData
{
    public static EnemyData CurrentEnemy;

    // ИСПРАВЛЕНИЕ ЗДЕСЬ: Убрали "Game." перед StoryEventData
    public static StoryEventData CurrentEvent;

    public static StoryNode CurrentNode;
    public static bool IsStoryMode { get; set; } = false;
    public static bool BossDefeated { get; set; } = false;
    // Поле для наград с карты
    public static List<string> RewardCards = new List<string>();
}