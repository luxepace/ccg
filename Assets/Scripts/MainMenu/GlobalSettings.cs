using UnityEngine;

public static class GlobalSettings
{
    public static Sprite CurrentCardBack { get; private set; } = null;

    public static void SetCardBack(Sprite sprite)
    {
        CurrentCardBack = sprite;
    }
}