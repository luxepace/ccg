using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIController : MonoBehaviour
{
    public static UIController Instance;

    [Header("UI References")]
    public TextMeshProUGUI PlayerMana;
    public TextMeshProUGUI EnemyMana;
    public TextMeshProUGUI PlayerHP;
    public TextMeshProUGUI EnemyHP;

    public GameObject ResultGO;
    public TextMeshProUGUI ResultTxt;

    public TextMeshProUGUI TurnTime;
    public Button EndTurnBtn;

    [Header("Result Buttons")]
    public Button BtnContinue;
    public Button BtnMainMenu;

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void StartGame()
    {
        if (EndTurnBtn != null && EndTurnBtn.interactable == false)
            EndTurnBtn.interactable = true;

        if (ResultGO != null)
            ResultGO.SetActive(false);

        UpdateHPAndMana();

        // Скрываем кнопки результата при старте боя
        if (BtnContinue != null) BtnContinue.gameObject.SetActive(false);
        if (BtnMainMenu != null) BtnMainMenu.gameObject.SetActive(false);
    }

    public void UpdateHPAndMana()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentGame != null)
        {
            if (PlayerMana != null)
                PlayerMana.text = GameManager.Instance.CurrentGame.Player.Mana.ToString();

            if (EnemyMana != null)
                EnemyMana.text = GameManager.Instance.CurrentGame.Enemy.Mana.ToString();

            if (PlayerHP != null)
                PlayerHP.text = GameManager.Instance.CurrentGame.Player.HP.ToString();

            if (EnemyHP != null)
                EnemyHP.text = GameManager.Instance.CurrentGame.Enemy.HP.ToString();
        }
    }

    public void ShowResult()
    {
        if (ResultGO != null)
        {
            ResultGO.SetActive(true);
        }

        Time.timeScale = 0f; // Пауза игры

        bool isWin = GameManager.Instance.CurrentGame.Enemy.HP <= 0;

        if (ResultTxt != null)
        {
            if (isWin)
                ResultTxt.text = "ПОБЕДА!";
            else
                ResultTxt.text = "ПОРАЖЕНИЕ";
        }

        // Настройка кнопки "Продолжить"
        if (BtnContinue != null)
        {
            BtnContinue.gameObject.SetActive(true);
            TextMeshProUGUI btnText = BtnContinue.GetComponentInChildren<TextMeshProUGUI>();

            if (TempData.IsStoryMode)
            {
                // --- СЮЖЕТНЫЙ РЕЖИМ ---
                if (isWin)
                {
                    if (btnText != null) btnText.text = "Продолжить путь";

                    BtnContinue.onClick.RemoveAllListeners();
                    BtnContinue.onClick.AddListener(ReturnToStoryMap);
                }
                else
                {
                    if (btnText != null) btnText.text = "Попробовать снова";

                    BtnContinue.onClick.RemoveAllListeners();
                    BtnContinue.onClick.AddListener(RestartCurrentBattle);
                }
            }
            else
            {
                // --- БЫСТРАЯ ИГРА / ТЕСТ ---
                if (btnText != null) btnText.text = "Играть снова";

                BtnContinue.onClick.RemoveAllListeners();
                BtnContinue.onClick.AddListener(RestartQuickGame);
            }
        }

        // Настройка кнопки "В главное меню"
        if (BtnMainMenu != null)
        {
            BtnMainMenu.gameObject.SetActive(true);
            BtnMainMenu.onClick.RemoveAllListeners();
            BtnMainMenu.onClick.AddListener(GoToMainMenu);
        }
    }

    // === МЕТОДЫ ДЕЙСТВИЙ ===

    void ReturnToStoryMap()
    {
        Time.timeScale = 1f;

        // Очищаем временные данные боя
        TempData.CurrentEnemy = null;
        TempData.IsStoryMode = false;

        // Загружаем сцену карты
        SceneManager.LoadScene("StoryScene");
    }

    void RestartCurrentBattle()
    {
        Time.timeScale = 1f;
        // Перезапускаем текущую сцену боя (для попытки снова после поражения в сюжете)
        SceneManager.LoadScene("CardGame");
    }

    void RestartQuickGame()
    {
        Time.timeScale = 1f;
        // Сброс врага для новой случайной генерации
        TempData.CurrentEnemy = null;
        SceneManager.LoadScene("CardGame");
    }

    void GoToMainMenu()
    {
        Time.timeScale = 1f;

        // Очистка временных данных
        TempData.CurrentEnemy = null;
        TempData.CurrentEvent = null;
        TempData.CurrentNode = null;
        TempData.IsStoryMode = false;

        SceneManager.LoadScene("MainMenu");
    }

    public void UpdateTurnTime(int time)
    {
        if (TurnTime != null)
            TurnTime.text = time.ToString();
    }

    public void OnOffTurnBtn()
    {
        if (EndTurnBtn != null)
            EndTurnBtn.interactable = GameManager.Instance.IsPlayerTurn;
    }
}