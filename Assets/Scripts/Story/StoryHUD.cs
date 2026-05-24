using UnityEngine;
using TMPro;

public class StoryHUD : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI textHP;
    public TextMeshProUGUI textGold;

    [Header("Deck Builder")] // Новое поле
    public GameObject deckBuilderPanel; // Сюда перетащить DeckBuilderPanel

    private void Start()
    {
        UpdateUI();
    }

    private void Update()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        if (PlayerProgressionManager.Instance == null)
        {
            if (textHP) textHP.text = "HP: --";
            if (textGold) textGold.text = "Gold: --";
            return;
        }

        if (textHP != null)
        {
            textHP.text = $"HP: {PlayerProgressionManager.Instance.MaxHp}";
        }

        if (textGold != null)
        {
            textGold.text = $"Gold: {PlayerProgressionManager.Instance.Gold}";
        }
    }

    // === НОВЫЙ МЕТОД ===
    public void OpenDeckBuilder()
    {
        if (deckBuilderPanel != null)
        {
            deckBuilderPanel.SetActive(true);
        }
    }
}