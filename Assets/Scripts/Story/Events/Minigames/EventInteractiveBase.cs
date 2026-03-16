using System;
using UnityEngine;
using System.Collections.Generic;

public abstract class EventInteractiveBase : MonoBehaviour
{
    // Теперь событие передает bool (true = победа, false = поражение)
    public event Action<bool> OnFinished;

    public abstract void Init(List<ParamPair> parameters);

    // Метод для завершения игры с результатом
    protected void FinishMinigame(bool isVictory)
    {
        OnFinished?.Invoke(isVictory);
    }
}