using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

public class FastGameSettingsManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown difficultyDropdown; // Низкая, Средняя, Высокая сложность
    public TMP_Dropdown aiModeDropdown;     // Сбалансированный, Атака, Защита
    public TMP_Dropdown enemyDeckDropdown;  // Случайный, Конкретные колоды
    public Button playButton;
    public Button backButton;

    [Header("Settings")]
    public int minDeckSizeForFastGame = 5; // Минимальный размер колоды для быстрой игры

    public void Start()
    {
        InitializeDifficultyDropdown();
        InitializeAiModeDropdown();
        InitializeEnemyDeckDropdown();

        if (playButton != null)
        {
            // Кнопка "Играть" теперь открывает редактор колоды
            playButton.onClick.AddListener(OnOpenDeckBuilder);
        }

        if (backButton != null)
            backButton.onClick.AddListener(GoBackToMainMenu);
    }

    void InitializeDifficultyDropdown()
    {
        if (difficultyDropdown == null) return;
        List<string> options = new List<string> { "Низкая (Глава 1)", "Средняя (Глава 2)", "Высокая (Глава 3)" };
        difficultyDropdown.ClearOptions();
        difficultyDropdown.AddOptions(options);
    }

    void InitializeAiModeDropdown()
    {
        if (aiModeDropdown == null) return;
        List<string> options = new List<string> { "Сбалансированный", "Атакующий", "Защитный" };
        aiModeDropdown.ClearOptions();
        aiModeDropdown.AddOptions(options);
    }

    // В файле FastGameSettingsManager.cs

    void InitializeEnemyDeckDropdown()
    {
        if (enemyDeckDropdown == null) return;

        List<string> options = new List<string> { "Случайный враг" };

        // === ИСПРАВЛЕНИЕ: Принудительная загрузка контента, если он еще не готов ===
        if (!StoryContentLoader.IsLoaded)
        {
            Debug.Log("[FastGame] Контент не загружен. Загружаем...");
            StoryContentLoader.LoadAllContent();
        }

        if (StoryContentLoader.AllEnemies != null && StoryContentLoader.AllEnemies.Count > 0)
        {
            foreach (var enemy in StoryContentLoader.AllEnemies)
            {
                // Добавляем пометку [БОСС] для удобства
                string label = enemy.isBoss ? $"[БОСС] {enemy.enemyName}" : enemy.enemyName;
                options.Add(label);
            }
            Debug.Log($"[FastGame] Загружено {StoryContentLoader.AllEnemies.Count} врагов в dropdown.");
        }
        else
        {
            Debug.LogError("[FastGame] ОШИБКА: Список врагов пуст даже после загрузки! Проверьте enemies.json в StreamingAssets.");
            options.Add("Ошибка загрузки");
        }

        enemyDeckDropdown.ClearOptions();
        enemyDeckDropdown.AddOptions(options);
    }

    // В файле FastGameSettingsManager.cs

    // В файле FastGameSettingsManager.cs

    public void OnOpenDeckBuilder()
    {
        // 1. СНАЧАЛА сохраняем настройки и СТАВИМ ФЛАГ
        SaveSettingsToTempData();

        Debug.Log($"[FastGame] Перед установкой: TempData.IsSettingUpFastGame = {TempData.IsSettingUpFastGame}");
        TempData.IsSettingUpFastGame = true;
        Debug.Log($"[FastGame] После установки: TempData.IsSettingUpFastGame = {TempData.IsSettingUpFastGame}");

        Debug.Log($"[FastGame] Флаг IsSettingUpFastGame установлен: {TempData.IsSettingUpFastGame}");

        // 2. Инициализируем временную колоду, если она пуста
        if (TempData.FastGameDeck == null)
        {
            TempData.FastGameDeck = new List<string>();
            // Можно скопировать текущую колоду игрока для удобства
            if (PlayerProgressionManager.Instance != null)
            {
                foreach (var card in PlayerProgressionManager.Instance.DeckCardNames)
                {
                    if (!TempData.FastGameDeck.Contains(card))
                        TempData.FastGameDeck.Add(card);
                }
            }
        }

        Debug.Log("[FastGame] Переход к сборке колоды для быстрой игры.");

        // 3. ТОЛЬКО ПОСЛЕ ЭТОГО открываем панель
        MainMenuManager mainMenu = FindObjectOfType<MainMenuManager>();
        if (mainMenu != null)
        {
            mainMenu.OpenDeckBuilder();
        }
    }

    // 2. Этот метод вызывается из DeckBuilderUI после подтверждения колоды
    public void StartFastBattle()
    {
        // Проверка на минимальный размер колоды
        if (TempData.FastGameDeck == null || TempData.FastGameDeck.Count < 5)
        {
            Debug.LogError("[FastGame] Ошибка: Колода слишком маленькая или пуста!");
            return;
        }

        // Если враг не выбран (случайный), выбираем его сейчас
        if (TempData.CurrentEnemy == null && StoryContentLoader.AllEnemies != null && StoryContentLoader.AllEnemies.Count > 0)
        {
            TempData.CurrentEnemy = StoryContentLoader.AllEnemies[Random.Range(0, StoryContentLoader.AllEnemies.Count)];
        }

        if (TempData.CurrentEnemy == null)
        {
            Debug.LogError("[FastGame] Ошибка: Не удалось выбрать врага!");
            return;
        }

        TempData.IsStoryMode = false;
        TempData.IsSettingUpFastGame = false; // Сбрасываем флаг, так как настройка завершена

        Debug.Log($"[FastGame] Запуск боя против: {TempData.CurrentEnemy.enemyName}");
        SceneManager.LoadScene("CardGame");
    }

    // Вспомогательный метод сохранения настроек из Dropdown'ов
    private void SaveSettingsToTempData()
    {
        // Режим ИИ
        switch (aiModeDropdown.value)
        {
            case 0: TempData.FastGameAiMode = AISourceData.AIMode.Balanced; break;
            case 1: TempData.FastGameAiMode = AISourceData.AIMode.Attack; break;
            case 2: TempData.FastGameAiMode = AISourceData.AIMode.Defend; break;
        }

        // Выбор врага
        int enemyIndex = enemyDeckDropdown.value;
        if (enemyIndex == 0)
        {
            TempData.CurrentEnemy = null; // Null означает "Случайный"
        }
        else
        {
            if (StoryContentLoader.AllEnemies != null && enemyIndex - 1 >= 0 && enemyIndex - 1 < StoryContentLoader.AllEnemies.Count)
            {
                TempData.CurrentEnemy = StoryContentLoader.AllEnemies[enemyIndex - 1];
            }
        }
    }

    EnemyData GetSelectedEnemyData()
    {
        // Дополнительная проверка загрузки перед выбором
        if (!StoryContentLoader.IsLoaded)
        {
            StoryContentLoader.LoadAllContent();
        }

        if (StoryContentLoader.AllEnemies == null || StoryContentLoader.AllEnemies.Count == 0)
        {
            Debug.LogError("[FastGame] AllEnemies пуст!");
            return null;
        }

        int index = enemyDeckDropdown.value;

        // Если выбран "Случайный враг" (индекс 0)
        if (index == 0)
        {
            return StoryContentLoader.AllEnemies[Random.Range(0, StoryContentLoader.AllEnemies.Count)];
        }
        else
        {
            // Иначе берем конкретного врага. 
            // Минус 1, так как первый элемент в списке - "Случайный враг"
            int enemyIndex = index - 1;

            if (enemyIndex >= 0 && enemyIndex < StoryContentLoader.AllEnemies.Count)
            {
                return StoryContentLoader.AllEnemies[enemyIndex];
            }
        }

        return null;
    }

    private AISourceData.AIMode GetSelectedAiMode()
    {
        switch (aiModeDropdown.value)
        {
            case 1: return AISourceData.AIMode.Attack;
            case 2: return AISourceData.AIMode.Defend;
            default: return AISourceData.AIMode.Balanced;
        }
    }

    void ApplyDeckLimitsForDifficulty(int difficulty)
    {
        if (PlayerProgressionManager.Instance == null) return;

        var limits = PlayerProgressionManager.Instance.GetDeckLimits(difficulty + 1); // +1 т.к. главы начинаются с 1

        List<string> currentDeck = PlayerProgressionManager.Instance.DeckCardNames;
        List<string> availablePool = PlayerProgressionManager.Instance.AvailableCards;

        // Простая логика: если колода превышает лимиты, удаляем лишние карты случайным образом
        // В идеале тут должна быть более умная логика удаления (например, сначала дорогие, потом дубликаты)

        bool changed = false;

        // Проверка общего лимита
        while (currentDeck.Count > limits.maxTotal && currentDeck.Count > 0)
        {
            string cardToRemove = currentDeck[Random.Range(0, currentDeck.Count)];
            currentDeck.Remove(cardToRemove);
            // Возвращаем карту в пул доступных, чтобы она не потерялась
            availablePool.Add(cardToRemove);
            changed = true;
        }

        // Здесь можно добавить проверки по уровням карт (maxLevel1, maxLevel2...) аналогично общему лимиту
        // Для простоты пока оставим только общий лимит.

        if (changed)
        {
            PlayerProgressionManager.Instance.SetDeckFromBuilder(currentDeck);
            Debug.Log("[FastGame] Колода игрока ограничена под выбранную сложность.");
        }
    }

    void GoBackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}