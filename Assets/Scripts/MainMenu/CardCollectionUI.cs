using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CardCollectionUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject collectionPanel;
    public Transform cardsGrid;
    public TextMeshProUGUI progressText;
    public GameObject cardPreviewPrefab;

    private void Awake()
    {
        if (collectionPanel != null) collectionPanel.SetActive(false);
    }

    // === ЭТОТ МЕТОД ВЕШАЕМ НА КНОПКУ "ОТКРЫТЬ" В МЕНЮ ===
    public void OpenCollection()
    {
        if (PlayerProgressionManager.Instance == null) return;

        collectionPanel.SetActive(true);
        Time.timeScale = 0f;
        RefreshCollection();
    }

    public void CloseCollection()
    {
        collectionPanel.SetActive(false);

        // Проверяем, активно ли меню паузы, чтобы не снимать игру с паузы случайно
        PauseMenu pauseMenu = FindObjectOfType<PauseMenu>();
        if (pauseMenu != null)
        {
            // Если пауза была активна, оставляем timescale = 0
            // Если нет (например, открыли из главного меню), ставим 1
            Time.timeScale = pauseMenu.IsPaused() ? 0f : 1f;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    void RefreshCollection()
    {
        if (cardsGrid == null) return;

        // === НОВОЕ: Если карты не загружены, загружаем их ===
        if (CardM.AllCards.Count == 0)
        {
            CardDatabase db = ScriptableObject.CreateInstance<CardDatabase>();
            db.LoadFromJSON("cards.json");

            CardM.AllCards = new List<Card>(db.fieldCards);
            CardM.AllCards.AddRange(db.spells);

            Debug.Log($"[Collection] Загружено {CardM.AllCards.Count} карт");
        }
        // ================================================

        // Очищаем сетку
        foreach (Transform child in cardsGrid)
            Destroy(child.gameObject);

        // Обновляем прогресс
        if (progressText != null)
        {
            int unlocked = PlayerProgressionManager.Instance.GetUnlockedCount();
            int total = CardM.AllCards.Count;
            float percent = total > 0 ? (float)unlocked / total * 100f : 0;
            progressText.text = $"Открыто: {unlocked}/{total} ({percent:F1}%)";
        }

        if (CardM.AllCards.Count == 0)
        {
            Debug.LogError("[Collection] CardM.AllCards всё ещё пуст!");
            return;
        }

        // Создаём слоты для ВСЕХ карт
        foreach (var card in CardM.AllCards)
        {
            GameObject preview = Instantiate(cardPreviewPrefab, cardsGrid);
            CollectionCardPreview previewUI = preview.GetComponent<CollectionCardPreview>();

            bool isUnlocked = PlayerProgressionManager.Instance.IsCardUnlocked(card.Name);
            if (previewUI != null) previewUI.Init(card, isUnlocked);
        }
    }

    public static void OpenFromPause()
    {
        CardCollectionUI instance = FindObjectOfType<CardCollectionUI>();

        if (instance == null)
        {
            // Если нет в сцене, создаем из префаба
            GameObject prefab = Resources.Load<GameObject>("UI/CardCollectionPanel");
            if (prefab != null)
            {
                // ИСПРАВЛЕНО: Ищем UI_HUD_Canvas
                GameObject canvas = GameObject.Find("UI_HUD_Canvas") ?? GameObject.Find("MainCanvas") ?? GameObject.Find("Canvas");
                if (canvas != null)
                {
                    GameObject obj = Instantiate(prefab, canvas.transform);
                    instance = obj.GetComponent<CardCollectionUI>();

                    // Растягиваем на весь экран
                    RectTransform rt = instance.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                        rt.SetAsLastSibling();
                    }
                }
            }
        }

        if (instance != null) instance.OpenCollection();
    }
}