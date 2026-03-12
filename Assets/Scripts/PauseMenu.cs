using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuUI; // UI меню паузы
    private bool isPaused = false;


    // Метод для переключения состояния паузы
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

    // Приостановить игру
    void PauseGame()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f; // останавливает игру
        isPaused = true;
    }

    // Возобновить игру
    void ResumeGame()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f; // продолжает игру
        isPaused = false;
    }

    // Выход в главное меню
    public void ToMainMenu()
    {
        Time.timeScale = 1f; // обязательно возвращаем нормальное время
        SceneManager.LoadScene("MainMenu"); // сменить на сцену главного меню
    }

    // Выход из игры
    public void ExitGame()
    {
        
        Debug.Log("Выход из игры");
        Application.Quit();
    }
}
