using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class NodeVisualController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("References")]
    public StoryNode NodeData;

    [Header("Icons")]
    public Image iconImage;
    public GameObject lockedIcon;
    public Image progressMark;

    [Header("Tooltip")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipText;

    [Header("Sprites")]
    public Sprite startSprite;
    public Sprite enemySprite;
    public Sprite eventSprite;
    public Sprite bossSprite;
    public Sprite restSprite;
    public Sprite lockedSprite;

    [Header("Colors")]
    public Color lockedColor = Color.gray;
    public Color availableColor = Color.white;
    public Color visitedColor = new Color(0.5f, 0.5f, 0.5f);
    public Color skippedColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    private StoryMapVisual mapVisual;
    private bool isHovering = false;

    public void Init(StoryNode node, StoryMapVisual visual)
    {
        NodeData = node;
        mapVisual = visual;

        SetIconByNodeType();
        UpdateVisualState();

        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    public void SetIconByNodeType()
    {
        if (iconImage == null)
            return;

        Sprite sprite = null;

        switch (NodeData.type)
        {
            case StoryNodeType.START:
                sprite = startSprite;
                break;
            case StoryNodeType.ENEMY:
                sprite = enemySprite;
                break;
            case StoryNodeType.EVENT:
                sprite = eventSprite;
                break;
            case StoryNodeType.BOSS:
                sprite = bossSprite;
                iconImage.transform.localScale = Vector3.one * 1.3f;
                break;
            case StoryNodeType.REST:
                sprite = restSprite;
                break;
        }

        if (sprite != null)
        {
            iconImage.sprite = sprite;
            iconImage.preserveAspect = true;
        }
    }

    public void UpdateVisualState()
    {
        // 1. ПРОВЕРКА: Является ли этот узел тем, на котором сейчас стоит игрок?
        bool isCurrentActiveNode = (StoryMapManager.Instance != null &&
                                    StoryMapManager.Instance.CurrentNode != null &&
                                    StoryMapManager.Instance.CurrentNode.nodeId == NodeData.nodeId);

        if (lockedIcon != null)
        {
            // Замок показываем, только если узел НЕ разблокирован И это НЕ пропущенный узел
            lockedIcon.SetActive(!NodeData.isUnlocked && !NodeData.isSkipped);
        }

        if (iconImage != null)
        {
            // Сброс масштаба перед применением новых значений
            iconImage.transform.localScale = Vector3.one;

            if (NodeData.isSkipped)
            {
                // ПРОПУЩЕННЫЙ УЗЕЛ: Полупрозрачный, обычная иконка, маленький размер
                SetIconByNodeType();
                iconImage.color = skippedColor;
                iconImage.transform.localScale = Vector3.one * 0.8f;
            }
            else if (!NodeData.isUnlocked)
            {
                // ЗАБЛОКИРОВАННЫЙ (будущий): Серый, иконка замка
                iconImage.sprite = lockedSprite;
                iconImage.color = lockedColor;
                iconImage.transform.localScale = Vector3.one;
            }
            else if (isCurrentActiveNode)
            {
                // ТЕКУЩИЙ АКТИВНЫЙ УЗЕЛ (на котором стоит игрок): 
                // Самый яркий, увеличенный, без галочки прогресса
                SetIconByNodeType();

                // Делаем цвет ярче доступного (можно добавить немного белого или просто оставить availableColor, но увеличить масштаб)
                iconImage.color = Color.Lerp(availableColor, Color.white, 0.3f);
                iconImage.transform.localScale = Vector3.one * 1.4f; // Заметно больше остальных

                // Убираем галочку прогресса, так как мы еще на этом узле
                if (progressMark != null) progressMark.gameObject.SetActive(false);
            }
            else if (NodeData.isVisited)
            {
                // ПОСЕЩЕННЫЙ (пройденный ранее): Темный, обычная иконка, галочка
                SetIconByNodeType();
                iconImage.color = visitedColor; // Темно-серый
                iconImage.transform.localScale = Vector3.one;

                if (progressMark != null) progressMark.gameObject.SetActive(true);
            }
            else
            {
                // ДОСТУПНЫЙ ДЛЯ ВЫБОРА (следующий шаг): Обычный яркий цвет
                SetIconByNodeType();
                iconImage.color = availableColor;
                iconImage.transform.localScale = Vector3.one;

                if (progressMark != null) progressMark.gameObject.SetActive(false);
            }
        }

        // Обновляем текст подсказки (на всякий случай)
        UpdateTooltipText();
    }

    void UpdateTooltipText()
    {
        if (tooltipText == null)
            return;

        string text = " ";

        switch (NodeData.type)
        {
            case StoryNodeType.START:
                text = "Начало пути";
                break;
            case StoryNodeType.ENEMY:
                if (!string.IsNullOrEmpty(NodeData.enemyId))
                {
                    EnemyData enemy = StoryContentLoader.GetEnemyById(NodeData.enemyId);
                    text = enemy != null ? enemy.enemyName : "Враг";
                }
                else
                {
                    text = "Враг";
                }
                break;
            case StoryNodeType.EVENT:
                if (!string.IsNullOrEmpty(NodeData.eventId))
                {
                    StoryEventData eventData = StoryContentLoader.GetEventById(NodeData.eventId);
                    text = eventData != null ? eventData.eventTitle : "Событие";
                }
                else
                {
                    text = "Событие";
                }
                break;
            case StoryNodeType.BOSS:
                if (!string.IsNullOrEmpty(NodeData.enemyId))
                {
                    EnemyData boss = StoryContentLoader.GetEnemyById(NodeData.enemyId);
                    text = boss != null ? "БОСС: " + boss.enemyName : "БОСС";
                }
                else
                {
                    text = "БОСС";
                }
                break;
            case StoryNodeType.REST:
                text = "Отдых";
                break;
        }

        tooltipText.text = text;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        UpdateTooltipText();
        if (NodeData.isUnlocked && tooltipPanel != null)
        {
            tooltipPanel.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[UI] Клик получен контроллером узла {NodeData.nodeId}");

        // Убедитесь, что эта проверка не блокирует клик ошибочно
        if (NodeData == null)
        {
            Debug.LogError("[UI] NodeData не инициализирован!");
            return;
        }

        // Передаем клик дальше в StoryMapVisual
        if (mapVisual != null)
        {
            mapVisual.OnNodeClicked(NodeData);
        }
        else
        {
            Debug.LogError("[UI] mapVisual не назначен в контроллере узла!");
        }
    }
}

