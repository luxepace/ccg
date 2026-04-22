using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

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

        // Очищаем сохранения игрока
        if (PlayerProgressionManager.Instance != null)
        {
            PlayerProgressionManager.Instance.DeleteSave();
        }

        // Очищаем сохранение карты сюжета
        string mapPath = Path.Combine(Application.persistentDataPath, "storyMapSave.json");
        if (File.Exists(mapPath))
        {
            File.Delete(mapPath);
            Debug.Log("[MENU] Старое сохранение карты удалено.");
        }

        // Удаляем файлы ландшафтов для всех глав
        for (int i = 0; i < 10; i++) 
        {
            string chapterMapPath = Path.Combine(Application.persistentDataPath, $"map_chapter_{i}.json");
            if (File.Exists(chapterMapPath))
            {
                File.Delete(chapterMapPath);
                Debug.Log($"[MENU] Удален старый ландшафт главы {i}: {chapterMapPath}");
            }
        }

        var mapManager = FindObjectOfType<StoryMapManager>();
        if (mapManager != null)
        {
            mapManager.DeleteSave();
            var gen = FindObjectOfType<MapGenerator3D>();
            if (gen != null)
            {
                gen.DeleteAllChapterMaps(); 
            }
        }

        // Инициализируем данные игрока с нуля
        if (PlayerProgressionManager.Instance != null)
        {
            PlayerProgressionManager.Instance.StartNewGame();
        }

        // Загружаем сцену сюжета
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