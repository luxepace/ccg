using System;
using UnityEngine;
using System.Collections.Generic;

public abstract class EventInteractiveBase : MonoBehaviour
{
    public event Action<bool> OnFinished;

    public abstract void Init(List<ParamPair> parameters);

    protected void FinishMinigame(bool isVictory)
    {
        OnFinished?.Invoke(isVictory);
    }
}