using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AI : MonoBehaviour
{
    // Ссылка на настройки баланса
    public AISourceData aiSourceData;

    private void Awake()
    {
        if (aiSourceData == null)
        {
            // Пробуем найти в Resources или в сцене
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

        // 1. Определяем режим игры (0 - Attack, 1 - Defend, 2 - Balanced)
        int mode = DetermineMode();
        string modeName = mode == 0 ? "ATTACK" : (mode == 1 ? "DEFEND" : "BALANCED");
        Debug.Log($"[AI] Режим хода: {modeName}");

        // 2. Сортируем карты в руке по полезности для текущего режима
        List<CardController> sortedHand = new List<CardController>(handCards);
        sortedHand.Sort((a, b) => {
            float scoreA = GetCardUtilityScore(a.Card, mode);
            float scoreB = GetCardUtilityScore(b.Card, mode);
            return scoreB.CompareTo(scoreA); // По убыванию (лучшие первые)
        });

        // 3. Разыгрываем карты
        foreach (var card in sortedHand)
        {
            // Проверки безопасности
            if (!GameManager.Instance.EnemyHandCards.Contains(card)) continue;
            if (GameManager.Instance.EnemyFieldCards.Count >= 5) break;
            if (GameManager.Instance.CurrentGame.Enemy.Mana < card.Card.Manacost) continue;

            // Логика розыгрыша
            if (card.Card.IsSpell)
            {
                CastSpellSmart(card, mode);
            }
            else
            {
                PlayMinion(card);
            }

            yield return new WaitForSeconds(0.8f);
        }

        yield return new WaitForSeconds(1f);

        // 4. Фаза атаки
        while (GameManager.Instance.EnemyFieldCards.Exists(x => x.Card.CanAttack))
        {
            var attacker = GameManager.Instance.EnemyFieldCards.Find(x => x.Card.CanAttack);
            if (attacker == null) break;

            CardController target = ChooseTarget(attacker, mode);

            if (target != null)
            {
                // Атака по карте
                attacker.Movement.MoveToTarget(target.transform);
                yield return new WaitForSeconds(0.75f);
                GameManager.Instance.CardsFight(attacker, target);
            }
            else
            {
                // Атака по герою (если target == null)
                attacker.Movement.MoveToTarget(GameManager.Instance.PlayerHero.transform);
                yield return new WaitForSeconds(0.75f);
                GameManager.Instance.DamageHero(attacker, false);
            }

            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(1f);
        GameManager.Instance.ChangeTurn();
    }

    // --- ЛОГИКА ВЫБОРА РЕЖИМА ---

    int DetermineMode()
    {
        // === ИЗМЕНЕНИЕ ДЛЯ БЫСТРОЙ ИГРЫ ===
        // Если мы не в сюжетном режиме (т.е. в быстрой игре), используем выбранный игроком режим
        if (!TempData.IsStoryMode)
        {
            switch (TempData.FastGameAiMode)
            {
                case AISourceData.AIMode.Attack:
                    return 0; // 0 = Attack mode logic inside ChooseTarget/GetCardUtilityScore
                case AISourceData.AIMode.Defend:
                    return 1; // 1 = Defend mode logic
                default:
                    break; // Если Balanced, падаем вниз к стандартной логике
            }
        }

        // === СТАНДАРТНАЯ ЛОГИКА ДЛЯ СЮЖЕТНОГО РЕЖИМА ===
        float myPower = CalculateSidePower(GameManager.Instance.EnemyFieldCards);
        float enemyPower = CalculateSidePower(GameManager.Instance.PlayerFieldCards);

        if (enemyPower > myPower * 1.3f) return 1;
        if (myPower > enemyPower * 1.1f) return 0;

        return 2; // Balanced
    }

    float CalculateSidePower(List<CardController> cards)
    {
        float total = 0;
        foreach (var c in cards)
        {
            if (c == null || !c.Card.IsAlive) continue;

            // Используем AISourceData для точной оценки силы карты на поле
            if (aiSourceData != null)
                total += aiSourceData.GetCardPower(c.Card);
            else
                total += (c.Card.Attack + c.Card.Defense); // Фоллбэк
        }
        return total;
    }

    // --- ЛОГИКА РОЗЫГРЫША КАРТ ---

    /// <summary>
    /// Оценивает полезность карты в руке для конкретного режима игры.
    /// </summary>
    float GetCardUtilityScore(Card card, int mode)
    {
        if (aiSourceData == null) return card.Manacost; // Простая сортировка по мане если нет данных

        // Базовая сила карты из AISourceData (учитывает Attack, Defense и Abilities)
        float basePower = aiSourceData.GetCardPower(card);

        // Модификаторы в зависимости от режима
        if (card.IsSpell)
        {
            SpellCard spell = (SpellCard)card;

            if (mode == 1) // ЗАЩИТА: Приоритет лечению, щитам и провокации
            {
                if (spell.Spell == SpellCard.SpellType.HEAL_ALLY_FIELD_CARDS) basePower *= 1.6f;
                if (spell.Spell == SpellCard.SpellType.HEAL_ALLY_HERO) basePower *= 1.4f;
                if (spell.Spell == SpellCard.SpellType.SHIELD_ON_ALLY_CARD) basePower *= 1.3f;
                if (spell.Spell == SpellCard.SpellType.PROVOCATION_ON_ALLY_CARD) basePower *= 1.5f;

                // Урон в защите менее полезен, но если он массовый (AOE) - может спасти ситуацию
                if (spell.Spell == SpellCard.SpellType.DAMAGE_ENEMY_FIELD_CARDS) basePower *= 1.2f;
            }
            else if (mode == 0) // АТАКА: Приоритет прямому урону и баффам своих карт
            {
                if (spell.Spell == SpellCard.SpellType.DAMAGE_ENEMY_HERO) basePower *= 1.5f;
                if (spell.Spell == SpellCard.SpellType.DAMAGE_ENEMY_CARD) basePower *= 1.3f;
                if (spell.Spell == SpellCard.SpellType.BUFF_CARD_DAMAGE) basePower *= 1.4f;
                if (spell.Spell == SpellCard.SpellType.DAMAGE_ENEMY_FIELD_CARDS) basePower *= 1.3f;
            }
        }
        else
        {
            // Для существ: оцениваем способности

            // В ЗАЩИТЕ ценим выживаемость и контроль
            if (mode == 1)
            {
                if (card.Abilities.Contains(Card.AbilityType.PROVOCATION)) basePower *= 1.4f;
                if (card.Abilities.Contains(Card.AbilityType.SHIELD)) basePower *= 1.3f;
                if (card.Abilities.Contains(Card.AbilityType.REGENERATION_EACH_TURN)) basePower *= 1.3f;
                if (card.Abilities.Contains(Card.AbilityType.COUNTER_ATTACK)) basePower *= 1.2f;
            }

            // В АТАКЕ ценим быстрый урон и пробивание защиты
            if (mode == 0)
            {
                // DOUBLE_ATTACK позволяет нанести урон дважды, что очень ценно в атаке
                if (card.Abilities.Contains(Card.AbilityType.DOUBLE_ATTACK)) basePower *= 1.35f;

                // INSTANT_ACTIVE позволяет атаковать сразу, ускоряя темп
                if (card.Abilities.Contains(Card.AbilityType.INSTANT_ACTIVE)) basePower *= 1.25f;

                // Высокая атака сама по себе уже учтена в GetCardPower, но можно добавить бонус
                if (card.Attack > 5) basePower *= 1.1f;
            }
        }

        // Небольшой бонус за дешевизну (чтобы не застревать с дорогой картой в начале игры)
        basePower += (10 - card.Manacost) * 0.05f;

        return basePower;
    }

    void PlayMinion(CardController card)
    {
        card.GetComponent<CardMovement>().MoveToField(GameManager.Instance.EnemyField);
        // Ждем пока анимация дойдет до поля (упрощенно)
        card.transform.SetParent(GameManager.Instance.EnemyField);
        card.OnCast();
    }

    void CastSpellSmart(CardController card, int mode)
    {
        // Здесь можно доработать выбор цели для целевых заклинаний
        // Пока используем стандартную логику, но с проверкой условий
        CastSpell(card);
    }

    // --- ЛОГИКА АТАКИ ---

    /// <summary>
    /// Выбирает цель для атаки. Возвращает null, если нужно атаковать героя.
    /// </summary>
    CardController ChooseTarget(CardController attacker, int mode)
    {
        bool hasProvocation = GameManager.Instance.PlayerFieldCards.Exists(x => x.Card.IsProvocation);

        // 1. Обязательная атака таунтера
        if (hasProvocation)
        {
            return GameManager.Instance.PlayerFieldCards.Find(x => x.Card.IsProvocation);
        }

        // 2. Выбор цели в зависимости от режима
        if (mode == 0) // АТАКА: Пытаемся пробить лицо или добить слабую карту
        {
            // Ищем карту, которую можем убить одним ударом (или почти убить)
            // Это помогает расчистить поле для последующих атак по герою
            var killable = GameManager.Instance.PlayerFieldCards.Find(x => x.Card.Defense <= attacker.Card.Attack);
            if (killable != null) return killable;

            // Если убить некого, с шансом 50% бьем в случайную карту (чтобы сбить щиты/баффы), иначе в лицо
            if (Random.Range(0, 2) == 0 && GameManager.Instance.PlayerFieldCards.Count > 0)
                return GameManager.Instance.PlayerFieldCards[Random.Range(0, GameManager.Instance.PlayerFieldCards.Count)];

            return null; // Null означает атаку героя
        }
        else // ЗАЩИТА/БАЛАНС: Убираем самую опасную карту врага
        {
            CardController mostDangerous = null;
            float maxDanger = 0;

            foreach (var enemy in GameManager.Instance.PlayerFieldCards)
            {
                float danger = 0;
                if (aiSourceData != null)
                    danger = aiSourceData.GetCardPower(enemy.Card);
                else
                    danger = enemy.Card.Attack + enemy.Card.Defense;

                if (danger > maxDanger)
                {
                    maxDanger = danger;
                    mostDangerous = enemy;
                }
            }

            // Если есть опасная цель (например, карта с двойной атакой или высоким уроном), бьем её
            if (mostDangerous != null && maxDanger > 6.0f) // Порог опасности
                return mostDangerous;

            return null; // Иначе бьем героя
        }
    }

    // --- СТАНДАРТНАЯ ЛОГИКА ЗАКЛИНАНИЙ (без изменений, так как она рабочая) ---

    void CastSpell(CardController card)
    {
        switch (((SpellCard)card.Card).SpellTarget)
        {
            case SpellCard.TargetType.NO_TARGET:
                switch (((SpellCard)card.Card).Spell)
                {
                    case SpellCard.SpellType.HEAL_ALLY_FIELD_CARDS:
                        if (GameManager.Instance.EnemyFieldCards.Count > 0)
                            StartCoroutine(CastCard(card));
                        break;

                    case SpellCard.SpellType.DAMAGE_ENEMY_FIELD_CARDS:
                        if (GameManager.Instance.PlayerFieldCards.Count > 0)
                            StartCoroutine(CastCard(card));
                        break;

                    case SpellCard.SpellType.HEAL_ALLY_HERO:
                        StartCoroutine(CastCard(card));
                        break;

                    case SpellCard.SpellType.DAMAGE_ENEMY_HERO:
                        StartCoroutine(CastCard(card));
                        break;
                }
                break;

            case SpellCard.TargetType.ALLY_CARD_TARGET:
                if (GameManager.Instance.EnemyFieldCards.Count > 0)
                {
                    // Улучшение: лечить самого раненого, а не случайного
                    var target = GetMostWoundedAlly();
                    if (target != null)
                        StartCoroutine(CastCard(card, target));
                }
                break;

            case SpellCard.TargetType.ENEMY_CARD_TARGET:
                if (GameManager.Instance.PlayerFieldCards.Count > 0)
                {
                    // Улучшение: бить самого слабого или самого опасного (здесь случайно для простоты)
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
            // Примерная оценка здоровья (Defense как HP)
            float hpPercent = (float)c.Card.Defense / (c.Card.Defense + c.Card.Attack + 1);
            if (hpPercent < minHpPercent)
            {
                minHpPercent = hpPercent;
                wounded = c;
            }
        }
        return wounded;
    }

    IEnumerator CastCard(CardController spell, CardController target = null)
    {
        if (((SpellCard)spell.Card).SpellTarget == SpellCard.TargetType.NO_TARGET)
        {
            spell.GetComponent<CardMovement>().MoveToField(GameManager.Instance.EnemyField);
            yield return new WaitForSeconds(0.5f);
            spell.OnCast();
        }
        else
        {
            spell.Info.ShowCardInfo();
            spell.GetComponent<CardMovement>().MoveToTarget(target.transform);
            yield return new WaitForSeconds(0.5f);

            GameManager.Instance.EnemyHandCards.Remove(spell);
            GameManager.Instance.EnemyFieldCards.Add(spell);
            GameManager.Instance.ReduceMana(false, spell.Card.Manacost);

            spell.Card.IsPlaced = true;
            spell.UseSpell(target);
        }

        string targetStr = target == null ? "no_target" : target.Card.Name;
        Debug.Log("Enemy spell cast: " + (spell.Card).Name + " target: " + targetStr);
    }
}