using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

public class EventManager : MonoBehaviour
{
    [Header("UI References (Назначаются в префабе!)")]
    public GameObject eventPanel;
    public TextMeshProUGUI titleText;      // Заголовок окна (название события)
    public TextMeshProUGUI dialogText; // Основной текст диалога
    public TextMeshProUGUI dialogTextMG; //Текст диалога если есть контейнер на экране
    public TextMeshProUGUI speakerText;    // Имя говорящего (отдельное поле)
    public Button nextButton;
    public Transform choicesContainer;
    public Button choiceButtonPrefab;
    public Button closeButton;
    public Transform minigameContainer;

    public Image backgroundImage;
    public Image frameImage;
    public Image portraitImage;

    private GameObject statsPanel;

    [Header("Settings")]
    public string resourcesFolder = "Events/Minigames";

    public GameObject rewardItemPrefab;
    public GameObject collectionCardPreviewPrefab;  // Перетащите сюда RewardItemUI.prefab
    public Transform rewardsContainer;  // Контейнер для наград (Horizontal Layout Group)

    private List<EventStep> currentSteps;
    private int currentStepIndex = 0;
    private StoryNode sourceNode;
    private bool isProcessingMinigame = false;
    private Action onCustomFinishCallback;

    // Хранит название текущего события для отображения в заголовке
    private string currentEventTitle = "";

    public void Init(StoryNode node)
    {
        if (eventPanel != null) eventPanel.SetActive(false);

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

        StartEventInternal(node);
    }

    public void InitWithData(StoryEventData data, Action onFinishCallback)
    {
        if (data == null || data.steps == null || data.steps.Count == 0)
        {
            Debug.LogError("Переданы пустые данные события в InitWithData!");
            if (onFinishCallback != null) onFinishCallback();
            Destroy(gameObject);
            return;
        }

        if (eventPanel != null) eventPanel.SetActive(false);

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

        sourceNode = null;
        currentSteps = data.steps;
        currentStepIndex = 0;
        isProcessingMinigame = false;

        // Сохраняем название события из конфига
        currentEventTitle = !string.IsNullOrEmpty(data.title) ? data.title : "Событие";

        onCustomFinishCallback = onFinishCallback;

        Time.timeScale = 0f;
        if (eventPanel != null) eventPanel.SetActive(true);

        ProcessStep();

        Debug.Log($"[EventManager] Инициализировано кастомное событие: {currentEventTitle}");
    }

    private void StartEventInternal(StoryNode node)
    {
        StoryEventData eventData = StoryContentLoader.GetAnyEventById(node.eventId);
        if (eventData == null || eventData.steps == null || eventData.steps.Count == 0)
        {
            Debug.LogError($"Событие {node.eventId} не найдено!");
            Destroy(gameObject);
            return;
        }

        sourceNode = node;
        currentSteps = eventData.steps;
        currentStepIndex = 0;
        isProcessingMinigame = false;

        // Сохраняем название события из конфига
        currentEventTitle = !string.IsNullOrEmpty(eventData.title) ? eventData.title : "Событие";

        Time.timeScale = 0f;
        if (eventPanel != null) eventPanel.SetActive(true);
        SetStatsVisible(false);

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

        ClearRewards();
        ClearChoices();
        foreach (Transform child in minigameContainer) Destroy(child.gameObject);

        // Фон
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
                backgroundImage.gameObject.SetActive(false);
            }
        }

        // Рамка
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

        // Портрет
        if (portraitImage != null)
        {
            if (!string.IsNullOrEmpty(step.portraitSpriteName))
            {
                Sprite portrait = Resources.Load<Sprite>("Sprites/Portraits/" + step.portraitSpriteName);
                if (portrait != null)
                {
                    portraitImage.sprite = portrait;
                    portraitImage.gameObject.SetActive(true);
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

        if (dialogText != null)
        {
            dialogText.gameObject.SetActive(true);
            dialogText.text = step.text;
        }

        // Очищаем контейнеры
        foreach (Transform child in minigameContainer) Destroy(child.gameObject);
        ClearChoices();

        // === ЗАПОЛНЕНИЕ ТЕКСТОВ ===

        // 1. ЗАГОЛОВОК ОКНА: Название события (например, "Таинственный торговец")
        if (titleText != null)
        {
            titleText.text = currentEventTitle;
        }

        // 2. ИМЯ ГОВОРЯЩЕГО: Отдельно в поле speakerText (например, "Торговец")
        if (speakerText != null)
        {
            if (!string.IsNullOrEmpty(step.speaker))
            {
                speakerText.text = step.speaker;
                speakerText.gameObject.SetActive(true);
            }
            else
            {
                speakerText.text = "";
                speakerText.gameObject.SetActive(false);
            }
        }

        bool hasMinigame = !string.IsNullOrEmpty(step.minigamePrefabName);

        if (hasMinigame)
        {
            // --- ВАРИАНТ А: ЕСТЬ МИНИ-ИГРА ---

            // 1. Основной текст (на месте контейнера) очищаем или скрываем, чтобы не мешал
            if (dialogText != null)
            {
                dialogText.text = "";
                // Можно полностью скрыть объект, если он занимает место
                // dialogText.gameObject.SetActive(false); 
            }

            // 2. Текст инструкции/описания пишем в НИЖНИЙ блок
            if (dialogTextMG != null)
            {
                dialogTextMG.text = step.text;
                dialogTextMG.gameObject.SetActive(true);
            }
        }
        else
        {
            // --- ВАРИАНТ Б: НЕТ МИНИ-ИГРЫ ---

            // 1. Нижний текст скрываем
            if (dialogTextMG != null)
            {
                dialogTextMG.gameObject.SetActive(false);
            }

            // 2. Основной текст пишем в стандартное место (где обычно стоит контейнер)
            if (dialogText != null)
            {
                dialogText.text = step.text;
                dialogText.gameObject.SetActive(true);
            }
        }

        if (nextButton != null)
        {
            TextMeshProUGUI btnTextComponent = nextButton.GetComponentInChildren<TextMeshProUGUI>();

            if (!string.IsNullOrEmpty(step.buttonText))
            {
                if (btnTextComponent) btnTextComponent.text = step.buttonText;
            }
            else
            {
                if (btnTextComponent) btnTextComponent.text = "Далее";
            }
        }

        switch (step.type)
        {
            case StepType.DIALOG:
                HandleDialogStep(step);
                break;
            case StepType.REWARD:
                // === ИЗМЕНЕНИЕ: Запускаем визуальное отображение ===
                ShowVisualRewards(step);

                // Если есть выбор после наград, показываем
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
                break;
            case StepType.CARD_CHOICE:
            case StepType.PUZZLE_SLIDER:
            case StepType.RPS:
            case StepType.CATCH_SPIRIT: // Поддержка новой мини-игры
                StartMinigame(step);
                break;
            default:
                Debug.LogError($"[ERROR] Неизвестный тип шага: {step.type}! Пропускаем шаг.");
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
        if (reward == null) return;

        int goldChange = reward.gold;
        int maxHpChange = 0;
        if (reward.bonusTypes != null)
        {
            foreach (var bonus in reward.bonusTypes)
            {
                if (bonus == "MAX_HP_UP") maxHpChange += 1;
                if (bonus == "MAX_HP_DOWN") maxHpChange -= 1;
            }
        }

        List<string> cardsToAdd = reward.cardNames;
        List<string> cardsToRemove = new List<string>();

        if (reward.bonusTypes != null && reward.bonusTypes.Contains("REMOVE_RANDOM_CARD"))
        {
            PlayerProgressionManager.Instance.RemoveRandomCard();
        }

        PlayerProgressionManager.Instance.ApplyReward(
            goldChange: goldChange,
            healAmount: reward.healAmount,
            maxHpChange: maxHpChange,
            cardsToAdd: cardsToAdd,
            cardsToRemove: cardsToRemove
        );
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
            GameObject gameObj = Instantiate(prefab, minigameContainer);

            RectTransform rt = gameObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localPosition = Vector3.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
                rt.SetAsLastSibling();
            }

            EventInteractiveBase miniGameScript = gameObj.GetComponent<EventInteractiveBase>();

            if (miniGameScript != null)
            {
                miniGameScript.OnFinished += OnMinigameFinished;
                miniGameScript.Init(step.parameters);
            }
            else
            {
                Debug.LogError($"[ERROR] Нет скрипта мини-игры на префабе!");
            }
        }
        else
        {
            Debug.LogError("[ERROR] MinigameContainer не назначен!");
        }
    }

    private void OnMinigameFinished(bool isVictory)
    {
        Debug.Log($"[EVENT] Мини-игра завершена. Победа: {isVictory}. Текущий индекс: {currentStepIndex}");
        isProcessingMinigame = false;

        // ЛОГИКА ПЕРЕХОДОВ ДЛЯ МИНИ-ИГР:
        // Индекс N: Сама мини-игра
        // Индекс N+1: Шаг ПОБЕДЫ (Reward/Dialog)
        // Индекс N+2: Шаг ПОРАЖЕНИЯ (Reward/Dialog)

        if (isVictory)
        {
            // При победе идем на следующий шаг по порядку (N+1)
            Debug.Log("[EVENT] Переход к шагу ПОБЕДЫ (следующий по порядку)");
            NextStep();
        }
        else
        {
            // При поражении пропускаем шаг победы и идем сразу к шагу поражения (N+2)
            int loseStepIndex = currentStepIndex + 2;

            if (loseStepIndex < currentSteps.Count)
            {
                Debug.Log($"[EVENT] Переход к шагу ПОРАЖЕНИЯ (индекс {loseStepIndex})");
                GoToStep(loseStepIndex);
            }
            else
            {
                Debug.Log("[EVENT] Шага поражения нет, завершаем событие.");
                FinishEvent(true);
            }
        }
    }

    public void NextStep()
    {
        if (currentStepIndex >= 0 && currentStepIndex < currentSteps.Count)
        {
            EventStep currentStep = currentSteps[currentStepIndex];

            if (currentStep.isFinal)
            {
                Debug.Log("[EVENT] Достигнут финальный шаг. Завершение события.");
                FinishEvent(true);
                return;
            }
        }

        currentStepIndex++;

        if (currentStepIndex >= currentSteps.Count)
        {
            FinishEvent(true);
        }
        else
        {
            ProcessStep();
        }
    }

    private void FinishEvent(bool success)
    {
        if (eventPanel != null) eventPanel.SetActive(false);

        Time.timeScale = 1f;

        SetStatsVisible(true);

        if (onCustomFinishCallback != null)
        {
            Debug.Log("[EventManager] Вызов кастомного коллбэка завершения (событие главы).");
            onCustomFinishCallback.Invoke();
            onCustomFinishCallback = null;
            Destroy(gameObject);
            return;
        }

        if (success && sourceNode != null)
        {
            StoryMapManager.Instance.MarkNodeVisited(sourceNode.chapterIndex, sourceNode.nodeId);
            StoryMapVisual visual = FindObjectOfType<StoryMapVisual>();
            if (visual != null) visual.RefreshAllNodeVisuals();
        }

        TempData.CurrentEvent = null;
        Destroy(gameObject);
    }

    private void CloseEvent()
    {
        FinishEvent(false);
    }

    private void FindStatsPanel()
    {
        if (statsPanel == null)
        {
            // Ищем объект по имени "StatsPanel" или по тегу, если вы его назначили
            statsPanel = GameObject.Find("StatsPanel");
            // Или если у вас есть тег "Stats":
            // statsPanel = GameObject.FindGameObjectWithTag("Stats");

            if (statsPanel == null)
            {
                Debug.LogWarning("[EventManager] StatsPanel не найден! Убедитесь, что объект называется 'StatsPanel' в иерархии.");
            }
        }
    }

    private void SetStatsVisible(bool visible)
    {
        FindStatsPanel();
        if (statsPanel != null)
        {
            statsPanel.SetActive(visible);
        }
    }

    private void ShowVisualRewards(EventStep step)
    {
        if (step == null || step.reward == null || rewardsContainer == null) return;

        RewardData reward = step.reward;

        // === ВЫВОД ТЕКСТА ШАГА В НИЖНИЙ БЛОК (dialogTextMG) ===
        if (dialogTextMG != null)
        {
            dialogTextMG.text = !string.IsNullOrEmpty(step.text) ? step.text : "";
            dialogTextMG.gameObject.SetActive(true);
        }

        // Скрываем обычный текст диалога, чтобы не дублировался
        if (dialogText != null)
        {
            dialogText.text = "";
            dialogText.gameObject.SetActive(false);
        }

        // Очищаем старые награды
        foreach (Transform child in rewardsContainer) Destroy(child.gameObject);

        // 1. Отображаем HP
        if (reward.healAmount != 0)
        {
            GameObject hpObj = Instantiate(rewardItemPrefab, rewardsContainer);
            RewardItemUI hpUI = hpObj.GetComponent<RewardItemUI>();

            if (reward.healAmount > 0)
            {
                hpUI.SetHP(reward.healAmount, "+");
            }
            else
            {
                hpUI.SetHP(Mathf.Abs(reward.healAmount), "-");
            }
        }

        // 2. Отображаем Gold
        if (reward.gold != 0)
        {
            GameObject goldObj = Instantiate(rewardItemPrefab, rewardsContainer);
            RewardItemUI goldUI = goldObj.GetComponent<RewardItemUI>();

            if (reward.gold > 0) goldUI.SetGold(reward.gold, "+");
            else goldUI.SetGold(Mathf.Abs(reward.gold), "-");
        }

        // 3. Отображаем Карты
        if (reward.cardNames != null)
        {
            foreach (string cardName in reward.cardNames)
            {
                Card cardData = CardM.AllCards.Find(c => c.Name == cardName);
                if (cardData != null)
                {
                    GameObject cardObj = Instantiate(collectionCardPreviewPrefab, rewardsContainer);
                    CollectionCardPreview preview = cardObj.GetComponent<CollectionCardPreview>();

                    if (preview != null)
                    {
                        preview.Init(cardData, true);

                        Button btn = cardObj.GetComponent<Button>();
                        if (btn != null) btn.interactable = false;

                        RectTransform rt = cardObj.GetComponent<RectTransform>();
                        if (rt != null) rt.localScale = Vector3.one * 1.0f;
                    }
                }
            }
        }

        // Применяем логику наград
        ApplyRewards(reward);
    }

    private void ClearRewards()
    {
        if (rewardsContainer != null)
        {
            foreach (Transform child in rewardsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // Возвращаем обычный текстовый диалог обратно
        if (dialogText != null) dialogText.gameObject.SetActive(true);

        // Скрываем нижний блок — его состояние будет заново определено в ProcessStep
        if (dialogTextMG != null)
        {
            dialogTextMG.text = "";
            dialogTextMG.gameObject.SetActive(false);
        }
    }
}