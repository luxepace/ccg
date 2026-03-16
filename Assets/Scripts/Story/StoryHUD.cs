using UnityEngine;
using TMPro;

public class StoryHUD : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI textHP;
    public TextMeshProUGUI textGold;

    private void Start()
    {
        // Проверяем наличие глобального менеджера игрока
        if (PlayerStats.Instance == null)
        {
            Debug.LogError("[StoryHUD] PlayerStats не найден в сцене!");
            return;
        }

        // Первоначальное обновление
        UpdateUI();
    }

    private void Update()
    {
        // Обновляем каждый кадр. Для двух строк текста это ничтожно мало ресурсов,
        // зато цифры меняются мгновенно при любом изменении в PlayerStats.
        UpdateUI();
    }

    void UpdateUI()
    {
        if (PlayerStats.Instance == null) return;

        if (textHP != null)
        {
            textHP.text = $"HP: {PlayerStats.Instance.CurrentHealth} / {PlayerStats.Instance.MaxHealth}";
        }

        if (textGold != null)
        {
            textGold.text = $"Gold: {PlayerStats.Instance.Gold}";
        }
    }
}