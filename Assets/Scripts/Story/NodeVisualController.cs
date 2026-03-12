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
        if (lockedIcon != null)
        {
            lockedIcon.SetActive(!NodeData.isUnlocked && !NodeData.isSkipped);
        }

        if (iconImage != null)
        {
            if (NodeData.isSkipped)
            {
                iconImage.color = skippedColor;
                SetIconByNodeType();
            }
            else if (!NodeData.isUnlocked)
            {
                iconImage.sprite = lockedSprite;
                iconImage.color = lockedColor;
            }
            else if (NodeData.isVisited)
            {
                iconImage.color = visitedColor;
            }
            else
            {
                iconImage.color = availableColor;
            }
        }

        if (progressMark != null)
        {
            progressMark.gameObject.SetActive(NodeData.isVisited);
        }

        UpdateTooltipText();
    }

    void UpdateTooltipText()
    {
        if (tooltipText == null)
            return;

        string text = "";

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
        if (!NodeData.isUnlocked || NodeData.isVisited)
            return;

        mapVisual.OnNodeClicked(NodeData);
    }
}

public static class TempData
{
    public static EnemyData CurrentEnemy;
    public static StoryEventData CurrentEvent;
    public static StoryNode CurrentNode;
}