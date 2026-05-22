using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardDetailPopup : MonoBehaviour
{
    [Header("UI References")]
    public Image cardImage;
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI manaCostText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI defenseText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI abilitiesText;
    public Button closeButton;

    private void Start()
    {
        // Настраиваем кнопку закрытия
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePopup);
        }
    }

    public void FillCardData(Card card)
    {
        if (card == null) return;

        Debug.Log($"[Popup] Заполняем данными: {card.Name}");

        // Название
        if (cardNameText != null)
            cardNameText.text = card.Name;

        // Мана
        if (manaCostText != null)
            manaCostText.text = card.Manacost.ToString();

        // Изображение
        if (cardImage != null && card.Logo != null)
        {
            cardImage.sprite = card.Logo;
            cardImage.preserveAspect = true;
        }

        // Атака / Защита
        if (card.IsSpell)
        {
            if (attackText != null) attackText.text = "-"; ;
            if (defenseText != null) defenseText.text = "-";
        }
        else
        {
            if (attackText != null)
            {
                attackText.gameObject.SetActive(true);
                attackText.text = card.Attack.ToString();
            }
            if (defenseText != null)
            {
                defenseText.gameObject.SetActive(true);
                defenseText.text = card.Defense.ToString();
            }
        }

        // Описание
        if (descriptionText != null)
        {
            descriptionText.text = !string.IsNullOrEmpty(card.fullDescription)
                ? card.fullDescription
                : card.shortDescription;
        }

        // Способности
        if (abilitiesText != null)
        {
            if (card.HasAbility)
            {
                abilitiesText.gameObject.SetActive(true);
                string abilities = string.Join(", ", card.Abilities);
                abilitiesText.text = $"<color=#FFD700>Способность:</color> {abilities}";
            }
            else
            {
                abilitiesText.gameObject.SetActive(false);
            }
        }

        Time.timeScale = 0f; // Пауза
    }

    public void ClosePopup()
    {
        Time.timeScale = 1f;
        Destroy(gameObject); // Просто уничтожаем клон
    }

    public void OnBackgroundClick()
    {
        ClosePopup();
    }
}