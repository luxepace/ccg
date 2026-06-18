using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI;

public class CardMovement : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CardController CC;
    Camera MainCamera;
    Vector3 offset;
    public Transform DefaultParent, DefaultTempCardParent;
    GameObject TempCardGO;
    public bool IsDraggable;
    int startID;

    void Awake()
    {
        MainCamera = Camera.allCameras[0];
        TempCardGO = GameObject.Find("TempCardGO");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        offset = transform.position - MainCamera.ScreenToWorldPoint(eventData.position);

        DefaultParent = DefaultTempCardParent = transform.parent;

        IsDraggable = GameManager.Instance.IsPlayerTurn &&
                     (
                         (DefaultParent.GetComponent<DropPlace>().Type == FieldType.SELF_HAND &&
                          GameManager.Instance.CurrentGame.Player.Mana >= CC.Card.Manacost) ||
                         (DefaultParent.GetComponent<DropPlace>().Type == FieldType.SELF_FIELD &&
                          CC.Card.CanAttack)
                     );

        if (!IsDraggable)
            return;

        startID = transform.GetSiblingIndex();

        if (CC.Card.IsSpell || CC.Card.CanAttack)
            GameManager.Instance.HighlightTargets(CC, true);

        TempCardGO.transform.SetParent(DefaultParent);
        TempCardGO.transform.SetSiblingIndex(transform.GetSiblingIndex());

        transform.SetParent(DefaultParent.parent);
        GetComponent<CanvasGroup>().blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsDraggable)
            return;

        Vector3 newPos = MainCamera.ScreenToWorldPoint(eventData.position);
        transform.position = newPos + offset;

        if (!CC.Card.IsSpell)
        {
            if (TempCardGO.transform.parent != DefaultTempCardParent)
                TempCardGO.transform.SetParent(DefaultTempCardParent);

            if (DefaultParent.GetComponent<DropPlace>().Type != FieldType.SELF_FIELD)
                CheckPosition();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsDraggable)
            return;

        GameManager.Instance.HighlightTargets(CC, false);

        transform.SetParent(DefaultParent);
        GetComponent<CanvasGroup>().blocksRaycasts = true;

        transform.SetSiblingIndex(TempCardGO.transform.GetSiblingIndex());
        TempCardGO.transform.SetParent(GameObject.Find("Canvas").transform);
        TempCardGO.transform.localPosition = new Vector3(2166, 0);
    }

    void CheckPosition()
    {
        int newIndex = DefaultTempCardParent.childCount;

        for (int i = 0; i < DefaultTempCardParent.childCount; i++)
        {
            if (transform.position.x < DefaultTempCardParent.GetChild(i).position.x)
            {
                newIndex = i;

                if (TempCardGO.transform.GetSiblingIndex() < newIndex)
                    newIndex--;

                break;
            }
        }

        if (TempCardGO.transform.parent == DefaultParent)
            newIndex = startID;

        TempCardGO.transform.SetSiblingIndex(newIndex);
    }

    public void MoveToField(Transform field)
    {
        transform.SetParent(GameObject.Find("Canvas").transform);
        transform.DOMove(field.position, .5f);
    }

    // === НОВЫЙ МЕТОД ДЛЯ ИИ (ЖДЕТ ОКОНЧАНИЯ АНИМАЦИИ) ===
    public IEnumerator MoveToFieldCoroutine(Transform field)
    {
        // Переносим на Canvas, чтобы анимация работала корректно
        transform.SetParent(GameObject.Find("Canvas").transform);

        // Запускаем твин и ЖДЕМ его завершения
        yield return transform.DOMove(field.position, 0.5f).WaitForCompletion();
    }

    public void MoveToTarget(Transform target)
    {
        StartCoroutine(MoveToTargetCor(target));
    }

    public IEnumerator MoveToTargetCor(Transform target)
    {
        if (transform == null || !gameObject.activeInHierarchy)
            yield return null;

        Vector3 pos = transform.position;
        Transform parent = transform.parent;
        int index = transform.GetSiblingIndex();

        if (parent != null && parent.GetComponent<HorizontalLayoutGroup>())
        {
            var layout = parent.GetComponent<HorizontalLayoutGroup>();
            layout.enabled = false;
        }

        Transform canvasTransform = GameObject.Find("Canvas").transform;
        if (canvasTransform == null)
        {
            Debug.LogError("Canvas не найден!");
            yield break;
        }

        transform.SetParent(canvasTransform);

        if (transform == null || !gameObject.activeInHierarchy)
            yield break;

        // Анимация к цели
        yield return transform.DOMove(target.position, 0.25f).WaitForCompletion();

        if (transform == null || !gameObject.activeInHierarchy)
            yield break;

        // Возвращаемся обратно
        yield return transform.DOMove(pos, 0.25f).WaitForCompletion();

        if (parent != null)
        {
            transform.SetParent(parent);
            transform.SetSiblingIndex(index);

            if (parent.GetComponent<HorizontalLayoutGroup>())
            {
                parent.GetComponent<HorizontalLayoutGroup>().enabled = true;
            }
        }
    }
}