using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pauseMenuUI;

    private bool isPaused = false;

    // Геттер для проверки состояния паузы (нужен CardCollectionUI, чтобы не снимать паузу случайно)
    public bool IsPaused() => isPaused;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void OnPauseButtonClicked()
    {
        if (!isPaused)
            PauseGame();
        else
            ResumeGame();
    }

    void PauseGame()
    {
        if (pauseMenuUI == null)
        {
            Debug.LogError("PauseMenuUI не назначен в инспекторе!");
            return;
        }

        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
        Debug.Log("[PAUSE] Игра приостановлена.");
    }

    void ResumeGame()
    {
        if (pauseMenuUI == null) return;

        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        Debug.Log("[PAUSE] Игра возобновлена.");
    }

    // === Открыть коллекцию карт из паузы ===
    public void OpenCardCollection()
    {
        // Статический метод сам найдёт панель или создаст её из префаба
        CardCollectionUI.OpenFromPause();
    }

    public void ToMainMenu()
    {
        Time.timeScale = 1f;

        // Полная очистка временного состояния
        TempData.CurrentEnemy = null;
        TempData.CurrentEvent = null;
        TempData.CurrentNode = null;
        TempData.IsStoryMode = false;
        TempData.BossDefeated = false;
        TempData.RewardCards.Clear();

        SceneManager.LoadScene("MainMenu");
    }

    // === Выход из приложения ===
    public void ExitGame()
    {
        Time.timeScale = 1f;
        Debug.Log("Выход из игры");
        Application.Quit();
    }
}