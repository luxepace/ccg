using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject continueButtonObject; // Ссылка на объект кнопки "Продолжить", чтобы скрывать её, если нет сейва

    private void Start()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);

        CheckContinueAvailability();
    }

    // Проверяем, есть ли сохранения, чтобы показать/скрыть кнопку "Продолжить"
    void CheckContinueAvailability()
    {
        bool hasProgress = false;

        // Проверяем сохранение игрока
        if (PlayerProgressionManager.Instance != null && PlayerProgressionManager.Instance.HasSave())
        {
            // Проверяем сохранение карты
            // Используем FindObjectOfType, так как менеджера может не быть в сцене меню (он в StoryScene)
            // Но мы можем проверить наличие файла напрямую через путь или создать временный экземпляр логики.
            // Проще проверить файл напрямую:
            string mapPath = System.IO.Path.Combine(Application.persistentDataPath, "storyMapSave.json");
            if (System.IO.File.Exists(mapPath))
            {
                hasProgress = true;
            }
        }

        if (continueButtonObject != null)
        {
            continueButtonObject.SetActive(hasProgress);
        }
    }

    public void StartNewGame()
    {
        Debug.Log("[MENU] Нажата кнопка 'Новая игра'");

        // 1. Полностью очищаем сохранения
        if (PlayerProgressionManager.Instance != null)
        {
            PlayerProgressionManager.Instance.DeleteSave();
        }

        // Удаляем сохранение карты (через статический путь или поиск менеджера, если он есть в меню)
        string mapPath = System.IO.Path.Combine(Application.persistentDataPath, "storyMapSave.json");
        if (System.IO.File.Exists(mapPath))
        {
            System.IO.File.Delete(mapPath);
            Debug.Log("[MENU] Старое сохранение карты удалено.");
        }

        // Если в сцене меню есть StoryMapManager (редко, но бывает), вызываем у него очистку
        var mapManager = FindObjectOfType<StoryMapManager>();
        if (mapManager != null)
        {
            mapManager.DeleteSave();
        }

        // 2. Инициализируем данные игрока с нуля
        if (PlayerProgressionManager.Instance != null)
        {
            PlayerProgressionManager.Instance.StartNewGame();
        }
        else
        {
            Debug.LogError("[MENU] PlayerProgressionManager не найден! Создаем временный или проверяем сцену.");
            // В идеале, PlayerProgressionManager должен быть в сцене MainMenu или DontDestroyOnLoad
        }

        // 3. Загружаем сцену сюжета. 
        // В StoryMapManager.Start() сработает логика: "Сохранения нет -> Генерируем новую карту".
        SceneManager.LoadScene("StoryScene");
    }

    public void ContinueGame()
    {
        Debug.Log("[MENU] Нажата кнопка 'Продолжить'");

        // 1. Проверяем наличие сохранений
        bool playerExists = PlayerProgressionManager.Instance != null && PlayerProgressionManager.Instance.HasSave();
        string mapPath = System.IO.Path.Combine(Application.persistentDataPath, "storyMapSave.json");
        bool mapExists = System.IO.File.Exists(mapPath);

        if (playerExists && mapExists)
        {
            // 2. Если все есть, просто загружаем сцену.
            // Менеджеры в сцене (PlayerProgressionManager, StoryMapManager) в методах Awake/Start
            // сами обнаружат файлы и загрузят данные (LoadProgress / LoadMap).
            // НИЧЕГО НЕ СБРАСЫВАЕМ И НЕ ГЕНЕРИРУЕМ ЗАНОВО.
            SceneManager.LoadScene("StoryScene");
        }
        else
        {
            Debug.LogWarning("[MENU] Сохранение повреждено или отсутствует. Запуск новой игры.");
            // Можно вызвать диалоговое окно, а пока просто запускаем новую
            StartNewGame();
        }
    }

    public void StartQuickGame()
    {
        TempData.IsStoryMode = false;
        TempData.CurrentEnemy = null;
        // Для быстрой игры прогресс сюжета не трогаем, просто грузим бой
        SceneManager.LoadScene("CardGame");
    }

    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void BackToMainFromSettings()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        CheckContinueAvailability();
    }

    public void ExitGame()
    {
        Debug.Log("Выход из игры");
        Application.Quit();
    }
}