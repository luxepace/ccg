using UnityEngine;
using System.Collections.Generic;

// В файле TempData.cs

public static class TempData
{
    // ... старые поля ...
    public static EnemyData CurrentEnemy;
    public static StoryEventData CurrentEvent;
    public static StoryNode CurrentNode;
    public static bool IsStoryMode { get; set; } = false;
    public static bool BossDefeated { get; set; } = false;
    public static List<string> RewardCards = new List<string>();

    // === НОВЫЕ ПОЛЯ ДЛЯ БЫСТРОЙ ИГРЫ ===

    // Режим ИИ (Balanced, Attack, Defend)
    public static AISourceData.AIMode FastGameAiMode { get; set; } = AISourceData.AIMode.Balanced;

    // Флаг, что мы находимся в процессе настройки быстрой игры (чтобы отличать от обычного боя)
    public static bool IsSettingUpFastGame { get; set; } = false;

    // Временная колода для быстрой игры (список имен карт)
    public static List<string> FastGameDeck { get; set; } = new List<string>();
}