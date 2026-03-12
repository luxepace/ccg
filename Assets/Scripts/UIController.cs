using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class UIController : MonoBehaviour
{
    public static UIController Instance;

    public TextMeshProUGUI PlayerMana, EnemyMana;
    public TextMeshProUGUI PlayerHP, EnemyHP;

    public GameObject ResultGO;
    public TextMeshProUGUI ResultTxt;

    public TextMeshProUGUI TurnTime;
    public Button EndTurnBtn;

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
    }

    public void UpdateHPAndMana()
    {
        PlayerMana.text = GameManager.Instance.CurrentGame.Player.Mana.ToString();
        EnemyMana.text = GameManager.Instance.CurrentGame.Enemy.Mana.ToString();
        PlayerHP.text = GameManager.Instance.CurrentGame.Player.HP.ToString();
        EnemyHP.text = GameManager.Instance.CurrentGame.Enemy.HP.ToString();
    }

    public void ShowResult()
    {
        ResultGO.SetActive(true);
        Time.timeScale = 0f;
        if (GameManager.Instance.CurrentGame.Enemy.HP == 0)
            ResultTxt.text = "WIN";
        else
            ResultTxt.text = "LOSE";
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