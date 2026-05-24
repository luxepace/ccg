using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public class MainMenuManager : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject deckBuilderPanel;  // ← Ссылка на GameObject панели (как settingsPanel)
    public GameObject continueButtonObject;
    public GameObject fastGameSettingsPanel;

    private void Start()
    {
        settingsPanel.SetActive(false);
        if (deckBuilderPanel != null) deckBuilderPanel.SetActive(false);  // ← Скрываем при старте
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

        // !!! УДАЛИТЕ ЭТУ СТРОКУ: PlayerProgressionManager.Instance.DeleteSave(); 
        // Она стирала файл с картами с диска!

        // Удаляем только сохранения карты/сюжета
        string mapPath = Path.Combine(Application.persistentDataPath, "storyMapSave.json");
        if (File.Exists(mapPath))
        {
            File.Delete(mapPath);
            Debug.Log("[MENU] Старое сохранение карты удалено.");
        }

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

        // Запускаем новую игру (с сохранением коллекции карт)
        if (PlayerProgressionManager.Instance != null)
        {
            PlayerProgressionManager.Instance.StartNewGame();
        }

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
        mainMenuPanel.SetActive(false);
        if (fastGameSettingsPanel != null)
        {
            fastGameSettingsPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("FastGameSettingsPanel не назначен в MainMenuManager!");
        }
    }

    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void OpenCardCollection()
    {
        CardCollectionUI collectionUI = FindObjectOfType<CardCollectionUI>();

        if (collectionUI == null)
        {
            GameObject prefab = Resources.Load<GameObject>("UI/CardCollectionPanel");
            if (prefab != null)
            {
                GameObject canvas = GameObject.Find("Canvas") ?? GameObject.Find("MainCanvas");
                if (canvas != null)
                {
                    GameObject obj = Instantiate(prefab, canvas.transform);
                    collectionUI = obj.GetComponent<CardCollectionUI>();
                }
            }
        }

        if (collectionUI != null) collectionUI.OpenCollection();
    }

    public void OpenDeckBuilder()
    {
        if (deckBuilderPanel != null)
        {
            deckBuilderPanel.SetActive(true);  // ← Просто включи панель!
            Time.timeScale = 0f;  // ← Пауза здесь
            Debug.Log("[Menu] Конструктор открыт");
        }
        else
        {
            Debug.LogError("[Menu] deckBuilderPanel не назначен!");
        }
    }
    public void CloseDeckBuilder()
    {
        if (deckBuilderPanel != null)
        {
            deckBuilderPanel.SetActive(false);
            Time.timeScale = 1f;
            Debug.Log("[Menu] Конструктор закрыт");
        }
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