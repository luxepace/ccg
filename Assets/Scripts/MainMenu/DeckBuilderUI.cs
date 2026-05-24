using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DeckBuilderUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform availableCardsContainer;
    public Transform deckCardsContainer;
    public TextMeshProUGUI collectionProgressText;
    public GameObject cardSlotPrefab;

    [Header("Deck Stats Texts (Split)")]
    public TextMeshProUGUI totalDeckText;
    public TextMeshProUGUI level1DeckText;
    public TextMeshProUGUI level2DeckText;
    public TextMeshProUGUI level3DeckText;

    [Header("Buttons")]
    public Button confirmButton; // Кнопка "Сохранить" / "Играть"

    [Header("Settings")]
    public int minDeckSize = 5;

    // === ЛОКАЛЬНЫЕ РАБОЧИЕ СПИСКИ ===
    private List<string> _localDeck;
    private List<string> _localAvailable;

    // Для быстрой игры: единый пул всех карт игрока
    private List<string> _fastGameInventory;

    private static bool isInitializing = false;

    // Свойство для проверки режима
    private bool IsFastGameMode => TempData.IsSettingUpFastGame;

    private void OnEnable()
    {
        if (isInitializing) return;
        isInitializing = true;

        Debug.Log("[DeckBuilder] Панель включена.");

        var mgr = PlayerProgressionManager.Instance;
        if (mgr == null)
        {
            isInitializing = false;
            return;
        }

        // 1. ИНИЦИАЛИЗАЦИЯ ДАННЫХ
        if (IsFastGameMode)
        {
            // === РЕЖИМ БЫСТРОЙ ИГРЫ ===

            // Создаем единый инвентарь из всех доступных игроку карт (Available + StoryDeck)
            _fastGameInventory = new List<string>(mgr.AvailableCards);
            _fastGameInventory.AddRange(mgr.DeckCardNames);

            // Инициализируем локальную колоду (копией из TempData или пустой)
            if (TempData.FastGameDeck != null && TempData.FastGameDeck.Count > 0)
            {
                _localDeck = new List<string>(TempData.FastGameDeck);

                // Вычитаем из инвентаря те карты, которые уже есть в сохраненной колоде,
                // чтобы они не отображались слева дважды.
                foreach (var card in _localDeck)
                {
                    if (_fastGameInventory.Contains(card))
                        _fastGameInventory.Remove(card);
                }
            }
            else
            {
                _localDeck = new List<string>();
            }

            Debug.Log($"[DeckBuilder] Быстрая игра. Инвентарь: {_fastGameInventory.Count}, Колода: {_localDeck.Count}");
        }
        else
        {
            // === СТАНДАРТНЫЙ РЕЖИМ ===
            _localDeck = new List<string>(mgr.DeckCardNames);
            _localAvailable = new List<string>(mgr.AvailableCards);
        }

        // 2. НАСТРОЙКА КНОПКИ ПОДТВЕРЖДЕНИЯ
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            TextMeshProUGUI btnText = confirmButton.GetComponentInChildren<TextMeshProUGUI>();

            if (IsFastGameMode)
            {
                confirmButton.onClick.AddListener(StartFastGameBattle);
                if (btnText) btnText.text = "ИГРАТЬ";
            }
            else
            {
                confirmButton.onClick.AddListener(ConfirmDeck);
                if (btnText) btnText.text = "СОХРАНИТЬ";
            }
        }

        ClearContainers();
        RefreshUI();
        isInitializing = false;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        isInitializing = false;

        // СБРОС ФЛАГА ПРИ ЗАКРЫТИИ ПАНЕЛИ
        TempData.IsSettingUpFastGame = false;

        _localDeck?.Clear();
        _localAvailable?.Clear();
        if (_fastGameInventory != null) _fastGameInventory.Clear();
    }

    public void ConfirmDeck()
    {
        var mgr = PlayerProgressionManager.Instance;
        if (mgr == null) return;

        var limits = mgr.GetDeckLimits();

        if (_localDeck.Count < minDeckSize) { Debug.LogWarning("Колода слишком маленькая!"); return; }
        if (_localDeck.Count > limits.maxTotal) { Debug.LogWarning("Превышен общий лимит!"); return; }

        int l1 = GetCardLevelCount(_localDeck, 1);
        int l2 = GetCardLevelCount(_localDeck, 2);
        int l3 = GetCardLevelCount(_localDeck, 3);

        if (l1 > limits.maxLevel1 || l2 > limits.maxLevel2 || l3 > limits.maxLevel3)
        {
            Debug.LogWarning("Нарушены лимиты по уровням!");
            return;
        }

        // Сохраняем в прогресс
        mgr.DeckCardNames.Clear();
        mgr.DeckCardNames.AddRange(_localDeck);

        mgr.AvailableCards.Clear();
        mgr.AvailableCards.AddRange(_localAvailable);

        mgr.SetDeckFromBuilder(mgr.DeckCardNames);

        Debug.Log($"[DeckBuilder] Сюжетная колода успешно сохранена: {mgr.DeckCardNames.Count} карт");

        MainMenuManager mainMenu = FindObjectOfType<MainMenuManager>();
        if (mainMenu != null)
            mainMenu.CloseDeckBuilder();
        else
        {
            gameObject.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    public void StartFastGameBattle()
    {
        Debug.Log("[DeckBuilder] Режим быстрой игры. Проверка колоды...");

        if (_localDeck.Count < minDeckSize)
        {
            Debug.LogWarning($"[DeckBuilder] Колода слишком маленькая! Минимум: {minDeckSize}");
            return;
        }

        // Сохраняем локальные изменения во временное хранилище TempData
        TempData.FastGameDeck.Clear();
        TempData.FastGameDeck.AddRange(_localDeck);

        Debug.Log($"[DeckBuilder] Колода для быстрой игры подтверждена ({TempData.FastGameDeck.Count} карт). Запуск боя...");

        FastGameSettingsManager fastSettings = FindObjectOfType<FastGameSettingsManager>();

        if (fastSettings != null)
        {
            fastSettings.StartFastBattle();
        }
        else
        {
            Debug.LogError("[DeckBuilder] ОШИБКА: Не найден объект FastGameSettingsManager на сцене!");
        }
    }

    public void CancelDeckBuilding()
    {
        Debug.Log("[DeckBuilder] Отмена изменений.");
        TempData.IsSettingUpFastGame = false;

        MainMenuManager mainMenu = FindObjectOfType<MainMenuManager>();
        if (mainMenu != null)
            mainMenu.CloseDeckBuilder();
        else
        {
            gameObject.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    // В файле DeckBuilderUI.cs

    void RefreshUI()
    {
        ClearContainers();
        if (availableCardsContainer == null || deckCardsContainer == null) return;

        var mgr = PlayerProgressionManager.Instance;
        if (CardM.AllCards.Count == 0) LoadCards();

        var limits = mgr.GetDeckLimits();

        // === СТАТИСТИКА ===
        int total = _localDeck.Count;
        int l1 = GetCardLevelCount(_localDeck, 1);
        int l2 = GetCardLevelCount(_localDeck, 2);
        int l3 = GetCardLevelCount(_localDeck, 3);

        if (totalDeckText != null) totalDeckText.text = $"Колода: {total}/{limits.maxTotal}";
        if (level1DeckText != null) level1DeckText.text = $"1 ур: {l1}/{limits.maxLevel1}";
        if (level2DeckText != null) level2DeckText.text = $"2 ур: {l2}/{limits.maxLevel2}";
        if (level3DeckText != null) level3DeckText.text = $"3 ур: {l3}/{limits.maxLevel3}";

        if (collectionProgressText != null)
        {
            int unlocked = mgr.GetUnlockedCount();
            collectionProgressText.text = $"Открыто карт: {unlocked}/{CardM.AllCards.Count}";
        }

        // === ЛЕВАЯ ПАНЕЛЬ: ДОСТУПНЫЕ КАРТЫ (Одна карта с счетчиком xN) ===

        List<string> sourceList;
        if (IsFastGameMode)
        {
            sourceList = _fastGameInventory;
        }
        else
        {
            sourceList = _localAvailable;
        }

        // Группируем карты по именам и считаем копии
        Dictionary<string, int> leftCounts = new Dictionary<string, int>();
        foreach (var name in sourceList)
        {
            if (leftCounts.ContainsKey(name)) leftCounts[name]++;
            else leftCounts[name] = 1;
        }

        // Сортируем уникальные имена
        List<string> sortedLeftNames = new List<string>(leftCounts.Keys);
        sortedLeftNames.Sort((a, b) => {
            Card cA = CardM.AllCards.Find(x => x.Name == a);
            Card cB = CardM.AllCards.Find(x => x.Name == b);
            if (cA == null || cB == null) return 0;
            return cA.Name.CompareTo(cB.Name);
        });

        // Создаем слоты для левой панели
        foreach (var name in sortedLeftNames)
        {
            Card card = CardM.AllCards.Find(x => x.Name == name);
            if (card != null)
            {
                int count = leftCounts[name];
                CreateCardSlot(card, availableCardsContainer, true, count);
            }
        }

        // === ПРАВАЯ ПАНЕЛЬ: КОЛОДА (Каждая копия - отдельная ячейка) ===

        // Просто создаем список всех карт в колоде (с дубликатами)
        List<Card> deckCardsList = new List<Card>();
        foreach (var name in _localDeck)
        {
            Card c = CardM.AllCards.Find(x => x.Name == name);
            if (c != null) deckCardsList.Add(c);
        }

        // Сортируем их
        deckCardsList.Sort((a, b) => a.Name.CompareTo(b.Name));

        // Создаем по одному слоту на каждую копию
        foreach (var card in deckCardsList)
        {
            CreateCardSlot(card, deckCardsContainer, false, 1); // 1 означает, что счетчик не нужен
        }
    }

    void CreateCardSlot(Card card, Transform container, bool isAvailable, int count)
    {
        if (cardSlotPrefab == null || container == null) return;

        GameObject slot = Instantiate(cardSlotPrefab, container);
        CardSlotUI slotUI = slot.GetComponent<CardSlotUI>();

        if (slotUI != null)
        {
            slotUI.Init(card, isAvailable, this);

            // Если это левая панель (доступные карты), устанавливаем счетчик вручную
            if (isAvailable)
            {
                slotUI.SetCount(count);
            }
            // Для правой панели (колода) убеждаемся, что счетчик скрыт
            else
            {
                slotUI.SetCount(1); // 1 скроет бейджик согласно логике выше
            }
        }
    }

    public void AddCardToDeck(string cardName)
    {
        var mgr = PlayerProgressionManager.Instance;
        var limits = mgr.GetDeckLimits();

        // Проверки лимитов
        if (_localDeck.Count >= limits.maxTotal) return;

        Card card = CardM.AllCards.Find(c => c.Name == cardName);
        if (card != null)
        {
            int level = card.minChapterLevel;
            if (level > 3) level = 3;
            int currentLevelCount = GetCardLevelCount(_localDeck, level);
            int limitForLevel = level == 1 ? limits.maxLevel1 : level == 2 ? limits.maxLevel2 : limits.maxLevel3;
            if (currentLevelCount >= limitForLevel) return;
        }

        // === ЛОГИКА ПЕРЕНОСА ===
        if (IsFastGameMode)
        {
            if (_fastGameInventory.Contains(cardName))
            {
                _fastGameInventory.Remove(cardName); // Убираем одну копию из инвентаря
                _localDeck.Add(cardName);            // Добавляем в колоду
            }
        }
        else
        {
            if (_localAvailable.Contains(cardName))
            {
                _localAvailable.Remove(cardName);
                _localDeck.Add(cardName);
            }
        }

        RefreshUI();
    }

    public void RemoveCardFromDeck(string cardName)
    {
        if (_localDeck.Contains(cardName))
        {
            _localDeck.Remove(cardName);

            if (IsFastGameMode)
            {
                _fastGameInventory.Add(cardName); // Возвращаем в инвентарь
            }
            else
            {
                _localAvailable.Add(cardName);
            }

            RefreshUI();
        }
    }

    void ClearContainers()
    {
        if (availableCardsContainer != null)
            foreach (Transform child in availableCardsContainer) Destroy(child.gameObject);
        if (deckCardsContainer != null)
            foreach (Transform child in deckCardsContainer) Destroy(child.gameObject);
    }

    void LoadCards()
    {
        CardDatabase db = ScriptableObject.CreateInstance<CardDatabase>();
        db.LoadFromJSON("cards.json");
        CardM.AllCards = new List<Card>(db.fieldCards);
        CardM.AllCards.AddRange(db.spells);
    }

    int GetCardLevelCount(List<string> deckNames, int targetLevel)
    {
        int count = 0;
        foreach (var name in deckNames)
        {
            Card card = CardM.AllCards.Find(c => c.Name == name);
            if (card != null)
            {
                int lvl = card.minChapterLevel;
                if (lvl > 3) lvl = 3;
                if (lvl == targetLevel) count++;
            }
        }
        return count;
    }
}