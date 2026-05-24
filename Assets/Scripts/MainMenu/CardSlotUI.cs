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
    public GameObject countCircle; // Кружок с количеством

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

        // По умолчанию включаем кружок только для панели "Доступные"
        if (countCircle != null)
        {
            countCircle.SetActive(isAvailable);
        }

        // Для правой панели (колода) кружок не нужен
        if (!isAvailable && countCircle != null)
        {
            countCircle.SetActive(false);
        }
    }

    // === НОВЫЙ МЕТОД ДЛЯ УПРАВЛЕНИЯ КОЛИЧЕСТВОМ ===
    public void SetCount(int count)
    {
        if (countText != null && countCircle != null)
        {
            if (count > 1)
            {
                countCircle.SetActive(true);
                countText.text = "x" + count.ToString();
                countText.gameObject.SetActive(true);
            }
            else
            {
                // Если карта одна или её нет, полностью скрываем бейджик и текст
                countCircle.SetActive(false);
                countText.gameObject.SetActive(false);
            }
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

        // СТАРАЯ ЛОГИКА ДЛЯ СЮЖЕТНОГО РЕЖИМА (оставляем как фоллбэк)
        // Но в быстрой игре мы будем перезаписывать это через SetCount()
        if (countText != null && isAvailable && PlayerProgressionManager.Instance != null)
        {
            int count = PlayerProgressionManager.Instance.GetAvailableCount(cardData.Name);

            // Если мы НЕ в быстрой игре, используем стандартную логику
            if (!TempData.IsSettingUpFastGame)
            {
                countText.text = $"x{count}";
                countText.gameObject.SetActive(count > 1); // Показываем только если > 1
                if (countCircle != null) countCircle.SetActive(count > 1);
            }
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