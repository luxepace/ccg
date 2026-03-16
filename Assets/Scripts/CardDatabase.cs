using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "CardDatabase", menuName = "Card/Card Database", order = 0)]
public class CardDatabase : ScriptableObject
{

    [Header("Карты на поле")]
    public List<Card> fieldCards = new List<Card>();

    [Header("Спеллы")]
    public List<SpellCard> spells = new List<SpellCard>();

    /// <summary>
    /// Загружает карты из JSON-файла
    /// </summary>
    public void LoadFromJSON(string fileName)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(filePath))
        {
            Debug.LogError($"Файл {filePath} не найден. Убедитесь, что он находится в папке StreamingAssets.");
            return;
        }

        string json = File.ReadAllText(filePath);
        CardDatabaseJSON loadedData = JsonUtility.FromJson<CardDatabaseJSON>(json);

        fieldCards.Clear();
        foreach (var m in loadedData.fieldCards)
        {
            fieldCards.Add(new Card(
                name: m.name,
                logoPath: m.logoPath,
                attack: m.attack,
                defense: m.defense,
                manacost: m.manacost
            )
            {
                Abilities = ParseAbilities(m.abilities),
                abilityValue = m.abilityValue,
                shortDescription = m.shortDescription,
                fullDescription = m.fullDescription
            });
        }

        spells.Clear();
        foreach (var s in loadedData.spells)
        {
            spells.Add(new SpellCard(
                name: s.name,
                logoPath: s.logoPath,
                manacost: s.manacost,
                spellType: ParseSpellType(s.spellType),
                spellValue: s.spellValue,
                targetType: ParseTargetType(s.targetType)
            )
            {
                Abilities = ParseAbilities(s.abilities),
                shortDescription = s.shortDescription,
                fullDescription = s.fullDescription
            });
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"Загружено {fieldCards.Count} карт и {spells.Count} спеллов");
    }

    /// <summary>
    /// Сохраняет текущие карты и спеллы в JSON-файл
    /// </summary>
    public void SaveToJSON(string fileName)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

        var databaseData = new CardDatabaseJSON();

        foreach (var card in fieldCards)
        {
            databaseData.fieldCards.Add(new FieldCardJSON
            {
                name = card.Name,
                logoPath = card.Logo != null ? $"Sprites/Cards/{card.Logo.name}" : "",
                attack = card.Attack,
                defense = card.Defense,
                manacost = card.Manacost,
                abilityValue = card.abilityValue,
                abilities = GetStringAbilities(card.Abilities),
                shortDescription = card.shortDescription,
                fullDescription = card.fullDescription
            });
        }

        foreach (var spell in spells)
        {
            databaseData.spells.Add(new SpellJSON
            {
                name = spell.Name,
                logoPath = spell.Logo != null ? $"Sprites/Spells/{spell.Logo.name}" : "",
                manacost = spell.Manacost,
                spellType = spell.Spell.ToString(),
                targetType = spell.SpellTarget.ToString(),
                spellValue = spell.SpellValue,
                abilities = GetStringAbilities(spell.Abilities),
                shortDescription = spell.shortDescription,
                fullDescription = spell.fullDescription
            });
        }

        string json = JsonUtility.ToJson(databaseData, true);
        File.WriteAllText(filePath, json);
        Debug.Log($"Карты сохранены в {filePath}");
    }

    // --- Вспомогательные методы ---

    private List<string> GetStringAbilities(List<Card.AbilityType> abilities)
    {
        List<string> result = new List<string>();
        foreach (var ability in abilities)
        {
            result.Add(ability.ToString());
        }
        return result;
    }

    private List<Card.AbilityType> ParseAbilities(List<string> abilityStrings)
    {
        List<Card.AbilityType> abilities = new List<Card.AbilityType>();
        foreach (string s in abilityStrings)
        {
            if (Enum.TryParse<Card.AbilityType>(s, out var type) && type != Card.AbilityType.NO_ABILITY)
            {
                abilities.Add(type);
            }
        }
        return abilities;
    }

    private SpellCard.SpellType ParseSpellType(string value)
    {
        if (Enum.TryParse<SpellCard.SpellType>(value, out var type))
        {
            return type;
        }
        return SpellCard.SpellType.NO_SPELL;
    }

    private SpellCard.TargetType ParseTargetType(string value)
    {
        if (Enum.TryParse<SpellCard.TargetType>(value, out var type))
        {
            return type;
        }
        return SpellCard.TargetType.NO_TARGET;
    }
}

// --- Вспомогательные классы для JSON ---
[System.Serializable]
public class CardDatabaseJSON
{
    public List<FieldCardJSON> fieldCards = new List<FieldCardJSON>();
    public List<SpellJSON> spells = new List<SpellJSON>();
}

[System.Serializable]
public class FieldCardJSON
{
    public string name;
    public string logoPath;
    public int attack;
    public int defense;
    public int manacost;
    public int abilityValue;
    public List<string> abilities;
    public string shortDescription;
    public string fullDescription;
}

[System.Serializable]
public class SpellJSON
{
    public string name;
    public string logoPath;
    public int manacost;
    public string spellType;
    public string targetType;
    public int spellValue;
    public List<string> abilities;
    public string shortDescription;
    public string fullDescription;
}