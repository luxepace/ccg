using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// =============================================================================
// КЛАСС ДАННЫХ ИГРЫ (Модель)
// =============================================================================
public class Game
{
    public Player Player, Enemy;
    public List<Card> EnemyDeck, PlayerDeck;

    // Конструктор для обычной игры (случайные колоды, стандартные HP)
    public Game()
    {
        EnemyDeck = GiveRandomDeck();
        PlayerDeck = GiveRandomDeck();
        Player = new Player();
        Enemy = new Player();
    }

    public Game(EnemyData enemyData)
    {
        // 1. ВРАГ
        Enemy = new Player();
        Enemy.HP = enemyData.enemyHP;
        EnemyDeck = GenerateDeckFromList(enemyData.enemyDeck);

        // 2. ИГРОК
        Player = new Player();

        // === ПРОВЕРКА РЕЖИМА БЫСТРОЙ ИГРЫ ===
        if (TempData.IsSettingUpFastGame || !TempData.IsStoryMode)
        {
            // РЕЖИМ БЫСТРОЙ ИГРЫ
            Player.MaxHP = 30; // Или берите из настроек сложности
            Player.HP = Player.MaxHP;

            if (TempData.FastGameDeck != null && TempData.FastGameDeck.Count > 0)
            {
                PlayerDeck = GenerateDeckFromList(TempData.FastGameDeck);
                Debug.Log($"[Game] Быстрая игра. Колода игрока из TempData: {PlayerDeck.Count} карт.");
            }
            else
            {
                // Фоллбэк на случайную, если TempData пуста (ошибка логики)
                PlayerDeck = GiveRandomDeck();
            }
        }
        else
        {
            // СЮЖЕТНЫЙ РЕЖИМ
            if (PlayerProgressionManager.Instance != null)
            {
                Player.MaxHP = PlayerProgressionManager.Instance.MaxHp;
                Player.HP = PlayerProgressionManager.Instance.CurrentHp;
                PlayerDeck = GenerateDeckFromList(PlayerProgressionManager.Instance.DeckCardNames);
            }
            else
            {
                PlayerDeck = GiveRandomDeck();
            }
        }
    }

    // Генерация случайной колоды (для игрока или тестового режима)
    List<Card> GiveRandomDeck()
    {
        List<Card> list = new List<Card>();
        // Генерируем 8 карт для скорости теста (можно вернуть 10)
        int deckSize = 8;

        for (int i = 0; i < deckSize; i++)
        {
            if (CardM.AllCards.Count == 0) break;
            var card = CardM.AllCards[Random.Range(0, CardM.AllCards.Count)];

            if (card.IsSpell)
                list.Add(((SpellCard)card).GetCopy());
            else
                list.Add(card.GetCopy());
        }
        return list;
    }

    // НОВЫЙ МЕТОД: Создание колоды из списка имен карт
    List<Card> GenerateDeckFromList(List<string> cardNames)
    {
        List<Card> list = new List<Card>();

        if (cardNames == null || cardNames.Count == 0)
        {
            Debug.LogWarning("[Game] У врага не задана колода (enemyDeck пуст). Генерируем случайную.");
            return GiveRandomDeck();
        }

        foreach (string name in cardNames)
        {
            // Ищем карту в общей базе по имени (должно совпадать точь-в-точь с cards.json)
            Card found = CardM.AllCards.Find(c => c.Name == name);

            if (found != null)
            {
                if (found.IsSpell)
                    list.Add(((SpellCard)found).GetCopy());
                else
                    list.Add(found.GetCopy());
            }
            else
            {
                Debug.LogError($"[Game] Карта с именем '{name}' НЕ НАЙДЕНА в базе карт! Проверьте опечатки в JSON врага.");
            }
        }

        // Перемешиваем собранную колоду врага
        for (int i = 0; i < list.Count; i++)
        {
            Card temp = list[i];
            int r = Random.Range(i, list.Count);
            list[i] = list[r];
            list[r] = temp;
        }

        Debug.Log($"[Game] Колода врага сформирована: {list.Count} карт.");
        return list;
    }
}

// =============================================================================
// МЕНЕДЖЕР ИГРЫ (MonoBehaviour)
// =============================================================================
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Game CurrentGame;
    public Transform EnemyHand, PlayerHand, EnemyField, PlayerField;
    public GameObject CardPref;

    int Turn, TurnTime = 30;

    public AttackedHero EnemyHero, PlayerHero;
    public AI EnemyAI;

    public List<CardController> PlayerHandCards = new List<CardController>(),
                             PlayerFieldCards = new List<CardController>(),
                             EnemyHandCards = new List<CardController>(),
                             EnemyFieldCards = new List<CardController>();

    public bool IsPlayerTurn
    {
        get
        {
            return Turn % 2 == 0;
        }
    }

    public PauseMenu pauseMenu;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log("[GameManager] Start() вызван. Начинаем игру!");
        StartGame();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (pauseMenu != null) pauseMenu.TogglePause();
        }
    }

    public void RestartGame()
    {
        StopAllCoroutines();
        ClearAllCards();
        StartGame();
    }

    public void StopGame()
    {
        StopAllCoroutines();
        ClearAllCards();
    }

    void ClearAllCards()
    {
        foreach (var card in PlayerHandCards) Destroy(card.gameObject);
        foreach (var card in PlayerFieldCards) Destroy(card.gameObject);
        foreach (var card in EnemyHandCards) Destroy(card.gameObject);
        foreach (var card in EnemyFieldCards) Destroy(card.gameObject);

        PlayerHandCards.Clear();
        PlayerFieldCards.Clear();
        EnemyHandCards.Clear();
        EnemyFieldCards.Clear();
    }

    // =========================================================================
    // ГЛАВНОЕ ИЗМЕНЕНИЕ: Логика старта игры
    // =========================================================================
    void StartGame()
    {
        Time.timeScale = 1f;
        Turn = 0;

        // ПРОВЕРКА: Есть ли данные о враге из карты сюжета?
        if (TempData.CurrentEnemy != null)
        {
            Debug.Log($"[GameManager] >>> ЗАПУСК БОЯ С БОССОМ: {TempData.CurrentEnemy.enemyName}");
            Debug.Log($"[GameManager] HP Босса: {TempData.CurrentEnemy.enemyHP}");
            Debug.Log($"[GameManager] Размер колоды босса: {TempData.CurrentEnemy.enemyDeck?.Count ?? 0} карт");

            // Создаем игру с данными конкретного врага
            CurrentGame = new Game(TempData.CurrentEnemy);

            // Настраиваем визуал и применяем фишки
            SetupEnemyVisuals(TempData.CurrentEnemy);

            // Очищаем TempData, чтобы следующий перезапуск сцены не подхватил старого врага
            // (Но не очищаем CurrentNode, он может понадобиться для возврата)
            // TempData.CurrentEnemy = null; 
        }
        else
        {
            Debug.Log("[GameManager] >>> ЗАПУСК СЛУЧАЙНОГО БОЯ (Режим тестирования / Нет данных врага)");
            CurrentGame = new Game();
        }

        // Раздача карт (теперь колоды уже правильные: у врага - из JSON, у игрока - рандом)
        GiveHandCards(CurrentGame.EnemyDeck, EnemyHand);
        GiveHandCards(CurrentGame.PlayerDeck, PlayerHand);

        UIController.Instance.StartGame();
        StartCoroutine(TurnFunc());
    }

    // Метод настройки врага (аватар, реплики, особые условия)
    void SetupEnemyVisuals(EnemyData enemy)
    {
        // 1. Установка аватара (если у EnemyHero есть дочерний Image)
        Image avatarImage = EnemyHero.GetComponentInChildren<Image>();
        if (avatarImage != null && !string.IsNullOrEmpty(enemy.avatarPath))
        {
            Sprite avatarSprite = Resources.Load<Sprite>(enemy.avatarPath);
            if (avatarSprite != null)
            {
                avatarImage.sprite = avatarSprite;
                avatarImage.preserveAspect = true;
                Debug.Log($"[Setup] Аватар врага установлен: {enemy.avatarPath}");
            }
            else
            {
                Debug.LogWarning($"[Setup] Спрайт аватара не найден по пути: {enemy.avatarPath}. Проверьте папку Resources.");
            }
        }

        // 2. Вывод реплики врага при начале боя
        if (enemy.quotes != null && enemy.quotes.Count > 0)
        {
            string quote = enemy.quotes[Random.Range(0, enemy.quotes.Count)];
            Debug.Log($"[BOSS SAY]: '{quote}'");
            // Здесь можно добавить код для отображения текста в UI пузыре, если создадите его
        }

        // 3. Применение особых условий (Фишек) из JSON
        if (enemy.condition == EnemyConditionType.START_WITH_LESS_HP)
        {
            CurrentGame.Player.HP = 15; // Игрок начинает с малым здоровьем
            Debug.Log("[ФИШКА] Игрок начинает бой с 15 HP!");
        }
        else if (enemy.condition == EnemyConditionType.REDUCED_MANA)
        {
            CurrentGame.Player.Manapool = 5; // Макс маны снижен
            CurrentGame.Player.Mana = 5;
            Debug.Log("[ФИШКА] Максимум маны игрока снижен до 5!");
        }
        else if (enemy.condition == EnemyConditionType.MAX_HAND_SIZE_4)
        {
            Debug.Log("[ФИШКА] Ограничение руки игрока до 4 карт (требуется доработка логики раздачи)!");
            // Пока просто лог, так как логика руки жестко зашита в циклах
        }

        UIController.Instance.UpdateHPAndMana();
    }

    // =========================================================================
    // Стандартная логика хода и механик
    // =========================================================================

    void GiveHandCards(List<Card> deck, Transform hand)
    {
        int i = 0;
        while (i++ < 4)
            GiveCardToHand(deck, hand);
    }

    void GiveCardToHand(List<Card> deck, Transform hand)
    {
        if (deck.Count == 0)
            return;

        CreateCardPref(deck[0], hand);
        deck.RemoveAt(0);
    }

    void CreateCardPref(Card card, Transform hand)
    {
        GameObject cardGO = Instantiate(CardPref, hand, false);
        CardController cardC = cardGO.GetComponent<CardController>();

        cardC.Init(card, hand == PlayerHand);

        if (cardC.IsPlayerCard)
            PlayerHandCards.Add(cardC);
        else
            EnemyHandCards.Add(cardC);
    }

    IEnumerator TurnFunc()
    {
        TurnTime = 31;
        UIController.Instance.UpdateTurnTime(TurnTime);

        foreach (var card in PlayerFieldCards)
            card.Info.HighlightCard(false);

        CheckCardsForManaAvailability();

        if (IsPlayerTurn)
        {
            foreach (var card in PlayerFieldCards)
            {
                card.Card.CanAttack = true;
                card.Info.HighlightCard(true);
                card.Ability.OnNewTurn();
            }

            while (TurnTime-- > 0)
            {
                UIController.Instance.UpdateTurnTime(TurnTime);
                yield return new WaitForSeconds(1);
            }

            ChangeTurn();
        }
        else
        {
            foreach (var card in EnemyFieldCards)
            {
                card.Card.CanAttack = true;
                card.Ability.OnNewTurn();
            }
            EnemyAI.MakeTurn();

            while (TurnTime-- > 0)
            {
                UIController.Instance.UpdateTurnTime(TurnTime);
                yield return new WaitForSeconds(1);
            }

            ChangeTurn();
        }
    }

    public void ChangeTurn()
    {
        StopAllCoroutines();

        Turn++;
        BattleStats.IncrementTurn();
        UIController.Instance.OnOffTurnBtn();

        if (IsPlayerTurn)
        {
            GiveNewCards();
            CurrentGame.Player.IncreaseManapool();
            CurrentGame.Player.RestoreRoundMana();
            UIController.Instance.UpdateHPAndMana();
        }
        else
        {
            CurrentGame.Enemy.IncreaseManapool();
            CurrentGame.Enemy.RestoreRoundMana();
        }

        StartCoroutine(TurnFunc());
    }

    void GiveNewCards()
    {
        GiveCardToHand(CurrentGame.EnemyDeck, EnemyHand);
        GiveCardToHand(CurrentGame.PlayerDeck, PlayerHand);
    }

    public void CardsFight(CardController attacker, CardController defender)
    {
        defender.Card.GetDamage(attacker.Card.Attack);
        attacker.OnDamageDeal();
        defender.OnTakeDamage(attacker);

        attacker.Card.GetDamage(defender.Card.Attack);
        attacker.OnTakeDamage();

        attacker.CheckForAlive();
        defender.CheckForAlive();
    }

    public void ReduceMana(bool playerMana, int manacost)
    {
        if (playerMana)
            CurrentGame.Player.Mana -= manacost;
        else
            CurrentGame.Enemy.Mana -= manacost;

        UIController.Instance.UpdateHPAndMana();
    }

    public void DamageHero(CardController card, bool isEnemyAttacked)
    {
        if (isEnemyAttacked)
            CurrentGame.Enemy.GetDamage(card.Card.Attack);
        else
            CurrentGame.Player.GetDamage(card.Card.Attack);

        UIController.Instance.UpdateHPAndMana();
        card.OnDamageDeal();
        CheckForResult();
    }

    public void CheckForResult()
    {
        bool isPlayerDead = CurrentGame.Player.HP <= 0;
        bool isEnemyDead = CurrentGame.Enemy.HP <= 0;
        if (isPlayerDead || isEnemyDead)
        {
            StopAllCoroutines();
            // 1. Считаем карты в руке
            int handCount = PlayerHandCards != null ? PlayerHandCards.Count : 0;

            // 2. Определяем, была ли победа (враг мертв)
            bool isVictory = isEnemyDead;
            TempData.BossDefeated = isEnemyDead;
            // 3. Получаем имя врага (если есть данные, иначе "Unknown")
            string enemyName = "Unknown";
            if (TempData.CurrentEnemy != null && !string.IsNullOrEmpty(TempData.CurrentEnemy.enemyName))
            {
                enemyName = TempData.CurrentEnemy.enemyName;
            }

            // 4. Вызываем метод со ВСЕМИ аргументами
            BattleStats.FinishBattle(
                CurrentGame.Player.HP,
                CurrentGame.Player.MaxHP, // Убедись, что это поле есть в классе Player!
                handCount,
                isVictory,
                enemyName
            );
            UIController.Instance.ShowResult();
        }
    }

    public void CheckCardsForManaAvailability()
    {
        foreach (var card in PlayerHandCards)
        {
            card.Info.HighlightManaAvaliability(CurrentGame.Player.Mana);
        }
    }

    public void HighlightTargets(CardController attacker, bool highlight)
    {
        List<CardController> targets = new List<CardController>();

        if (attacker.Card.IsSpell)
        {
            var spellCard = (SpellCard)attacker.Card;

            if (spellCard.SpellTarget == SpellCard.TargetType.NO_TARGET)
                targets = new List<CardController>();
            else if (spellCard.SpellTarget == SpellCard.TargetType.ALLY_CARD_TARGET)
                targets = PlayerFieldCards;
            else
                targets = EnemyFieldCards;
        }
        else
        {
            if (EnemyFieldCards.Exists(x => x.Card.IsProvocation))
                targets = EnemyFieldCards.FindAll(x => x.Card.IsProvocation);
            else
            {
                targets = EnemyFieldCards;
                if (!attacker.Card.IsSpell)
                    EnemyHero.HighlightAsTarget(highlight);
            }
        }

        foreach (var card in targets)
        {
            if (attacker.Card.IsSpell)
                card.Info.HighlightAsSpellTarget(highlight);
            else
                card.Info.HighlightAsTarget(highlight);
        }
    }
}