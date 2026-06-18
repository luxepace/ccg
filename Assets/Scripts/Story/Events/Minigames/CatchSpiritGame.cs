using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CatchSpiritGame : EventInteractiveBase
{
    [Header("UI References")]
    public RectTransform playArea;
    public GameObject spiritPrefab;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreText;
    public Button continueBtn;

    private GameObject currentSpirit;
    private float timeLeft = 3f;
    private int clicksNeeded = 3;
    private int currentClicks = 0;
    private bool isGameActive = false;

    public override void Init(List<ParamPair> parameters)
    {
        // Сброс переменных
        timeLeft = 3f;
        currentClicks = 0;
        isGameActive = true;

        if (continueBtn) continueBtn.gameObject.SetActive(false);
        UpdateUI();

        // Удаляем старого духа если есть
        if (currentSpirit != null) Destroy(currentSpirit);

        // Спавним первого духа
        SpawnSpirit();

        // Запускаем корутину таймера
        StartCoroutine(GameLoop());
    }

    private void UpdateUI()
    {
        if (timerText) timerText.text = $"Время: {timeLeft:F1}";
        if (scoreText) scoreText.text = $"Осталось поймать: {clicksNeeded - currentClicks}";
    }

    private void SpawnSpirit()
    {
        if (!isGameActive) return;

        if (currentSpirit != null) Destroy(currentSpirit);

        currentSpirit = Instantiate(spiritPrefab, playArea);
        currentSpirit.transform.localPosition = GetRandomPositionInRect(playArea);

        // Добавляем обработчик клика
        Button btn = currentSpirit.GetComponent<Button>();
        if (btn == null)
        {
            btn = currentSpirit.AddComponent<Button>();
            Image img = currentSpirit.GetComponent<Image>();
            if (img) btn.targetGraphic = img;
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnSpiritClicked);
    }

    private Vector2 GetRandomPositionInRect(RectTransform rect)
    {
        float padding = 20f;
        float x = Random.Range(rect.rect.x + padding, rect.rect.xMax - padding);
        float y = Random.Range(rect.rect.y + padding, rect.rect.yMax - padding);
        return new Vector2(x, y);
    }

    private void OnSpiritClicked()
    {
        if (!isGameActive) return;

        currentClicks++;
        UpdateUI();

        if (currentClicks >= clicksNeeded)
        {
            WinGame();
        }
        else
        {
            SpawnSpirit();
        }
    }

    // === ИСПРАВЛЕНИЕ ТАЙМЕРА ===
    private System.Collections.IEnumerator GameLoop()
    {
        while (isGameActive && timeLeft > 0)
        {
            // Используем WaitForSecondsRealtime, так как Time.timeScale может быть 0
            yield return new WaitForSecondsRealtime(0.1f);

            timeLeft -= 0.1f;
            UpdateUI();

            if (timeLeft <= 0)
            {
                LoseGame();
            }
        }
    }

    private void WinGame()
    {
        isGameActive = false;
        if (timerText) timerText.text = "ПОБЕДА!";
        if (currentSpirit) Destroy(currentSpirit);

        if (continueBtn)
        {
            continueBtn.gameObject.SetActive(true);
            continueBtn.onClick.RemoveAllListeners();
            continueBtn.onClick.AddListener(() => FinishMinigame(true));
        }
    }

    private void LoseGame()
    {
        isGameActive = false;
        if (timerText) timerText.text = "ВРЕМЯ ВЫШЛО!";
        if (currentSpirit) Destroy(currentSpirit);

        if (continueBtn)
        {
            continueBtn.gameObject.SetActive(true);
            continueBtn.onClick.RemoveAllListeners();
            continueBtn.onClick.AddListener(() => FinishMinigame(false));
        }
    }
}