using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AI : MonoBehaviour
{
    // Настройки баланса
    public AISourceData aiSourceData;

    // Память о прошлом ходе
    private float previousMyPower = 0f;
    private float previousEnemyPower = 0f;
    private int previousMyCardsCount = 0;
    private int previousEnemyCardsCount = 0;

    private void Awake()
    {
        if (aiSourceData == null)
        {
            aiSourceData = Resources.Load<AISourceData>("AI/AISourceData");
            if (aiSourceData == null)
                aiSourceData = FindObjectOfType<AISourceData>();
        }
    }

    public void MakeTurn()
    {
        StartCoroutine(EnemyTurn(GameManager.Instance.EnemyHandCards));
    }

    IEnumerator EnemyTurn(List<CardController> handCards)
    {
        yield return new WaitForSeconds(1f);

        // Выбираем режим: атака или защита
        int mode = DetermineMode();
        string modeName = mode == 0 ? "ATTACK" : (mode == 1 ? "DEFEND" : "BALANCED");

        Debug.Log($"[AI] === НАЧАЛО ХОДА ПРОТИВНИКА ===");
        Debug.Log($"[AI] Режим: {modeName}");
        Debug.Log($"[AI] Мана противника: {GameManager.Instance.CurrentGame.Enemy.Mana}");

        // Отбираем карты по мане
        List<CardController> affordableCards = handCards.FindAll(c => c.Card.Manacost <= GameManager.Instance.CurrentGame.Enemy.Mana);

        if (affordableCards.Count > 0)
        {
            // Находим лучшую комбинацию
            List<CardController> bestCombo = FindBestCardCombination(affordableCards, mode);

            Debug.Log($"[AI] Найдено оптимальное сочетание из {bestCombo.Count} карт.");

            foreach (var card in bestCombo)
            {
                if (!GameManager.Instance.EnemyHandCards.Contains(card)) continue;
                if (GameManager.Instance.EnemyFieldCards.Count >= 5)
                {
                    Debug.Log("[AI] Поле заполнено (5 карт). Прекращаю розыгрыш.");
                    break;
                }

                if (GameManager.Instance.CurrentGame.Enemy.Mana < card.Card.Manacost)
                {
                    Debug.LogWarning($"[AI] Ошибка расчета маны! Пропускаю карту '{card.Card.Name}'.");
                    continue;
                }

                if (card.Card.IsSpell)
                {
                    Debug.Log($"[AI] Разыгрываю заклинание: {card.Card.Name} (Мана: {card.Card.Manacost})");
                    yield return StartCoroutine(CastSpellSmart(card, mode));
                }
                else
                {
                    Debug.Log($"[AI] Разыгрываю существо: {card.Card.Name} (Атака: {card.Card.Attack}, Защита: {card.Card.Defense}, Мана: {card.Card.Manacost})");
                    yield return StartCoroutine(card.GetComponent<CardMovement>().MoveToFieldCoroutine(GameManager.Instance.EnemyField));
                    card.transform.SetParent(GameManager.Instance.EnemyField);
                    card.OnCast();
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            Debug.Log("[AI] Нет карт, на которые хватает маны.");
        }

        yield return new WaitForSeconds(1f);

        // Атака
        Debug.Log("[AI] Начинаю фазу атаки...");
        while (GameManager.Instance.EnemyFieldCards.Exists(x => x.Card.CanAttack))
        {
            var attacker = GameManager.Instance.EnemyFieldCards.Find(x => x.Card.CanAttack);
            if (attacker == null) break;

            CardController target = ChooseTarget(attacker, mode);

            if (target != null)
            {
                Debug.Log($"[AI] {attacker.Card.Name} атакует карту: {target.Card.Name}");
                yield return StartCoroutine(attacker.Movement.MoveToTargetCor(target.transform));
                yield return new WaitForSeconds(0.75f);
                GameManager.Instance.CardsFight(attacker, target);
            }
            else
            {
                Debug.Log($"[AI] {attacker.Card.Name} атакует ГЕРОЯ игрока");
                yield return StartCoroutine(attacker.Movement.MoveToTargetCor(GameManager.Instance.PlayerHero.transform));
                yield return new WaitForSeconds(0.75f);
                GameManager.Instance.DamageHero(attacker, false);
            }
            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log("[AI] === КОНЕЦ ХОДА ПРОТИВНИКА ===");

        // Запоминаем состояние поля
        UpdateMemoryForNextTurn();

        yield return new WaitForSeconds(1f);
        GameManager.Instance.ChangeTurn();
    }

    void UpdateMemoryForNextTurn()
    {
        previousMyPower = CalculateSidePower(GameManager.Instance.EnemyFieldCards);
        previousEnemyPower = CalculateSidePower(GameManager.Instance.PlayerFieldCards);
        previousMyCardsCount = GameManager.Instance.EnemyFieldCards.Count;
        previousEnemyCardsCount = GameManager.Instance.PlayerFieldCards.Count;
    }

    int DetermineMode()
    {
        // Быстрая игра с фиксированным режимом
        if (!TempData.IsStoryMode)
        {
            switch (TempData.FastGameAiMode)
            {
                case AISourceData.AIMode.Attack:
                    Debug.Log("[AI] Режим установлен игроком: ATTACK");
                    return 0;
                case AISourceData.AIMode.Defend:
                    Debug.Log("[AI] Режим установлен игроком: DEFEND");
                    return 1;
                case AISourceData.AIMode.Balanced:
                    break;
            }
        }

        // Оцениваем поле
        float currentMyPower = CalculateSidePower(GameManager.Instance.EnemyFieldCards);
        float currentEnemyPower = CalculateSidePower(GameManager.Instance.PlayerFieldCards);

        int currentMyCardsCount = GameManager.Instance.EnemyFieldCards.Count;
        int currentEnemyCardsCount = GameManager.Instance.PlayerFieldCards.Count;

        Debug.Log($"[AI] Оценка поля: Моя сила {currentMyPower:F1} vs Сила врага {currentEnemyPower:F1}");

        // Первый ход без истории
        if (previousMyPower == 0 && previousEnemyPower == 0)
        {
            if (currentEnemyPower > currentMyPower * 1.3f)
            {
                Debug.Log("[AI] Первый ход: враг сильнее, выбран режим DEFEND");
                return 1;
            }
            Debug.Log("[AI] Первый ход: достаточное преимущество, выбран режим ATTACK");
            return 0;
        }

        // Сравниваем с прошлым ходом
        float enemyPowerRatio = currentEnemyPower / (previousEnemyPower + 0.1f);
        float myPowerRatio = currentMyPower / (previousMyPower + 0.1f);

        Debug.Log($"[AI] Динамика: враг {enemyPowerRatio:F2}x, мы {myPowerRatio:F2}x от прошлого хода");

        // Когда защищаться
        bool needDefend = false;
        if (currentEnemyPower > currentMyPower * 1.5f)
            needDefend = true;
        if (enemyPowerRatio > 1.3f && previousEnemyPower > previousMyPower)
            needDefend = true;
        if (currentMyCardsCount < 2 && currentEnemyCardsCount >= 3)
            needDefend = true;

        // Когда атаковать
        bool needAttack = false;
        if (currentMyPower > currentEnemyPower * 1.2f)
            needAttack = true;
        int myLosses = previousMyCardsCount - currentMyCardsCount;
        int enemyLosses = previousEnemyCardsCount - currentEnemyCardsCount;
        if (enemyLosses > myLosses && currentMyPower >= currentEnemyPower)
            needAttack = true;
        if (enemyPowerRatio < 0.8f && currentMyPower >= currentEnemyPower * 0.9f)
            needAttack = true;

        // Выбор режима
        if (needDefend && !needAttack)
        {
            Debug.Log($"[AI] Режим: DEFEND (враг опасен: {previousEnemyPower:F1} -> {currentEnemyPower:F1})");
            return 1;
        }
        else if (needAttack && !needDefend)
        {
            Debug.Log("[AI] Режим: ATTACK (у нас преимущество)");
            return 0;
        }
        else if (needDefend && needAttack)
        {
            if (currentEnemyPower > currentMyPower)
            {
                Debug.Log("[AI] Режим: DEFEND (конфликт сигналов, враг сильнее)");
                return 1;
            }
            else
            {
                Debug.Log("[AI] Режим: ATTACK (конфликт сигналов, мы сильнее)");
                return 0;
            }
        }
        else
        {
            Debug.Log("[AI] Режим: ATTACK (нейтральная ситуация, атака по умолчанию)");
            return 0;
        }
    }

    float CalculateSidePower(List<CardController> cards)
    {
        float total = 0;
        foreach (var c in cards)
        {
            if (c == null || !c.Card.IsAlive) continue;
            if (aiSourceData != null)
                total += aiSourceData.GetCardPower(c.Card);
            else
                total += (c.Card.Attack + c.Card.Defense);
        }
        return total;
    }

    float GetCardUtilityScore(Card card, int mode)
    {
        if (aiSourceData == null) return card.Manacost;
        float basePower = aiSourceData.GetCardPower(card);

        if (card.IsSpell)
        {
            SpellCard spell = (SpellCard)card;
            if (mode == 1)
            {
                if (spell.Spell == SpellCard.SpellType.HEAL_ALLY_FIELD_CARDS) basePower *= 1.6f;
                if (spell.Spell == SpellCard.SpellType.HEAL_ALLY_HERO) basePower *= 1.4f;
                if (spell.Spell == SpellCard.SpellType.SHIELD_ON_ALLY_CARD) basePower *= 1.3f;
                if (spell.Spell == SpellCard.SpellType.PROVOCATION_ON_ALLY_CARD) basePower *= 1.5f;
            }
            else if (mode == 0)
            {
                if (spell.Spell == SpellCard.SpellType.DAMAGE_ENEMY_HERO) basePower *= 1.5f;
                if (spell.Spell == SpellCard.SpellType.DAMAGE_ENEMY_CARD) basePower *= 1.3f;
                if (spell.Spell == SpellCard.SpellType.BUFF_CARD_DAMAGE) basePower *= 1.4f;
            }
        }
        else
        {
            if (mode == 1)
            {
                if (card.Abilities.Contains(Card.AbilityType.PROVOCATION)) basePower *= 1.4f;
                if (card.Abilities.Contains(Card.AbilityType.SHIELD)) basePower *= 1.3f;
                if (card.Abilities.Contains(Card.AbilityType.REGENERATION_EACH_TURN)) basePower *= 1.3f;
            }
            if (mode == 0)
            {
                if (card.Abilities.Contains(Card.AbilityType.DOUBLE_ATTACK)) basePower *= 1.35f;
                if (card.Abilities.Contains(Card.AbilityType.INSTANT_ACTIVE)) basePower *= 1.25f;
            }
        }
        basePower += (10 - card.Manacost) * 0.05f;
        return basePower;
    }

    List<CardController> FindBestCardCombination(List<CardController> cards, int mode)
    {
        int maxMana = GameManager.Instance.CurrentGame.Enemy.Mana;
        int maxSlots = 5 - GameManager.Instance.EnemyFieldCards.Count;
        if (cards.Count == 0) return new List<CardController>();

        int n = cards.Count;
        int limit = Mathf.Min(n, 20);
        float bestScore = -1;
        List<CardController> bestCombo = new List<CardController>();
        int totalCombinations = 1 << limit;

        for (int i = 1; i < totalCombinations; i++)
        {
            List<CardController> currentCombo = new List<CardController>();
            int currentManaCost = 0;
            float currentScore = 0;
            int slotCount = 0;
            bool isValid = true;

            for (int j = 0; j < limit; j++)
            {
                if ((i & (1 << j)) != 0)
                {
                    CardController card = cards[j];
                    if (!card.Card.IsSpell)
                    {
                        if (slotCount + 1 > maxSlots) { isValid = false; break; }
                        slotCount++;
                    }
                    currentManaCost += card.Card.Manacost;
                    currentScore += GetCardUtilityScore(card.Card, mode);
                    currentCombo.Add(card);
                }
            }

            if (isValid && currentManaCost <= maxMana)
            {
                if (currentScore > bestScore)
                {
                    bestScore = currentScore;
                    bestCombo = currentCombo;
                }
            }
        }
        return bestCombo;
    }

    CardController ChooseTarget(CardController attacker, int mode)
    {
        bool hasProvocation = GameManager.Instance.PlayerFieldCards.Exists(x => x.Card.IsProvocation);
        if (hasProvocation)
        {
            return GameManager.Instance.PlayerFieldCards.Find(x => x.Card.IsProvocation);
        }

        if (mode == 0)
        {
            var killable = GameManager.Instance.PlayerFieldCards.Find(x => x.Card.Defense <= attacker.Card.Attack);
            if (killable != null) return killable;

            if (Random.Range(0, 2) == 0 && GameManager.Instance.PlayerFieldCards.Count > 0)
                return GameManager.Instance.PlayerFieldCards[Random.Range(0, GameManager.Instance.PlayerFieldCards.Count)];

            return null;
        }
        else
        {
            CardController mostDangerous = null;
            float maxDanger = 0;

            foreach (var enemy in GameManager.Instance.PlayerFieldCards)
            {
                float danger = aiSourceData != null ? aiSourceData.GetCardPower(enemy.Card) : enemy.Card.Attack + enemy.Card.Defense;
                if (danger > maxDanger)
                {
                    maxDanger = danger;
                    mostDangerous = enemy;
                }
            }

            if (mostDangerous != null && maxDanger > 6.0f) return mostDangerous;
            return null;
        }
    }

    IEnumerator CastSpellSmart(CardController card, int mode)
    {
        // Вызываем основную логику и ждем её
        yield return StartCoroutine(CastCard(card));
    }

    void CastSpell(CardController card)
    {
        switch (((SpellCard)card.Card).SpellTarget)
        {
            case SpellCard.TargetType.NO_TARGET:
                switch (((SpellCard)card.Card).Spell)
                {
                    case SpellCard.SpellType.HEAL_ALLY_FIELD_CARDS:
                        if (GameManager.Instance.EnemyFieldCards.Count > 0) StartCoroutine(CastCard(card));
                        break;
                    case SpellCard.SpellType.DAMAGE_ENEMY_FIELD_CARDS:
                        if (GameManager.Instance.PlayerFieldCards.Count > 0) StartCoroutine(CastCard(card));
                        break;
                    case SpellCard.SpellType.HEAL_ALLY_HERO:
                    case SpellCard.SpellType.DAMAGE_ENEMY_HERO:
                        StartCoroutine(CastCard(card));
                        break;
                }
                break;
            case SpellCard.TargetType.ALLY_CARD_TARGET:
                if (GameManager.Instance.EnemyFieldCards.Count > 0)
                {
                    var target = GetMostWoundedAlly();
                    if (target != null) StartCoroutine(CastCard(card, target));
                }
                break;
            case SpellCard.TargetType.ENEMY_CARD_TARGET:
                if (GameManager.Instance.PlayerFieldCards.Count > 0)
                {
                    var target = GameManager.Instance.PlayerFieldCards[Random.Range(0, GameManager.Instance.PlayerFieldCards.Count)];
                    StartCoroutine(CastCard(card, target));
                }
                break;
        }
    }

    CardController GetMostWoundedAlly()
    {
        CardController wounded = null;
        float minHpPercent = 1.1f;
        foreach (var c in GameManager.Instance.EnemyFieldCards)
        {
            float hpPercent = (float)c.Card.Defense / (c.Card.Defense + c.Card.Attack + 1);
            if (hpPercent < minHpPercent) { minHpPercent = hpPercent; wounded = c; }
        }
        return wounded;
    }

    IEnumerator CastCard(CardController spell, CardController target = null)
    {
        // 1. ЛОГИКА ДЛЯ ЗАКЛИНАНИЙ БЕЗ ЦЕЛИ (AOE / ГЕРОЙ)
        if (((SpellCard)spell.Card).SpellTarget == SpellCard.TargetType.NO_TARGET)
        {
            // Сначала летим на поле
            yield return StartCoroutine(spell.GetComponent<CardMovement>().MoveToFieldCoroutine(GameManager.Instance.EnemyField));

            // === ОТКРЫВАЕМ КАРТУ ТОЛЬКО ПОСЛЕ ПРИЛЕТА ===
            if (spell.Info != null)
            {
                spell.Info.ShowCardInfo();
            }

            // Пауза для чтения
            yield return new WaitForSeconds(1.5f);

            // Применяем эффект
            spell.OnCast();
        }
        // 2. ЛОГИКА ДЛЯ ЗАКЛИНАНИЙ С ЦЕЛЬЮ
        else
        {
            // Сначала летим к цели
            yield return StartCoroutine(spell.GetComponent<CardMovement>().MoveToTargetCor(target.transform));

            // === ПРОВЕРКА: Существует ли цель и карта после полета? ===
            // Если цель умерла пока карта летела, или карта была удалена, прерываем выполнение
            if (target == null || spell == null || !spell.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[AI] Цель исчезла во время полета заклинания. Отмена.");
                // Можно добавить логику возврата карты в руку или её уничтожения без эффекта
                if (spell != null && spell.Info != null) spell.Info.HideCardInfo();
                yield break;
            }

            // === ОТКРЫВАЕМ КАРТУ ТОЛЬКО КОГДА ОНА УЖЕ У ЦЕЛИ ===
            if (spell.Info != null)
            {
                spell.Info.ShowCardInfo();
            }

            // Пауза для чтения (игрок видит карту прямо над целью)
            yield return new WaitForSeconds(1.5f);

            // Удаляем из руки и добавляем на поле (визуально она уже там)
            GameManager.Instance.EnemyHandCards.Remove(spell);
            GameManager.Instance.EnemyFieldCards.Add(spell);

            // Списываем ману
            int cost = spell.Card.Manacost;
            if (GameManager.Instance.CurrentGame.Enemy.Mana < cost)
            {
                GameManager.Instance.CurrentGame.Enemy.Mana = 0;
            }
            else
            {
                GameManager.Instance.ReduceMana(false, cost);
            }

            spell.Card.IsPlaced = true;

            // Применяем эффект
            spell.UseSpell(target);
        }

        string targetStr = target == null ? "no_target" : target.Card.Name;
        Debug.Log("Enemy spell cast: " + (spell.Card).Name + " target: " + targetStr);
    }
}