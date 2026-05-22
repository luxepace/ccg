using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardSlotUI : MonoBehaviour
{
    [Header("UI References")]
    public Image cardImage;
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI manaCostText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI defenseText;
    public TextMeshProUGUI countText;

    [Header("Count UI")]
    public GameObject countCircle; // <-- Добавьте сюда ссылку на кружок из префаба

    [Header("Buttons")]
    public Button actionButton;
    public TextMeshProUGUI actionButtonText;

    private Card cardData;
    private DeckBuilderUI deckBuilder;
    private bool isAvailable;

    public void Init(Card card, bool available, DeckBuilderUI builder)
    {
        cardData = card;
        isAvailable = available;
        deckBuilder = builder;

        LoadCardData();
        SetupButton();

        // Включаем кружок только для карт в списке "Доступные"
        if (countCircle != null)
        {
            countCircle.SetActive(isAvailable);
        }
    }

    void LoadCardData()
    {
        if (cardData == null) return;

        if (cardImage != null)
        {
            if (cardData.Logo != null)
            {
                cardImage.sprite = cardData.Logo;
                cardImage.preserveAspect = true;
                cardImage.color = Color.white;
            }
            else
            {
                cardImage.sprite = null;
                cardImage.color = new Color(0.5f, 0.5f, 0.5f);
            }
        }

        if (cardNameText != null) cardNameText.text = cardData.Name;
        if (manaCostText != null) manaCostText.text = cardData.Manacost.ToString();

        if (cardData.IsSpell)
        {
            if (attackText != null) attackText.gameObject.SetActive(false);
            if (defenseText != null) defenseText.gameObject.SetActive(false);
        }
        else
        {
            if (attackText != null)
            {
                attackText.gameObject.SetActive(true);
                attackText.text = cardData.Attack.ToString();
            }
            if (defenseText != null)
            {
                defenseText.gameObject.SetActive(true);
                defenseText.text = cardData.Defense.ToString();
            }
        }

        // Показываем текст количества только для доступных карт
        if (countText != null && isAvailable && PlayerProgressionManager.Instance != null)
        {
            int count = PlayerProgressionManager.Instance.GetAvailableCount(cardData.Name);
            countText.text = $"x{count}";
            countText.gameObject.SetActive(count > 0);
        }
        else if (countText != null)
        {
            countText.gameObject.SetActive(false);
        }
    }

    void SetupButton()
    {
        if (actionButton == null) return;
        actionButton.onClick.RemoveAllListeners();

        if (isAvailable)
        {
            if (actionButtonText != null) actionButtonText.text = "Добавить";
            actionButton.onClick.AddListener(() => deckBuilder.AddCardToDeck(cardData.Name));
        }
        else
        {
            if (actionButtonText != null) actionButtonText.text = "Удалить";
            actionButton.onClick.AddListener(() => deckBuilder.RemoveCardFromDeck(cardData.Name));
        }
    }
}