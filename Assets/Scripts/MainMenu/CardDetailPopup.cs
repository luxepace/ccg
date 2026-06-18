using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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

        // Способности (для существ)
        if (abilitiesText != null)
        {
            if (card.IsSpell)
            {
                // Для заклинаний показываем тип заклинания
                abilitiesText.gameObject.SetActive(true);
                string spellTypeName = GetSpellTypeNameRussian(((SpellCard)card).Spell);
                abilitiesText.text = $"<color=#FFD700>Тип: </color>{spellTypeName}";
            }
            else if (card.HasAbility)
            {
                // Для существ показываем способности
                abilitiesText.gameObject.SetActive(true);
                List<string> abilityNames = new List<string>();
                foreach (var ability in card.Abilities)
                {
                    abilityNames.Add(GetAbilityNameRussian(ability));
                }
                string abilities = string.Join(", ", abilityNames);
                abilitiesText.text = $"<color=#FFD700>Способность: </color>{abilities}";
            }
            else
            {
                abilitiesText.gameObject.SetActive(false);
            }
        }

        Time.timeScale = 0f; // Пауза
    }

    string GetSpellTypeNameRussian(SpellCard.SpellType spellType)
    {
        switch (spellType)
        {
            case SpellCard.SpellType.HEAL_ALLY_FIELD_CARDS:
                return "Лечение всех союзников";
            case SpellCard.SpellType.DAMAGE_ENEMY_FIELD_CARDS:
                return "Урон всем врагам";
            case SpellCard.SpellType.HEAL_ALLY_HERO:
                return "Лечение героя";
            case SpellCard.SpellType.DAMAGE_ENEMY_HERO:
                return "Урон герою противника";
            case SpellCard.SpellType.HEAL_ALLY_CARD:
                return "Лечение союзника";
            case SpellCard.SpellType.DAMAGE_ENEMY_CARD:
                return "Урон врагу";
            case SpellCard.SpellType.SHIELD_ON_ALLY_CARD:
                return "Щит союзнику";
            case SpellCard.SpellType.PROVOCATION_ON_ALLY_CARD:
                return "Провокация союзнику";
            case SpellCard.SpellType.BUFF_CARD_DAMAGE:
                return "Усиление урона союзника";
            case SpellCard.SpellType.DEBUFF_CARD_DAMAGE:
                return "Снижение урона врага";
            default:
                return spellType.ToString();
        }
    }

    string GetAbilityNameRussian(Card.AbilityType ability)
    {
        switch (ability)
        {
            case Card.AbilityType.INSTANT_ACTIVE:
                return "Мгновенная активация";
            case Card.AbilityType.DOUBLE_ATTACK:
                return "Двойная атака";
            case Card.AbilityType.SHIELD:
                return "Щит";
            case Card.AbilityType.PROVOCATION:
                return "Провокация";
            case Card.AbilityType.REGENERATION_EACH_TURN:
                return "Постепенная регенерация";
            case Card.AbilityType.COUNTER_ATTACK:
                return "Контратака";
            default:
                return ability.ToString();
        }
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