using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO; // Убедись, что это есть

public class MainMenuManager : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject continueButtonObject;

    private void Start()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        CheckContinueAvailability();
    }

    void CheckContinueAvailability()
    {
        bool hasProgress = false;

        if (PlayerProgressionManager.Instance != null && PlayerProgressionManager.Instance.HasSave())
        {
            string mapPath = Path.Combine(Application.persistentDataPath, "storyMapSave.json");
            if (File.Exists(mapPath))
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

        // 1. Очищаем сохранения игрока
        if (PlayerProgressionManager.Instance != null)
        {
            PlayerProgressionManager.Instance.DeleteSave();
        }

        // 2. Очищаем сохранение карты сюжета
        string mapPath = Path.Combine(Application.persistentDataPath, "storyMapSave.json");
        if (File.Exists(mapPath))
        {
            File.Delete(mapPath);
            Debug.Log("[MENU] Старое сохранение карты удалено.");
        }

        // === НОВОЕ: Удаляем файлы ландшафтов для всех глав ===
        // Мы должны гарантировать, что старые файлы высот не будут использованы
        for (int i = 0; i < 10; i++) // Предполагаем макс 10 глав
        {
            string chapterMapPath = Path.Combine(Application.persistentDataPath, $"map_chapter_{i}.json");
            if (File.Exists(chapterMapPath))
            {
                File.Delete(chapterMapPath);
                Debug.Log($"[MENU] Удален старый ландшафт главы {i}: {chapterMapPath}");
            }
        }
        // ================================================

        // Если в сцене меню есть StoryMapManager, вызываем у него очистку (на всякий случай)
        var mapManager = FindObjectOfType<StoryMapManager>();
        if (mapManager != null)
        {
            mapManager.DeleteSave();
            // Если у MapGenerator3D есть метод очистки, можно вызвать и его здесь, 
            // но удаление файлов выше уже решает проблему.
            var gen = FindObjectOfType<MapGenerator3D>();
            if (gen != null)
            {
                gen.DeleteAllChapterMaps(); // Если ты добавил этот метод в MapGenerator3D
            }
        }

        // 3. Инициализируем данные игрока с нуля
        if (PlayerProgressionManager.Instance != null)
        {
            PlayerProgressionManager.Instance.StartNewGame();
        }

        // 4. Загружаем сцену сюжета
        // В StoryMapManager.Start() сработает логика: "Сохранения нет -> Генерируем новую карту и новые ландшафты"
        SceneManager.LoadScene("StoryScene");
    }

    public void ContinueGame()
    {
        Debug.Log("[MENU] Нажата кнопка 'Продолжить'");

        bool playerExists = PlayerProgressionManager.Instance != null && PlayerProgressionManager.Instance.HasSave();
        string mapPath = Path.Combine(Application.persistentDataPath, "storyMapSave.json");
        bool mapExists = File.Exists(mapPath);

        if (playerExists && mapExists)
        {
            SceneManager.LoadScene("StoryScene");
        }
        else
        {
            Debug.LogWarning("[MENU] Сохранение повреждено или отсутствует. Запуск новой игры.");
            StartNewGame();
        }
    }

    public void StartQuickGame()
    {
        TempData.IsStoryMode = false;
        TempData.CurrentEnemy = null;
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