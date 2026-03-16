using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AISourceData", menuName = "AI/Source Data", order = 0)]
public class AISourceData : ScriptableObject
{
    [System.Serializable]
    public struct AbilityPower
    {
        public Card.AbilityType ability;
        public float basePower;     // Базовое значение > 1
        public float valuePower;    // Усиление на каждую единицу abilityValue
    }

    [System.Serializable]
    public struct SpellPower
    {
        public SpellCard.SpellType spellType;
        public float basePower;     // Базовая сила спелла > 1
        public float valuePower;    // Усиление на каждую единицу spellValue
    }

    [Header("Сила способностей")]
    public List<AbilityPower> abilityPowers = new List<AbilityPower>()
    {
        new AbilityPower { ability = Card.AbilityType.INSTANT_ACTIVE,       basePower = 1.4f, valuePower = 0.2f },
        new AbilityPower { ability = Card.AbilityType.DOUBLE_ATTACK,       basePower = 1.8f, valuePower = 0.3f },
        new AbilityPower { ability = Card.AbilityType.SHIELD,              basePower = 1.5f, valuePower = 0.2f },
        new AbilityPower { ability = Card.AbilityType.PROVOCATION,         basePower = 1.7f, valuePower = 0.1f },
        new AbilityPower { ability = Card.AbilityType.REGENERATION_EACH_TURN, basePower = 1.6f, valuePower = 0.3f },
        new AbilityPower { ability = Card.AbilityType.COUNTER_ATTACK,      basePower = 1.5f, valuePower = 0.4f }
    };

    [Header("Сила спеллов")]
    public List<SpellPower> spellPowers = new List<SpellPower>()
    {
        new SpellPower { spellType = SpellCard.SpellType.HEAL_ALLY_FIELD_CARDS,     basePower = 1.2f, valuePower = 0.2f },
        new SpellPower { spellType = SpellCard.SpellType.DAMAGE_ENEMY_FIELD_CARDS,   basePower = 2.0f, valuePower = 0.5f },
        new SpellPower { spellType = SpellCard.SpellType.HEAL_ALLY_HERO,            basePower = 1.3f, valuePower = 0.1f },
        new SpellPower { spellType = SpellCard.SpellType.DAMAGE_ENEMY_HERO,          basePower = 1.8f, valuePower = 0.4f },
        new SpellPower { spellType = SpellCard.SpellType.HEAL_ALLY_CARD,            basePower = 1.4f, valuePower = 0.2f },
        new SpellPower { spellType = SpellCard.SpellType.DAMAGE_ENEMY_CARD,         basePower = 1.7f, valuePower = 0.3f },
        new SpellPower { spellType = SpellCard.SpellType.SHIELD_ON_ALLY_CARD,       basePower = 1.5f, valuePower = 0.2f },
        new SpellPower { spellType = SpellCard.SpellType.PROVOCATION_ON_ALLY_CARD,   basePower = 1.6f, valuePower = 0.1f },
        new SpellPower { spellType = SpellCard.SpellType.BUFF_CARD_DAMAGE,          basePower = 1.7f, valuePower = 0.3f },
        new SpellPower { spellType = SpellCard.SpellType.DEBUFF_CARD_DAMAGE,        basePower = 1.5f, valuePower = 0.2f }
    };

    private Dictionary<Card.AbilityType, (float basePower, float valuePower)> abilityMap;
    private Dictionary<SpellCard.SpellType, (float basePower, float valuePower)> spellMap;

    private void OnEnable()
    {
        abilityMap = new Dictionary<Card.AbilityType, (float, float)>();
        foreach (var entry in abilityPowers)
        {
            abilityMap[entry.ability] = (entry.basePower, entry.valuePower);
        }

        spellMap = new Dictionary<SpellCard.SpellType, (float, float)>();
        foreach (var entry in spellPowers)
        {
            spellMap[entry.spellType] = (entry.basePower, entry.valuePower);
        }
    }



    /// <summary>
    /// Возвращает силу способности монстра
    /// </summary>
    public float GetAbilityPower(Card card)
    {
        float totalPower = 1.0f; // Минимальная сила способности

        foreach (var ability in card.Abilities)
        {
            if (abilityMap.TryGetValue(ability, out var power))
            {
                totalPower += power.basePower + card.abilityValue * power.valuePower;
            }
        }

        return totalPower;
    }

    /// <summary>
    /// Возвращает силу спелла
    /// </summary>
    public float GetSpellPower(SpellCard spell)
    {
        if (spellMap.TryGetValue(spell.Spell, out var power))
        {
            return power.basePower + spell.SpellValue * power.valuePower;
        }

        return 1.0f; // Минимум, если тип не найден
    }

    /// <summary>
    /// Возвращает общую силу карты (монстр или спелл)
    /// </summary>
    public float GetCardPower(Card card)
    {
        if (card.IsSpell)
        {
            var spell = (SpellCard)card;
            return spell.Manacost * GetSpellPower(spell);
        }

        float abilityPower = GetAbilityPower(card);
        return Mathf.Max(1.0f, card.Attack * abilityPower + card.Defense);
    }
}