// В файле MainMenuManager.cs

using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    private void Start()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void StartGame()
    {
        // ЗАГРУЗКА СЦЕНЫ КАРТЫ СЮЖЕТА
        SceneManager.LoadScene("StoryScene");
    }

    public void StartQuickGame()
    {
        // НАСТРОЙКА БЫСТРОЙ ИГРЫ
        TempData.IsStoryMode = false; // Режим быстрой игры
        TempData.CurrentEnemy = null; // Очищаем данные врага, чтобы GameManager создал случайного

        // Сразу загружаем сцену боя
        SceneManager.LoadScene("CardGame");
    }

    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void ExitGame()
    {
        Debug.Log("Выход из игры");
        Application.Quit();
    }
}