using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RPSGame : EventInteractiveBase
{
    [Header("UI References")]
    public Button rockBtn;
    public Button paperBtn;
    public Button scissorsBtn;
    public TextMeshProUGUI resultText;
    public Button continueBtn;

    private string[] choices = { "ROCK", "PAPER", "SCISSORS" };
    private bool isProcessing = false;

    public override void Init(List<ParamPair> parameters)
    {
        isProcessing = false;
        if (continueBtn) continueBtn.gameObject.SetActive(false);
        if (resultText) resultText.text = "";

        if (rockBtn) rockBtn.onClick.AddListener(() => PlayRound("ROCK"));
        if (paperBtn) paperBtn.onClick.AddListener(() => PlayRound("PAPER"));
        if (scissorsBtn) scissorsBtn.onClick.AddListener(() => PlayRound("SCISSORS"));
        if (continueBtn) continueBtn.onClick.AddListener(FinishGame);
    }

    private void PlayRound(string playerChoice)
    {
        if (isProcessing) return;
        isProcessing = true;

        SetInteractable(false);

        string enemyChoice = choices[UnityEngine.Random.Range(0, choices.Length)];

        // Определяем победу
        bool isVictory = false;
        bool isDraw = false;

        if (playerChoice == enemyChoice)
        {
            isDraw = true;
        }
        else if ((playerChoice == "ROCK" && enemyChoice == "SCISSORS") ||
                 (playerChoice == "PAPER" && enemyChoice == "ROCK") ||
                 (playerChoice == "SCISSORS" && enemyChoice == "PAPER"))
        {
            isVictory = true;
        }

        string resultMessage = isDraw ? "Ничья!" : (isVictory ? "Победа!" : "Поражение!");

        if (resultText)
            resultText.text = $"Вы: {playerChoice}\nВраг: {enemyChoice}\n{resultMessage}";

        // Запоминаем результат для кнопки продолжения
        // Используем closure или временную переменную, но проще сохранить в поле класса
        lastRoundVictory = isVictory;
        lastRoundDraw = isDraw;

        if (continueBtn)
            continueBtn.gameObject.SetActive(true);
    }

    private bool lastRoundVictory = false;
    private bool lastRoundDraw = false;

    private void FinishGame()
    {
        // Передаем результат: награду даем только если победа (ничью можно считать как поражение или победу по желанию)
        // Сейчас логика: Награда только за чистую победу.
        bool giveReward = lastRoundVictory;

        Debug.Log($"[RPS] Игра окончена. Победа: {giveReward}");

        FinishMinigame(giveReward);
    }

    private void SetInteractable(bool state)
    {
        if (rockBtn) rockBtn.interactable = state;
        if (paperBtn) paperBtn.interactable = state;
        if (scissorsBtn) scissorsBtn.interactable = state;
    }

    private void OnDestroy()
    {
        if (rockBtn) rockBtn.onClick.RemoveAllListeners();
        if (paperBtn) paperBtn.onClick.RemoveAllListeners();
        if (scissorsBtn) scissorsBtn.onClick.RemoveAllListeners();
        if (continueBtn) continueBtn.onClick.RemoveAllListeners();
    }
}