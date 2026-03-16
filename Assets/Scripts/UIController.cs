using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // Нужно для загрузки сцен

public class UIController : MonoBehaviour
{
    public static UIController Instance;
    public TextMeshProUGUI PlayerMana, EnemyMana;
    public TextMeshProUGUI PlayerHP, EnemyHP;

    public GameObject ResultGO;
    public TextMeshProUGUI ResultTxt;

    public TextMeshProUGUI TurnTime;
    public Button EndTurnBtn;

    // НОВЫЕ ПОЛЯ ДЛЯ КНОПОК
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
        if (EndTurnBtn.interactable == false)
            EndTurnBtn.interactable = true;

        ResultGO.SetActive(false);
        UpdateHPAndMana();

        // Настраиваем кнопки при старте боя (скрываем их до конца боя)
        if (BtnContinue != null) BtnContinue.gameObject.SetActive(false);
        if (BtnMainMenu != null) BtnMainMenu.gameObject.SetActive(false);
    }

    public void UpdateHPAndMana()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentGame != null)
        {
            PlayerMana.text = GameManager.Instance.CurrentGame.Player.Mana.ToString();
            EnemyMana.text = GameManager.Instance.CurrentGame.Enemy.Mana.ToString();
            PlayerHP.text = GameManager.Instance.CurrentGame.Player.HP.ToString();
            EnemyHP.text = GameManager.Instance.CurrentGame.Enemy.HP.ToString();
        }
    }

    public void ShowResult()
    {
        ResultGO.SetActive(true);
        Time.timeScale = 0f; // Пауза

        bool isWin = GameManager.Instance.CurrentGame.Enemy.HP == 0;

        if (isWin)
            ResultTxt.text = "ПОБЕДА!";
        else
            ResultTxt.text = "ПОРАЖЕНИЕ";

        // Показываем кнопки и настраиваем их
        if (BtnContinue != null)
        {
            BtnContinue.gameObject.SetActive(true);

            if (TempData.IsStoryMode)
            {
                // Если сюжетный режим
                if (isWin)
                {
                    BtnContinue.GetComponentInChildren<TextMeshProUGUI>().text = "Продолжить путь";
                    BtnContinue.onClick.RemoveAllListeners();
                    BtnContinue.onClick.AddListener(ReturnToStoryMap);
                }
                else
                {
                    // При поражении в сюжете - рестарт главы или возврат к началу
                    BtnContinue.GetComponentInChildren<TextMeshProUGUI>().text = "Попробовать снова";
                    BtnContinue.onClick.RemoveAllListeners();
                    BtnContinue.onClick.AddListener(RestartCurrentBattle);
                }
            }
            else
            {
                // Если быстрая игра
                BtnContinue.GetComponentInChildren<TextMeshProUGUI>().text = "Играть снова";
                BtnContinue.onClick.RemoveAllListeners();
                BtnContinue.onClick.AddListener(RestartQuickGame);
            }
        }

        if (BtnMainMenu != null)
        {
            BtnMainMenu.gameObject.SetActive(true);
            BtnMainMenu.onClick.RemoveAllListeners();
            BtnMainMenu.onClick.AddListener(GoToMainMenu);
        }
    }

    // --- МЕТОДЫ ДЕЙСТВИЙ ---

    void ReturnToStoryMap()
    {
        Time.timeScale = 1f;

        // Если победили - сюжетный герой остается живым (HP мы лечим перед следующим боем)
        // Если нужно сохранить остаток HP (если уберешь авто-лечение), то:
        if (GameManager.Instance != null && PlayerStats.Instance != null)
        {
            // PlayerStats.Instance.CurrentHealth = GameManager.Instance.CurrentGame.Player.HP; 
            // Но пока у нас логика "полное лечение перед боем", так что тут ничего делать не надо.
        }

        TempData.CurrentEnemy = null;
        TempData.IsStoryMode = false;
        SceneManager.LoadScene("StoryScene");
    }  

    void RestartCurrentBattle()
    {
        Time.timeScale = 1f;
        // Просто перезапускаем текущую сцену боя (для попытки снова после поражения)
        SceneManager.LoadScene("CardGame");
    }

    void RestartQuickGame()
    {
        Time.timeScale = 1f;
        // Перезапуск быстрой игры (с новым рандомным врагом)
        TempData.CurrentEnemy = null; // Сброс врага
        SceneManager.LoadScene("CardGame");
    }

    void GoToMainMenu()
    {
        Time.timeScale = 1f;
        // Очистка временных данных
        TempData.CurrentEnemy = null;
        TempData.CurrentEvent = null;
        TempData.CurrentNode = null;
        // Можно сбросить флаг, но он перепишется при следующем входе

        SceneManager.LoadScene("MainMenu");
    }

    public void UpdateTurnTime(int time)
    {
        TurnTime.text = time.ToString();
    }


    public void OnOffTurnBtn()
    {
        EndTurnBtn.interactable = GameManager.Instance.IsPlayerTurn;
    }
}