using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardItemUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;           // Иконка (сердце/монета/карта)
    public TextMeshProUGUI valueText; // Текст количества (+5, +10 и т.д.)

    // Инициализация для HP
    public void SetHP(int amount, string mathSymbol)
    {
        if (iconImage != null)
        {
            // Загружаем спрайт сердца из Resources
            Sprite heartSprite = Resources.Load<Sprite>("Sprites/Rewards/heart_icon");
            if (heartSprite != null)
            {
                iconImage.sprite = heartSprite;
                iconImage.preserveAspect = true;
            }
            else
            {
                Debug.LogWarning("[RewardItemUI] Не найден спрайт сердца: Sprites/Rewards/heart_icon");
            }
        }

        if (valueText != null)
        {
            valueText.text = $"{mathSymbol}{amount}";
            valueText.color = Color.red;
        }

    }

    // Инициализация для Gold
    public void SetGold(int amount, string mathSymbol)
    {
        if (iconImage != null)
        {
            // Загружаем спрайт монеты из Resources
            Sprite goldSprite = Resources.Load<Sprite>("Sprites/Rewards/gold_icon");
            if (goldSprite != null)
            {
                iconImage.sprite = goldSprite;
                iconImage.preserveAspect = true;
            }
            else
            {
                Debug.LogWarning("[RewardItemUI] Не найден спрайт монеты: Sprites/Rewards/gold_icon");
            }
        }

        if (valueText != null)
        {
            valueText.text = $"{mathSymbol}{amount}";
            valueText.color = new Color(1f, 0.84f, 0f); // Золотой цвет
        }

    }

    // Инициализация для карты
    public void SetCard(Card cardData)
    {
        if (iconImage != null && cardData != null && cardData.Logo != null)
        {
            iconImage.sprite = cardData.Logo;
            iconImage.preserveAspect = true;
        }

        if (valueText != null)
        {
            valueText.text = "x1";
            valueText.color = Color.white;
        }

    }

    // Очистка/сброс
    public void Clear()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
        }

        if (valueText != null)
        {
            valueText.text = "";
        }

    }
}