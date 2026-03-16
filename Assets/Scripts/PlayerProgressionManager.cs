using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class PlayerSaveData
{
    public int currentHp;
    public int maxHp;
    public int gold;
    public List<string> deckCardNames = new List<string>();
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
    public List<string> DeckCardNames { get; private set; } = new List<string>();

    private string saveFileName = "playerProgress.json";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProgress(); // Пытаемся загрузить при старте приложения
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // === ПРОВЕРКА НАЛИЧИЯ СОХРАНЕНИЯ ===
    public bool HasSave()
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        return File.Exists(path);
    }

    // === НОВАЯ ИГРА (ПОЛНЫЙ СБРОС) ===
    public void StartNewGame(List<string> chosenDeck = null)
    {
        Debug.Log("[Progression] Старт новой игры. Сброс данных...");

        // 1. Сброс статов
        MaxHp = StartingMaxHp;
        CurrentHp = MaxHp;
        Gold = 0;

        DeckCardNames.Clear();

        // 2. Инициализация колоды
        if (chosenDeck != null && chosenDeck.Count > 0)
        {
            DeckCardNames.AddRange(chosenDeck);
        }
        else if (DefaultStarterDeck.Count > 0)
        {
            DeckCardNames.AddRange(DefaultStarterDeck);
        }
        else
        {
            // Дефолтная колода, если ничего не передано
            DeckCardNames.Add("Охотник с копьём");
            DeckCardNames.Add("Щитоносец");
            // Добавь остальные стартовые карты
        }

        // 3. Сохраняем сразу, чтобы при переходе в сцену данные были
        SaveProgress();

        // 4. Очищаем статистику боев
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
        if (maxHpChange != 0) { MaxHp += maxHpChange; if (MaxHp < 1) MaxHp = 1; changed = true; }
        if (healAmount != 0) { CurrentHp += healAmount; CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp); changed = true; }

        if (cardsToAdd != null)
        {
            foreach (var card in cardsToAdd)
            {
                if (!string.IsNullOrEmpty(card) && !DeckCardNames.Contains(card))
                {
                    DeckCardNames.Add(card);
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
        PlayerSaveData data = new PlayerSaveData
        {
            currentHp = CurrentHp,
            maxHp = MaxHp,
            gold = Gold,
            deckCardNames = new List<string>(DeckCardNames)
        };

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(Application.persistentDataPath, saveFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
        // Debug.Log("[Progression] Данные сохранены.");
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
                DeckCardNames = data.deckCardNames ?? new List<string>();
                Debug.Log("[Progression] Данные загружены.");
                return;
            }
        }

        // Если сохранения нет, инициализируем дефолтными значениями (но не сохраняем, пока игрок не начнет игру)
        MaxHp = StartingMaxHp;
        CurrentHp = MaxHp;
        Gold = 0;
        if (DefaultStarterDeck.Count > 0) DeckCardNames = new List<string>(DefaultStarterDeck);
        else { DeckCardNames.Add("Охотник с копьём"); DeckCardNames.Add("Щитоносец"); }

        Debug.Log("[Progression] Сохранение не найдено, установлены значения по умолчанию.");
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
}