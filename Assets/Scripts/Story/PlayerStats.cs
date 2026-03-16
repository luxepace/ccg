using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance;

    [Header("Характеристики")]
    public int CurrentHealth = 30;
    public int MaxHealth = 30;
    public int Gold = 0;

    [Header("Колода")]
    // Здесь храним ID карт или имена, которые есть у игрока ГЛОБАЛЬНО
    public List<string> DeckCardNames = new List<string>();

    [Header("Настройки")]
    public bool IsDead = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Живет всегда
            InitDefaultDeck(); // Стартовая колода
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitDefaultDeck()
    {
        if (DeckCardNames.Count == 0)
        {
            // Стартовый набор карт (имена должны совпадать с cards.json)
            DeckCardNames.Add("Охотник с копьём");
            DeckCardNames.Add("Щитоносец");
            // Добавь сюда 5-8 названий карт, которые есть в твоей базе
            Debug.Log("[PlayerStats] Колода инициализирована стартовыми картами.");
        }
    }

    // --- Методы управления ---

    public void HealToFull()
    {
        CurrentHealth = MaxHealth;
    }

    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
    }

    public void TakeDamage(int amount)
    {
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (CurrentHealth == 0) IsDead = true;
    }

    public void IncreaseMaxHealth(int amount)
    {
        MaxHealth += amount;
        CurrentHealth += amount;
    }

    public void AddGold(int amount)
    {
        Gold += amount;
    }

    public void AddCardToDeck(string cardName)
    {
        if (!DeckCardNames.Contains(cardName))
        {
            DeckCardNames.Add(cardName);
            Debug.Log($"[PlayerStats] Карта '{cardName}' добавлена в колоду.");
        }
    }

    public void RemoveCardFromDeck(string cardName)
    {
        if (DeckCardNames.Contains(cardName))
        {
            DeckCardNames.Remove(cardName);
            Debug.Log($"[PlayerStats] Карта '{cardName}' удалена из колоды.");
        }
    }

    // Сброс прогресса (для кнопки "Новая игра")
    public void ResetProgress()
    {
        CurrentHealth = 30;
        MaxHealth = 30;
        Gold = 0;
        IsDead = false;
        DeckCardNames.Clear();
        InitDefaultDeck();
        BattleStats.ClearHistory();
    }
}