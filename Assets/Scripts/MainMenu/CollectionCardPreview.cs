using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class CollectionCardPreview : MonoBehaviour
{
    [Header("UI References")]
    public Image cardImage, AttackBack, DefenceBack;
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI manaCostText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI defenseText;
    public TextMeshProUGUI descriptionText;
    public GameObject lockedOverlay;
    public TextMeshProUGUI lockedText;
    public Image cardBackground;

    [Header("Colors")]
    public Color unlockedColor = Color.white;
    public Color lockedColor = new Color(0.3f, 0.3f, 0.3f);
    public Color lockedTextColor = new Color(0.5f, 0.5f, 0.5f);

    private Card cardData;
    private bool isUnlocked;
    private Button cardButton;

    private void Awake()
    {
        // Получаем Button компонент
        cardButton = GetComponent<Button>();

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardClicked);
        }
    }

    public void Init(Card card, bool unlocked)
    {
        cardData = card;
        isUnlocked = unlocked;

        // Фон карточки
        if (cardBackground != null)
        {
            cardBackground.color = isUnlocked ? new Color(1, 1, 1, 0.1f) : new Color(0, 0, 0, 0.5f);
        }

        // Изображение карты
        if (cardImage != null)
        {
            if (isUnlocked && card.Logo != null)
            {
                cardImage.sprite = card.Logo;
                cardImage.preserveAspect = true;
                cardImage.color = unlockedColor;
            }
            else
            {
                cardImage.sprite = null;
                cardImage.color = lockedColor;
            }
        }

        // Название
        if (cardNameText != null)
        {
            cardNameText.text = isUnlocked ? card.Name : "???";
            cardNameText.color = isUnlocked ? Color.white : lockedTextColor;
        }

        // Оверлей замка
        if (lockedOverlay != null)
        {
            lockedOverlay.SetActive(!isUnlocked);
        }

        // Текст "???"
        if (lockedText != null)
        {
            lockedText.gameObject.SetActive(!isUnlocked);
        }

        // Показываем статы только для открытых карт
        bool showStats = isUnlocked;

        if (manaCostText != null)
        {
            manaCostText.gameObject.SetActive(showStats);
            if (showStats) manaCostText.text = card.Manacost.ToString();
        }

        if (descriptionText != null)
        {
            descriptionText.gameObject.SetActive(showStats);
            if (showStats)
            {
                descriptionText.text = cardData.shortDescription;
            }
        }

        // Для спеллов не показываем атаку/защиту
        if (card.IsSpell)
        {
            if (attackText != null) attackText.gameObject.SetActive(false);
            if (attackText != null) AttackBack.gameObject.SetActive(false);
            if (defenseText != null) defenseText.gameObject.SetActive(false);
            if (defenseText != null) DefenceBack.gameObject.SetActive(false);
        }
        else
        {
            if (attackText != null)
            {
                attackText.gameObject.SetActive(showStats);
                if (showStats) attackText.text = card.Attack.ToString();
            }
            if (defenseText != null)
            {
                defenseText.gameObject.SetActive(showStats);
                if (showStats) defenseText.text = card.Defense.ToString();
            }
        }

        // === ВАЖНО: Блокируем клик для закрытых карт ===
        if (cardButton != null)
        {
            cardButton.interactable = isUnlocked;
        }
    }

    void OnCardClicked()
    {
        if (!isUnlocked || cardData == null) return;

        Debug.Log($"[Collection] Клик по карте: {cardData.Name}");

        // Ищем CardDetailPopup в сцене
        CardDetailPopup popup = FindObjectOfType<CardDetailPopup>();

        if (popup == null)
        {
            // Если не нашли, создаём из префаба
            GameObject prefab = Resources.Load<GameObject>("UI/CardDetailPopup");
            if (prefab != null)
            {
                // ИСПРАВЛЕНО: Ищем UI_HUD_Canvas
                GameObject canvas = GameObject.Find("UI_HUD_Canvas") ?? GameObject.Find("MainCanvas") ?? GameObject.Find("Canvas");
                if (canvas != null)
                {
                    GameObject obj = Instantiate(prefab, canvas.transform);
                    popup = obj.GetComponent<CardDetailPopup>();
                    Debug.Log("[Collection] Панель создана успешно");
                }
                else
                {
                    Debug.LogError("[Collection] Canvas не найден!");
                }
            }
            else
            {
                Debug.LogError("[Collection] Префаб UI/CardDetailPopup не найден в Resources!");
            }
        }

        if (popup != null)
        {
            popup.FillCardData(cardData);
        }
    }
}