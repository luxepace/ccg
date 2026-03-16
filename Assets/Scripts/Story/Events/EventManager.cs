using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    // Ссылка на префаб самого себя (настраивается в StoryMapVisual или GameManager)
    // Но лучше передавать имя префаба или иметь статическую ссылку.
    // Для простоты: мы будем создавать этот префаб через StoryMapVisual.

    [Header("UI References (Назначаются в префабе!)")]
    public GameObject eventPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI dialogText;
    public TextMeshProUGUI speakerText;

    public Button nextButton;
    public Transform choicesContainer;
    public Button choiceButtonPrefab;   // Префаб кнопки выбора
    public Button closeButton;
    public Transform minigameContainer;

    public Image backgroundImage;
    public Image frameImage;
    public Image portraitImage;

    [Header("Settings")]
    public string resourcesFolder = "Events/Minigames";

    private List<EventStep> currentSteps;
    private int currentStepIndex = 0;
    private StoryNode sourceNode;
    private bool isProcessingMinigame = false;

    // Метод инициализации, который вызывается сразу после создания префаба
    public void Init(StoryNode node)
    {
        // Скрываем панель при старте (на случай если в префабе забыли выключить)
        if (eventPanel != null) eventPanel.SetActive(false);

        // Настраиваем кнопки
        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(NextStep);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseEvent);
        }

        // Запускаем событие
        StartEventInternal(node);
    }

    private void StartEventInternal(StoryNode node)
    {
        StoryEventData eventData = StoryContentLoader.GetAnyEventById(node.eventId);
        if (eventData == null || eventData.steps == null || eventData.steps.Count == 0)
        {
            Debug.LogError($"Событие {node.eventId} не найдено!");
            Destroy(gameObject); // Уничтожаем префаб, если ошибка
            return;
        }

        sourceNode = node;
        currentSteps = eventData.steps;
        currentStepIndex = 0;
        isProcessingMinigame = false;

        Time.timeScale = 0f;
        if (eventPanel != null) eventPanel.SetActive(true); // Показываем окно

        ProcessStep();
    }

    private void ProcessStep()
    {
        if (currentStepIndex >= currentSteps.Count)
        {
            FinishEvent(true);
            return;
        }

        EventStep step = currentSteps[currentStepIndex];

        Debug.Log($"[DEBUG] Обработка шага {currentStepIndex}. Тип: {step.type}");
        // === НОВАЯ ЛОГИКА: СМЕНА ГРАФИКИ ===

        // 1. Фон
        if (backgroundImage != null)
        {
            if (!string.IsNullOrEmpty(step.backgroundSpriteName))
            {
                Sprite bg = Resources.Load<Sprite>("Sprites/Backgrounds/" + step.backgroundSpriteName);
                if (bg != null)
                {
                    backgroundImage.sprite = bg;
                    backgroundImage.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"Фон '{step.backgroundSpriteName}' не найден!");
                    backgroundImage.gameObject.SetActive(false);
                }
            }
            else
            {
                // Если имя не задано, можно скрыть фон или оставить предыдущий
                // backgroundImage.gameObject.SetActive(false); 
            }
        }

        // 2. Рамка
        if (frameImage != null)
        {
            if (!string.IsNullOrEmpty(step.frameSpriteName))
            {
                Sprite frame = Resources.Load<Sprite>("Sprites/Frames/" + step.frameSpriteName);
                if (frame != null)
                {
                    frameImage.sprite = frame;
                    frameImage.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"Рамка '{step.frameSpriteName}' не найдена!");
                    frameImage.gameObject.SetActive(false);
                }
            }
            else
            {
                frameImage.gameObject.SetActive(false);
            }
        }

        // 3. Портрет
        if (portraitImage != null)
        {
            if (!string.IsNullOrEmpty(step.portraitSpriteName))
            {
                Sprite portrait = Resources.Load<Sprite>("Sprites/Portraits/" + step.portraitSpriteName);
                if (portrait != null)
                {
                    portraitImage.sprite = portrait;
                    portraitImage.gameObject.SetActive(true);

                    // Опционально: можно зеркалить портрет, если персонаж смотрит влево/вправо
                    // Vector3 scale = portraitImage.transform.localScale;
                    // scale.x = Mathf.Abs(scale.x) * (lookLeft ? -1 : 1);
                    // portraitImage.transform.localScale = scale;
                }
                else
                {
                    Debug.LogWarning($"Портрет '{step.portraitSpriteName}' не найден!");
                    portraitImage.gameObject.SetActive(false);
                }
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
            }
        }
        // ======================================
        // Очищаем контейнеры
        foreach (Transform child in minigameContainer) Destroy(child.gameObject);
        ClearChoices();

        // Заполняем тексты
        if (titleText) titleText.text = step.speaker ?? "";
        if (dialogText) dialogText.text = step.text;

        // === НОВАЯ ЛОГИКА ДЛЯ ТЕКСТА КНОПКИ ===
        if (nextButton != null)
        {
            TextMeshProUGUI btnTextComponent = nextButton.GetComponentInChildren<TextMeshProUGUI>();

            if (!string.IsNullOrEmpty(step.buttonText))
            {
                // Если в JSON задан свой текст - используем его
                if (btnTextComponent) btnTextComponent.text = step.buttonText;
            }
            else
            {
                // Иначе ставим текст по умолчанию
                if (btnTextComponent) btnTextComponent.text = "Далее";
            }
        }
        // SWITCH ПО ТИПУ
        switch (step.type)
        {
            case StepType.DIALOG:
                Debug.Log("[DEBUG] Запуск HandleDialogStep");
                HandleDialogStep(step);
                break;
            case StepType.REWARD:
                Debug.Log("[DEBUG] Запуск HandleRewardStep");
                HandleRewardStep(step);
                break;
            case StepType.CARD_CHOICE:
            case StepType.PUZZLE_SLIDER:
            case StepType.RPS:
                Debug.Log($"[DEBUG] Запуск StartMinigame для типа {step.type}");
                StartMinigame(step);
                break;
            default:
                Debug.LogError($"[ERROR] Неизвестный тип шага: {step.type}! Пропускаем шаг.");
                // ВОТ ЗДЕСЬ МОЖЕТ БЫТЬ ПРОБЛЕМА: Если тип не распознан, мы сразу жмем NextStep
                NextStep(); 
                break;
        }
    }

    void HandleDialogStep(EventStep step)
    {
        if (step.choices != null && step.choices.Count > 0)
        {
            ShowChoices(step.choices);
            if (nextButton) nextButton.gameObject.SetActive(false);
        }
        else
        {
            if (nextButton) nextButton.gameObject.SetActive(true);
        }
        if (closeButton) closeButton.gameObject.SetActive(false);
    }

    void HandleRewardStep(EventStep step)
    {
        string rewardText = "Получены награды:\n";
        if (step.reward.gold > 0) rewardText += $"Золото: {step.reward.gold}\n";
        if (step.reward.healAmount > 0) rewardText += $"Лечение: {step.reward.healAmount}\n";
        if (step.reward.cardNames != null)
        {
            foreach (var cardName in step.reward.cardNames) rewardText += $"Карта: {cardName}\n";
        }

        if (dialogText) dialogText.text = rewardText;
        ApplyRewards(step.reward);

        if (step.choices != null && step.choices.Count > 0)
        {
            ShowChoices(step.choices);
            if (nextButton) nextButton.gameObject.SetActive(false);
        }
        else
        {
            if (nextButton) nextButton.gameObject.SetActive(true);
        }
        if (closeButton) closeButton.gameObject.SetActive(false);
    }

    void ClearChoices()
    {
        if (choicesContainer == null) return;
        foreach (Transform child in choicesContainer) Destroy(child.gameObject);
        choicesContainer.gameObject.SetActive(false);
    }

    void ShowChoices(List<StepChoice> choices)
    {
        if (choicesContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogError("Не назначены choicesContainer или choiceButtonPrefab!");
            return;
        }

        choicesContainer.gameObject.SetActive(true);

        foreach (var choice in choices)
        {
            Button newBtn = Instantiate(choiceButtonPrefab, choicesContainer);
            newBtn.gameObject.SetActive(true);

            TextMeshProUGUI btnText = newBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = choice.choiceText;

            int targetIndex = choice.nextStepIndex;
            newBtn.onClick.RemoveAllListeners();
            newBtn.onClick.AddListener(() => GoToStep(targetIndex));
        }
    }

    void GoToStep(int index)
    {
        if (isProcessingMinigame) return;
        currentStepIndex = index;
        ProcessStep();
    }

    private void ApplyRewards(RewardData reward)
    {
        if (reward.healAmount > 0 && GameManager.Instance != null && GameManager.Instance.CurrentGame != null)
        {
            GameManager.Instance.CurrentGame.Player.HP += reward.healAmount;
            if (UIController.Instance != null) UIController.Instance.UpdateHPAndMana();
        }

        if (reward.cardNames != null && reward.cardNames.Count > 0)
        {
            if (TempData.RewardCards == null) TempData.RewardCards = new List<string>();
            TempData.RewardCards.AddRange(reward.cardNames);
        }
    }

    private void StartMinigame(EventStep step)
    {
        Debug.Log($"[MINIGAME] Запуск типа: {step.type}. Префаб: {step.minigamePrefabName}");

        isProcessingMinigame = true;
        if (nextButton) nextButton.gameObject.SetActive(false);
        ClearChoices();
        if (closeButton) closeButton.gameObject.SetActive(false);

        if (dialogText) dialogText.text = step.text;

        string prefabPath = $"{resourcesFolder}/{step.minigamePrefabName}";
        GameObject prefab = Resources.Load<GameObject>(prefabPath);

        if (prefab == null)
        {
            Debug.LogError($"[ERROR] Префаб мини-игры не найден: {prefabPath}");
            isProcessingMinigame = false;
            NextStep();
            return;
        }

        if (minigameContainer != null)
        {
            // Создаем игру внутри контейнера
            GameObject gameObj = Instantiate(prefab, minigameContainer);

            // === ВОТ ЭТОТ БЛОК РЕШАЕТ ПРОБЛЕМУ ===
            RectTransform rt = gameObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                // 1. Заставляем игру занять 100% места родителя (MinigameContainer)
                rt.anchorMin = Vector2.zero;      // Левый нижний угол (0,0)
                rt.anchorMax = Vector2.one;       // Правый верхний угол (1,1)

                // 2. Обнуляем отступы (это аналог ручного ввода Left/Right/Top/Bottom = 0)
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                // 3. Сбрасываем позицию и масштаб для чистоты
                rt.localPosition = Vector3.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;

                // 4. Убеждаемся, что игра поверх других элементов контейнера
                rt.SetAsLastSibling();
            }
            // =====================================

            EventInteractiveBase miniGameScript = gameObj.GetComponent<EventInteractiveBase>();

            if (miniGameScript != null)
            {
                miniGameScript.OnFinished += OnMinigameFinished;
                miniGameScript.Init(step.parameters);
            }
            else
            {
                Debug.LogError($"[ERROR] Нет скрипта мини-игры на префабе!");
                miniGameScript.OnFinished += OnMinigameFinished;
            }
        }
        else
        {
            Debug.LogError("[ERROR] MinigameContainer не назначен!");
        }
    }

    private void OnMinigameFinished(bool isVictory)
    {
        Debug.Log($"[EVENT] Мини-игра завершена. Победа: {isVictory}");
        isProcessingMinigame = false;

        if (isVictory)
        {
            // ПОБЕДА: Просто идем к следующему шагу (это Шаг 2 - Награда).
            // А оттуда игрок сам нажмет кнопку и перейдет на Шаг 4.
            NextStep();
        }
        else
        {
            // ПОРАЖЕНИЕ: Мы должны ПРОПУСТИТЬ шаг с наградой (Шаг 2) 
            // и перейти сразу к Шагу 3 (Прощание с проигравшим).

            Debug.Log("[EVENT] Игрок проиграл. Пропускаем награду и идем на шаг прощания.");

            // Текущий индекс указывает на игру (Шаг 1).
            // Следующий шаг (Шаг 2) - это награда.
            // Нам нужен Шаг 3. Значит, прыгаем через один.
            int skipIndex = currentStepIndex + 2;

            if (skipIndex < currentSteps.Count)
            {
                GoToStep(skipIndex);
            }
            else
            {
                // Если шагов больше нет, завершаем событие
                FinishEvent(true);
            }
        }
    }

    public void NextStep()
    {
        // Проверяем текущий шаг: если он помечен как финальный, то завершаем событие
        if (currentStepIndex >= 0 && currentStepIndex < currentSteps.Count)
        {
            EventStep currentStep = currentSteps[currentStepIndex];

            // Если текущий шаг имеет флаг isFinal, то дальше идти некуда - завершаем
            if (currentStep.isFinal)
            {
                Debug.Log("[EVENT] Достигнут финальный шаг. Завершение события.");
                FinishEvent(true);
                return;
            }
        }

        // Обычный переход к следующему шагу
        currentStepIndex++;

        if (currentStepIndex >= currentSteps.Count)
        {
            // Если шаги кончились, тоже завершаем
            FinishEvent(true);
        }
        else
        {
            ProcessStep();
        }
    }

    private void FinishEvent(bool success)
    {
        // Скрываем панель
        if (eventPanel != null) eventPanel.SetActive(false);

        Time.timeScale = 1f;

        if (success && sourceNode != null)
        {
            StoryMapManager.Instance.MarkNodeVisited(sourceNode.chapterIndex, sourceNode.nodeId);
            StoryMapVisual visual = FindObjectOfType<StoryMapVisual>();
            if (visual != null) visual.RefreshAllNodeVisuals();
        }

        TempData.CurrentEvent = null;

        // УНИЧТОЖАЕМ ПРЕФАБ ПОСЛЕ ЗАВЕРШЕНИЯ СОБЫТИЯ
        Destroy(gameObject);
    }

    private void CloseEvent()
    {
        FinishEvent(false);
    }
}