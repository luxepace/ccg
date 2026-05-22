using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Обязательно для Button
using TMPro;

public class DeckBuilderUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform availableCardsContainer;
    public Transform deckCardsContainer;
    public TextMeshProUGUI collectionProgressText;
    public GameObject cardSlotPrefab;

    [Header("Buttons")]
    public Button closeButton; // <-- Явное поле для кнопки закрытия

    [Header("Deck Stats Texts")]
    public TextMeshProUGUI totalDeckText;
    public TextMeshProUGUI level1DeckText;
    public TextMeshProUGUI level2DeckText;
    public TextMeshProUGUI level3DeckText;

    [Header("Settings")]
    public int minDeckSize = 8;

    private static bool isInitializing = false;

    private void OnEnable()
    {
        if (isInitializing) return;
        isInitializing = true;

        Time.timeScale = 0f; // Пауза при открытии

        if (PlayerProgressionManager.Instance == null)
        {
            Debug.LogError("[DeckBuilder] PlayerProgressionManager не найден!");
            CloseDeck();
            return;
        }

        // Программно привязываем кнопку закрытия (надежнее, чем через Inspector)
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseDeck);
        }

        ClearContainers();
        RefreshUI();
        isInitializing = false;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f; // Возвращаем время
        isInitializing = false;
    }

    public void ConfirmDeck()
    {
        var mgr = PlayerProgressionManager.Instance;
        if (mgr == null) return;

        var limits = mgr.GetDeckLimits();

        if (mgr.DeckCardNames.Count < minDeckSize)
        {
            Debug.LogWarning($"[DeckBuilder] Колода слишком маленькая! Минимум: {minDeckSize}");
            return;
        }

        if (mgr.DeckCardNames.Count > limits.maxTotal)
        {
            Debug.LogWarning($"[DeckBuilder] Превышен общий лимит! Максимум: {limits.maxTotal}");
            return;
        }

        int l1 = GetCardLevelCount(mgr.DeckCardNames, 1);
        int l2 = GetCardLevelCount(mgr.DeckCardNames, 2);
        int l3 = GetCardLevelCount(mgr.DeckCardNames, 3);

        if (l1 > limits.maxLevel1 || l2 > limits.maxLevel2 || l3 > limits.maxLevel3)
        {
            Debug.LogWarning($"[DeckBuilder] Нарушены лимиты по уровням!");
            return;
        }

        mgr.SetDeckFromBuilder(mgr.DeckCardNames);
        Debug.Log($"[DeckBuilder] Колода успешно сохранена в память!");

        CloseDeck();
    }

    public void CloseDeck()
    {
        gameObject.SetActive(false); // Вызовет OnDisable и вернет Time.timeScale = 1
    }

    void RefreshUI()
    {
        ClearContainers();
        if (availableCardsContainer == null || deckCardsContainer == null) return;

        var mgr = PlayerProgressionManager.Instance;
        if (CardM.AllCards.Count == 0) LoadCards();

        var limits = mgr.GetDeckLimits();

        int total = mgr.DeckCardNames.Count;
        int l1 = GetCardLevelCount(mgr.DeckCardNames, 1);
        int l2 = GetCardLevelCount(mgr.DeckCardNames, 2);
        int l3 = GetCardLevelCount(mgr.DeckCardNames, 3);

        if (totalDeckText != null) totalDeckText.text = $"Колода: {total}/{limits.maxTotal}";
        if (level1DeckText != null) level1DeckText.text = $"1 ур: {l1}/{limits.maxLevel1}";
        if (level2DeckText != null) level2DeckText.text = $"2 ур: {l2}/{limits.maxLevel2}";
        if (level3DeckText != null) level3DeckText.text = $"3 ур: {l3}/{limits.maxLevel3}";

        if (collectionProgressText != null)
        {
            int unlocked = mgr.GetUnlockedCount();
            collectionProgressText.text = $"Открыто карт: {unlocked}/{CardM.AllCards.Count}";
        }

        Dictionary<string, int> poolCounts = new Dictionary<string, int>();
        foreach (var cardName in mgr.AvailableCards)
        {
            if (poolCounts.ContainsKey(cardName)) poolCounts[cardName]++;
            else poolCounts[cardName] = 1;
        }

        List<Card> sortedAvailable = new List<Card>();
        foreach (var kvp in poolCounts)
        {
            Card c = CardM.AllCards.Find(x => x.Name == kvp.Key);
            if (c != null) sortedAvailable.Add(c);
        }
        sortedAvailable.Sort((a, b) => a.Name.CompareTo(b.Name));

        foreach (var card in sortedAvailable)
            CreateCardSlot(card, availableCardsContainer, true);

        List<Card> sortedDeck = new List<Card>();
        foreach (var cardName in mgr.DeckCardNames)
        {
            Card c = CardM.AllCards.Find(x => x.Name == cardName);
            if (c != null) sortedDeck.Add(c);
        }
        sortedDeck.Sort((a, b) => a.Name.CompareTo(b.Name));

        foreach (var card in sortedDeck)
            CreateCardSlot(card, deckCardsContainer, false);
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

    void CreateCardSlot(Card card, Transform container, bool isAvailable)
    {
        if (cardSlotPrefab == null || container == null) return;
        GameObject slot = Instantiate(cardSlotPrefab, container);
        CardSlotUI slotUI = slot.GetComponent<CardSlotUI>();
        if (slotUI != null) slotUI.Init(card, isAvailable, this);
    }

    public void AddCardToDeck(string cardName)
    {
        var mgr = PlayerProgressionManager.Instance;
        var limits = mgr.GetDeckLimits();

        if (mgr.DeckCardNames.Count >= limits.maxTotal) { Debug.Log("[DeckBuilder] Лимит колоды!"); return; }

        Card card = CardM.AllCards.Find(c => c.Name == cardName);
        if (card != null)
        {
            int level = card.minChapterLevel > 3 ? 3 : card.minChapterLevel;
            int currentLevelCount = GetCardLevelCount(mgr.DeckCardNames, level);
            int limitForLevel = level == 1 ? limits.maxLevel1 : level == 2 ? limits.maxLevel2 : limits.maxLevel3;
            if (currentLevelCount >= limitForLevel) { Debug.Log($"[DeckBuilder] Лимит {level} уровня!"); return; }
        }

        if (mgr.AvailableCards.Contains(cardName))
        {
            mgr.AvailableCards.Remove(cardName);
            mgr.DeckCardNames.Add(cardName);
            RefreshUI();
        }
    }

    public void RemoveCardFromDeck(string cardName)
    {
        var mgr = PlayerProgressionManager.Instance;
        if (mgr != null && mgr.DeckCardNames.Contains(cardName))
        {
            mgr.DeckCardNames.Remove(cardName);
            mgr.AvailableCards.Add(cardName);
            RefreshUI();
        }
    }

    int GetCardLevelCount(List<string> deckNames, int targetLevel)
    {
        int count = 0;
        foreach (var name in deckNames)
        {
            Card card = CardM.AllCards.Find(c => c.Name == name);
            if (card != null)
            {
                int lvl = card.minChapterLevel > 3 ? 3 : card.minChapterLevel;
                if (lvl == targetLevel) count++;
            }
        }
        return count;
    }
}