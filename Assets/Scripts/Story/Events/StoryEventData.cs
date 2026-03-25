using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// ДАННЫЕ СОБЫТИЙ (ТОЛЬКО ЗДЕСЬ)
// Все классы ниже объявлены в глобальном пространстве имен, чтобы быть доступными везде.
// =============================================================================

[Serializable]
public class StoryEventData
{
    public string id;
    public string title; // Заголовок события (например, "Таинственный торговец")
    public List<EventStep> steps = new List<EventStep>();
}

[Serializable]
public class EventStep
{
    public StepType type;
    public string text; // Основной текст диалога или описания
    public string speaker; // Имя говорящего
    public string backgroundSpriteName;

    public string frameSpriteName;

    public string portraitSpriteName;
    public string buttonText;
    // Для шага REWARD
    public RewardData reward;

    // Для шагов мини-игр (имя префаба в Resources/Events/Minigames/)
    public string minigamePrefabName;

    public bool isFinal = false;
    // Параметры для мини-игр или логики
    public List<ParamPair> parameters = new List<ParamPair>();

    // Варианты выбора для игрока
    // Если список пуст -> показываем кнопку "Далее" (линейный переход)
    // Если есть элементы -> скрываем "Далее" и показываем кнопки выбора
    public List<StepChoice> choices = new List<StepChoice>();
}

[Serializable]
public class StepChoice
{
    public string choiceText; // Текст на кнопке (например, "Купить", "Уйти")
    public int nextStepIndex; // Индекс следующего шага в списке steps
}

[Serializable]
public class ParamPair
{
    public string key;
    public string value;
}

[Serializable]
public enum StepType
{
    DIALOG,
    REWARD,
    PUZZLE_SLIDER,
    CARD_CHOICE,
    RPS,
    CARD_SHOP
}

[Serializable]
public class RewardData
{
    public int gold;
    public int healAmount;
    public List<string> cardNames = new List<string>();
    public List<string> bonusTypes = new List<string>();
}

[Serializable]
public class EventPool
{
    public string poolId;
    public List<string> eventIds;
}