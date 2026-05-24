using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class FastGameManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject settingsPanel;      // Панель выбора сложности/врага/режима ИИ
    public GameObject deckBuilderPanel;   // Панель сборки колоды (можно переиспользовать DeckBuilderUI)
    public GameObject mainMenuPanel;

    [Header("Dropdowns & Buttons")]
    public TMP_Dropdown difficultyDropdown;
    public TMP_Dropdown aiModeDropdown;
    public TMP_Dropdown enemyDropdown;
    public Button startDeckBuildingBtn;
    public Button backToSettingsBtn;
    public Button confirmDeckBtn;
    public Button cancelDeckBtn;

    private void Start()
    {
        // Скрываем все панели при старте, кроме главного меню
        if (settingsPanel) settingsPanel.SetActive(false);
        if (deckBuilderPanel) deckBuilderPanel.SetActive(false);

        // Настраиваем кнопки
        if (startDeckBuildingBtn) startDeckBuildingBtn.onClick.AddListener(OnStartDeckBuilding);
        if (backToSettingsBtn) backToSettingsBtn.onClick.AddListener(OnBackToSettings);
        if (confirmDeckBtn) confirmDeckBtn.onClick.AddListener(OnConfirmDeckAndStart);
        if (cancelDeckBtn) cancelDeckBtn.onClick.AddListener(OnCancelDeckBuilding);
    }

    // 1. Игрок нажал "Начать сборку колоды" после выбора настроек
    public void OnStartDeckBuilding()
    {
        // Сохраняем выбранные настройки во TempData
        SaveSettingsToTempData();

        // Помечаем, что мы в режиме настройки
        TempData.IsSettingUpFastGame = true;

        // Переключаем UI
        if (settingsPanel) settingsPanel.SetActive(false);
        if (deckBuilderPanel) deckBuilderPanel.SetActive(true);

        // Здесь можно инициализировать DeckBuilderUI для режима быстрой игры
        // Например, передать ему TempData.FastGameDeck для редактирования
        Debug.Log("[FastGame] Переход к сборке колоды.");
    }

    // 2. Игрок подтвердил колоду и хочет начать бой
    public void OnConfirmDeckAndStart()
    {
        // Получаем текущую колоду из DeckBuilderUI (предполагаем, что он обновляет TempData.FastGameDeck)
        // Или берем напрямую из PlayerProgressionManager, если вы разрешаете менять основную колоду

        // ВАЖНО: Если вы хотите, чтобы колода для быстрой игры была ОТДЕЛЬНОЙ от сюжетной,
        // то DeckBuilderUI должен работать с TempData.FastGameDeck, а не с PlayerProgressionManager.DeckCardNames.

        // Для простоты сейчас предположим, что игрок собирает колоду из своих доступных карт,
        // и мы просто сохраняем этот список во TempData.

        // Проверка на минимальный размер колоды (например, 5 карт)
        if (TempData.FastGameDeck.Count < 5)
        {
            Debug.LogWarning("[FastGame] Колода слишком маленькая! Минимум 5 карт.");
            return;
        }

        // Запускаем бой
        StartFastBattle();
    }

    // 3. Отмена сборки колоды
    public void OnCancelDeckBuilding()
    {
        TempData.IsSettingUpFastGame = false;
        if (deckBuilderPanel) deckBuilderPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    // 4. Возврат к настройкам
    public void OnBackToSettings()
    {
        TempData.IsSettingUpFastGame = false;
        if (deckBuilderPanel) deckBuilderPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    // 5. Сохранение настроек из Dropdown'ов
    private void SaveSettingsToTempData()
    {
        // Сложность (пока просто сохраняем индекс, можно использовать для ограничений колоды)
        // int difficulty = difficultyDropdown.value; 

        // Режим ИИ
        switch (aiModeDropdown.value)
        {
            case 0: TempData.FastGameAiMode = AISourceData.AIMode.Balanced; break;
            case 1: TempData.FastGameAiMode = AISourceData.AIMode.Attack; break;
            case 2: TempData.FastGameAiMode = AISourceData.AIMode.Defend; break;
        }

        // Выбор врага
        int enemyIndex = enemyDropdown.value;
        if (StoryContentLoader.AllEnemies != null && enemyIndex >= 0 && enemyIndex < StoryContentLoader.AllEnemies.Count)
        {
            // Если выбран "Случайный" (допустим, индекс 0), то выбираем рандомно позже
            if (enemyIndex == 0)
            {
                TempData.CurrentEnemy = null; // Null означает "случайный"
            }
            else
            {
                // Иначе берем конкретного врага (минус 1, т.к. первый элемент - "Случайный")
                TempData.CurrentEnemy = StoryContentLoader.AllEnemies[enemyIndex - 1];
            }
        }

        Debug.Log($"[FastGame] Настройки сохранены. Враг: {TempData.CurrentEnemy?.enemyName ?? "Случайный"}, Режим ИИ: {TempData.FastGameAiMode}");
    }

    // 6. Запуск сцены боя
    private void StartFastBattle()
    {
        TempData.IsStoryMode = false;
        TempData.IsSettingUpFastGame = false;

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

        Debug.Log($"[FastGame] Запуск боя против: {TempData.CurrentEnemy.enemyName}");
        SceneManager.LoadScene("CardGame");
    }
}