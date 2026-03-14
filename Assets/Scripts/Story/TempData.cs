using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public static class TempData
{
    public static EnemyData CurrentEnemy;
    public static StoryEventData CurrentEvent;
    public static StoryNode CurrentNode;
    public static bool IsStoryMode { get; set; } = false;
}