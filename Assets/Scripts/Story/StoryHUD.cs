using UnityEngine;
using TMPro;

public class StoryHUD : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI textHP;
    public TextMeshProUGUI textGold;

    private void Start()
    {
        UpdateUI();
    }

    private void Update()
    {
        // Обновляем каждый кадр, чтобы видеть изменения сразу
        UpdateUI();
    }

    void UpdateUI()
    {
        // ПРОВЕРКА: Используем PlayerProgressionManager вместо PlayerStats
        if (PlayerProgressionManager.Instance == null)
        {
            // Если менеджера нет (например, в редакторе до старта), можно скрыть UI или поставить заглушку
            if (textHP) textHP.text = "HP: --";
            if (textGold) textGold.text = "Gold: --";
            return;
        }

        if (textHP != null)
        {
            // Берем данные из нового менеджера
            textHP.text = $"HP: {PlayerProgressionManager.Instance.CurrentHp} / {PlayerProgressionManager.Instance.MaxHp}";
        }

        if (textGold != null)
        {
            textGold.text = $"Gold: {PlayerProgressionManager.Instance.Gold}";
        }
    }
}