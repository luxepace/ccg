using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BattleRecord
{
    public int turnsCount;
    public int playerEndHP;
    public int playerMaxHP;
    public int cardsInHandAtEnd;
    public bool isVictory; // Добавим флаг победы позже, пока пусть будет
    public string enemyName; // Можно передать имя врага

    public BattleRecord(int turns, int hp, int maxHp, int hand, bool win, string name)
    {
        turnsCount = turns;
        playerEndHP = hp;
        playerMaxHP = maxHp;
        cardsInHandAtEnd = hand;
        isVictory = win;
        enemyName = name;
    }
}

public static class BattleStats
{
    // История всех боев в текущей сессии
    public static List<BattleRecord> BattleHistory = new List<BattleRecord>();

    // Данные текущего (последнего) боя
    public static int TurnsCount { get; private set; } = 0;
    public static int PlayerEndHP { get; private set; } = 0;
    public static int PlayerMaxHP { get; private set; } = 0;
    public static int CardsInHandAtEnd { get; private set; } = 0;

    public static bool IsBattleCompleted { get; private set; } = false;

    public static void Reset()
    {
        TurnsCount = 0;
        PlayerEndHP = 0;
        CardsInHandAtEnd = 0;
        IsBattleCompleted = false;
        // Историю НЕ сбрасываем!
    }

    public static void IncrementTurn()
    {
        if (!IsBattleCompleted)
            TurnsCount++;
    }

    // Добавили параметр isVictory и enemyName
    public static void FinishBattle(int currentHp, int maxHp, int handCount, bool isVictory, string enemyName = "Unknown")
    {
        PlayerEndHP = currentHp;
        PlayerMaxHP = maxHp;
        CardsInHandAtEnd = handCount;
        IsBattleCompleted = true;

        // Сохраняем в историю
        BattleRecord record = new BattleRecord(TurnsCount, currentHp, maxHp, handCount, isVictory, enemyName);
        BattleHistory.Add(record);

        Debug.Log($"[BATTLE STATS] БОЙ ЗАВЕРШЕН! | Ходов: {TurnsCount} | ХП: {currentHp}/{maxHp} | Карт: {handCount} | Победа: {isVictory}");
        Debug.Log($"[HISTORY] Всего боев записано: {BattleHistory.Count}");
    }

    // Метод для очистки истории (например, при начале новой игры)
    public static void ClearHistory()
    {
        BattleHistory.Clear();
    }
}