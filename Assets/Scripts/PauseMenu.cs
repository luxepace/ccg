using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuUI; // Сюда перетащи панель меню из UI_HUD_Canvas
    private bool isPaused = false;

    void Update()
    {
        // ЭТА ПРОВЕРКА РАБОТАЕТ ТОЛЬКО ЗДЕСЬ (в Update)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    // Этот метод теперь вызывает и Update (при Esc), и Кнопка (при клике)
    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    // Метод ТОЛЬКО для кнопки "Пауза" в интерфейсе
    // В инспекторе кнопки на событие OnClick() повесь именно этот метод!
    public void OnPauseButtonClicked()
    {
        // Если игра уже на паузе (например, нажали Esc), то кнопка должна закрывать меню
        // Но обычно кнопка "Пауза" нужна только чтобы открыть её.
        // Логичнее сделать так:
        if (!isPaused)
        {
            PauseGame();
        }
        else
        {
            // Если вдруг нажали кнопку, когда меню открыто - закрываем
            ResumeGame();
        }
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

    public void ToMainMenu()
    {
        Time.timeScale = 1f;
        // Сохранение прогресса перед выходом (опционально)
        // StoryMapManager.Instance.SaveMap(); 

        SceneManager.LoadScene("MainMenu");
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
        Debug.Log("Выход из игры");
        Application.Quit();
    }
}