using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class PlayerSaveData
{
    public int currentHp;
    public int maxHp;
    public int gold;
    public List<string> unlockedCards = new List<string>();    // Коллекция (уникальные)
    public List<string> availableCards = new List<string>();   // Пул доступных копий (с дублями)
    public List<string> deckCardNames = new List<string>();
    public int currentChapter = 1;
}

public class PlayerProgressionManager : MonoBehaviour
{
    public static PlayerProgressionManager Instance;

    [Header("Настройки")]
    public int StartingMaxHp = 30;
    public List<string> DefaultStarterDeck = new List<string>();

    public int CurrentHp { get; private set; }
    public int MaxHp { get; private set; }
    public int Gold { get; private set; }

    public List<string> UnlockedCards { get; private set; } = new List<string>();
    public List<string> AvailableCards { get; private set; } = new List<string>();
    public List<string> DeckCardNames { get; private set; } = new List<string>();

    private string saveFileName = "playerProgress.json";

    public int CurrentChapter { get; set; } = 1;

    [System.Serializable]
    public class DeckLimits
    {
        public int maxTotal;
        public int maxLevel1;
        public int maxLevel2;
        public int maxLevel3;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress();
            RepairSaveData(); // Автоматически чинит битое сохранение при старте
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool HasSave()
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        return File.Exists(path);
    }

    public void StartNewGame(List<string> chosenDeck = null)
    {
        Debug.Log("[Progression] Старт новой игры...");

        // 1. СБРОС ТЕКУЩЕГО ЗАБЕГА
        CurrentChapter = 1;
        MaxHp = StartingMaxHp;
        CurrentHp = MaxHp;
        Gold = 0;

        // 2. ВОЗВРАТ КАРТ ИЗ КОЛОДЫ В ОБЩИЙ ПУЛ (Для повторных запусков)
        foreach (var cardName in DeckCardNames)
        {
            AvailableCards.Add(cardName);
        }
        DeckCardNames.Clear();

        List<string> starterCards = new List<string>();

        if (chosenDeck != null && chosenDeck.Count > 0)
        {
            // Если передана кастомная колода (например, из быстрой игры), используем её
            starterCards = chosenDeck;
        }
        else
        {
            // === ПРОВЕРКА НА ПЕРВЫЙ ЗАПУСК ===
            bool isFirstLaunch = (UnlockedCards.Count == 0);

            if (isFirstLaunch)
            {
                Debug.Log("[Progression] Первый запуск! Генерируем стартовую колоду.");

                // Убедимся, что база карт загружена
                if (CardM.AllCards == null || CardM.AllCards.Count == 0)
                {
                    Debug.LogWarning("[Progression] База карт пуста! Попытка принудительной загрузки...");
                    CardDatabase db = ScriptableObject.CreateInstance<CardDatabase>();
                    db.LoadFromJSON("cards.json");
                    CardM.AllCards = new List<Card>(db.fieldCards);
                    CardM.AllCards.AddRange(db.spells);
                }

                foreach (var card in CardM.AllCards)
                {
                    if (card.isBaseCard)
                    {
                        int copiesToAdd = (card.Manacost <= 2) ? 2 : 1;

                        for (int i = 0; i < copiesToAdd; i++)
                        {
                            // ВАЖНО: Добавляем ТОЛЬКО в колоду и коллекцию.
                            // В AvailableCards НЕ добавляем, чтобы избежать дублей.
                            starterCards.Add(card.Name);

                            if (!UnlockedCards.Contains(card.Name))
                            {
                                UnlockedCards.Add(card.Name);
                            }
                        }
                    }
                }
            }
            else
            {
                // Повторный запуск: Колода остается пустой.
                // Игрок должен собрать её сам через редактор колоды.
                Debug.Log($"[Progression] Повторный запуск. В пуле доступно {AvailableCards.Count} карт.");
            }
        }

        // Заполняем колоду сформированными картами
        foreach (var cardName in starterCards)
        {
            // Для первого запуска карты идут напрямую в колоду.
            // Для повторного запуска (если бы мы делали авто-сборку) нужно было бы проверять AvailableCards.
            // Но так как мы решили не давать стартовые карты повторно, просто добавляем.
            DeckCardNames.Add(cardName);
        }

        Debug.Log($"[Progression] Колода сформирована: {DeckCardNames.Count} карт");
        Debug.Log($"[Progression] Доступно карт (пул): {AvailableCards.Count}");
        Debug.Log($"[Progression] Открыто карт (коллекция): {UnlockedCards.Count}");

        SaveProgress();
        BattleStats.ClearHistory();
    }

    public void PrepareForBattle()
    {
        CurrentHp = MaxHp;
        Debug.Log($"[Progression] Бой: HP восстановлено до {CurrentHp}/{MaxHp}");
        SaveProgress();
    }

    public void ApplyReward(int goldChange = 0, int healAmount = 0, int maxHpChange = 0,
                    List<string> cardsToAdd = null, List<string> cardsToRemove = null)
    {
        bool changed = false;

        if (goldChange != 0) { Gold += goldChange; if (Gold < 0) Gold = 0; changed = true; }
        int totalMaxHpChange = maxHpChange + healAmount;

        if (totalMaxHpChange != 0)
        {
            MaxHp += totalMaxHpChange;
            if (MaxHp < 1) MaxHp = 1;

            // Также восстанавливаем текущее HP до нового максимума, чтобы не было ситуации HP < MaxHP
            CurrentHp = MaxHp;

            changed = true;
        }

        if (cardsToAdd != null)
        {
            foreach (var card in cardsToAdd)
            {
                if (!string.IsNullOrEmpty(card))
                {
                    // Если карта новая, добавляем в коллекцию
                    if (!UnlockedCards.Contains(card))
                    {
                        UnlockedCards.Add(card);
                        Debug.Log($"[Collection] ОТКРЫТА НОВАЯ КАРТА: {card}");
                    }

                    // ДОБАВЛЯЕМ ТОЛЬКО В ПУЛ ДОСТУПНЫХ КАРТ (не в колоду!)
                    AvailableCards.Add(card);
                    Debug.Log($"[Available] Добавлена копия в доступные: {card}");

                    // УБРАНО: DeckCardNames.Add(card); 
                    // Игрок сам решит, добавлять ли эту карту в колоду через конструктор

                    changed = true;
                }
            }
        }

        if (cardsToRemove != null)
        {
            foreach (var card in cardsToRemove)
            {
                if (!string.IsNullOrEmpty(card) && DeckCardNames.Contains(card))
                {
                    DeckCardNames.Remove(card);
                    changed = true;
                }
            }
        }

        if (changed) SaveProgress();
    }

    public void RemoveRandomCard()
    {
        if (DeckCardNames.Count == 0) return;
        int index = Random.Range(0, DeckCardNames.Count);
        string removed = DeckCardNames[index];
        DeckCardNames.RemoveAt(index);
        Debug.Log($"[Progression] Удалена карта: {removed}");
        SaveProgress();
    }

    public void SaveProgress()
    {
        try
        {
            // Гарантируем, что списки не null перед сериализацией
            if (UnlockedCards == null) UnlockedCards = new List<string>();
            if (AvailableCards == null) AvailableCards = new List<string>();
            if (DeckCardNames == null) DeckCardNames = new List<string>();

            PlayerSaveData data = new PlayerSaveData
            {
                currentHp = CurrentHp,
                maxHp = MaxHp,
                gold = Gold,
                unlockedCards = new List<string>(UnlockedCards),
                availableCards = new List<string>(AvailableCards),
                deckCardNames = new List<string>(DeckCardNames),
                currentChapter = CurrentChapter
            };

            string json = JsonUtility.ToJson(data, true);
            string path = Path.Combine(Application.persistentDataPath, saveFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);

            Debug.Log($"[Save] Прогресс успешно сохранен в файл: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Save] Ошибка сохранения: {e.Message}\nStackTrace: {e.StackTrace}");
        }
    }

    public void LoadProgress()
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName);

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);

            if (data != null)
            {
                CurrentHp = data.currentHp;
                MaxHp = data.maxHp;
                Gold = data.gold;
                UnlockedCards = data.unlockedCards ?? new List<string>();
                AvailableCards = data.availableCards ?? new List<string>();
                DeckCardNames = data.deckCardNames ?? new List<string>();
                CurrentChapter = data.currentChapter > 0 ? data.currentChapter : 1;
                Debug.Log("[Progression] Данные загружены.");
                return;
            }
        }

        Debug.Log("[Progression] Первый запуск. Инициализация...");
        StartNewGame();
    }

    // === АВТОПОЧИНКА СОХРАНЕНИЯ ===
    private void RepairSaveData()
    {
        // ИСПРАВЛЕНИЕ: Не пытаемся "чинить" сохранение при самом первом запуске игры.
        // При первом запуске AvailableCards ДОЛЖЕН быть пустым, а карты - только в колоде.
        // Проверяем признаки реального прогресса: наличие золота, открытых карт или пройденных глав.
        bool hasRealProgress = Gold > 0 || UnlockedCards.Count > 0 || CurrentChapter > 1;

        if (!hasRealProgress)
        {
            Debug.Log("[Repair] Первый запуск detected. Пропускаем автопочинку, чтобы не дублировать стартовые карты.");
            return;
        }

        bool fixedSomething = false;

        // Если пул пуст, но в колоде есть карты (например, после бага), переносим их в пул
        if (AvailableCards.Count == 0 && DeckCardNames.Count > 0)
        {
            foreach (var card in DeckCardNames)
            {
                if (!AvailableCards.Contains(card))
                {
                    AvailableCards.Add(card);
                    fixedSomething = true;
                }
            }
        }

        // Синхронизируем коллекцию с пулом (все, что в пуле, должно быть открыто в коллекции)
        foreach (var card in AvailableCards)
        {
            if (!UnlockedCards.Contains(card))
            {
                UnlockedCards.Add(card);
                fixedSomething = true;
            }
        }

        if (fixedSomething)
        {
            Debug.Log("[Progression] Исправлена структура сохранения.");
            SaveProgress();
        }
    }

    public void SetDeckFromBuilder(List<string> newDeck)
    {
        DeckCardNames = new List<string>(newDeck);
        SaveProgress();
        Debug.Log($"[Progression] Колода обновлена игроком. Размер: {DeckCardNames.Count}");
    }

    public bool IsCardUnlocked(string cardName)
    {
        return UnlockedCards.Contains(cardName);
    }

    public bool IsCardAvailable(string cardName)
    {
        return GetAvailableCount(cardName) > 0;
    }

    public int GetUnlockedCount()
    {
        return new HashSet<string>(UnlockedCards).Count;
    }

    public int GetAvailableCount(string cardName)
    {
        int count = 0;
        foreach (var card in AvailableCards)
        {
            if (card == cardName) count++;
        }
        return count;
    }

    public void UnlockCard(string cardName)
    {
        if (!string.IsNullOrEmpty(cardName) && !UnlockedCards.Contains(cardName))
        {
            UnlockedCards.Add(cardName);
            AvailableCards.Add(cardName); // Даем 1 копию при открытии
            Debug.Log($"[Collection] ОТКРЫТА НОВАЯ КАРТА: {cardName}");
            SaveProgress();
        }
    }

    public void AddCardsToDeck(List<string> cardsToAdd)
    {
        if (cardsToAdd == null) return;
        foreach (var card in cardsToAdd)
        {
            if (!string.IsNullOrEmpty(card))
            {
                UnlockCard(card);
                DeckCardNames.Add(card);
            }
        }
        SaveProgress();
    }

    public List<string> GetUnlockedCards()
    {
        return new List<string>(UnlockedCards);
    }

    public void DeleteSave()
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("[Progression] Сохранение игрока удалено.");
        }
    }

    public DeckLimits GetDeckLimits()
    {
        return GetDeckLimits(CurrentChapter);
    }

    public DeckLimits GetDeckLimits(int chapter)
    {
        if (chapter >= 3)
            return new DeckLimits { maxTotal = 25, maxLevel1 = 12, maxLevel2 = 8, maxLevel3 = 5 };
        else if (chapter == 2)
            return new DeckLimits { maxTotal = 20, maxLevel1 = 10, maxLevel2 = 7, maxLevel3 = 3 };
        else // Глава 1 или игра не начата
            return new DeckLimits { maxTotal = 15, maxLevel1 = 8, maxLevel2 = 5, maxLevel3 = 2 };
    }
}