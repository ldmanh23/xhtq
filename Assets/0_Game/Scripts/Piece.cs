using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class Piece : GameUnit, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    public SpriteRenderer pieceSprite;
    public SpriteRenderer spriteRenderer;
    public float flipDuration = 0.18f;
    public float pictureOverlapScale = 1.01f;
    public float borderFadeDuration = 0.12f;

    public Vector2Int posInBoard;
    public PictureSO pictureSO;
    public int pictureId;
    public Vector2Int localCell;

    Sequence flipSequence;
    Tween lockTween;
    Sprite backSprite;
    Vector3 defaultPieceScale = Vector3.one;
    Vector3 defaultPictureScale = Vector3.one;
    Color defaultPictureColor = Color.white;
    Vector3 dragOffset;
    Vector3 snapPosition;
    float dragZ;
    int defOrder;
    Transform dragTransform;
    PieceGroup dragGroup;
    PieceGroup pointerDownGroup;
    Vector3 dragStartPosition;

    public SpriteRenderer[] borders;

    public Collider2D _col;

    bool isLock;
    public bool IsLock => isLock;
    public int numberOfLock;
    public TMP_Text numberOfLock_txt;
    public GameObject lockObj;

    public override void OnInit()
    {
    }

    public override void OnDespawn()
    {
        KillFlip();
        KillLockTween();
        ClearLock();
        ResetVisualState();
        ResetBordersImmediate();
    }

    private void Awake()
    {
        defaultPieceScale = transform.localScale;
        SpriteRenderer targetSprite = GetTargetSprite();

        if (targetSprite != null)
        {
            backSprite = targetSprite.sprite;
        }

        if (pieceSprite != null)
        {
            defaultPictureScale = pieceSprite.transform.localScale;
            defaultPictureColor = pieceSprite.color;
        }

        defOrder = spriteRenderer.sortingOrder;
    }

    public void Setup(PictureSO pictureSO, Vector2Int localCell, Sprite sprite, int x, int y, bool playFlip, float delay)
    {
        this.pictureSO = pictureSO;
        pictureId = pictureSO != null ? pictureSO.pictureId : 0;
        this.localCell = localCell;
        SetPosInBoard(x, y);
        ClearLock();
        ResetVisualState();
        ResetBordersImmediate();

        KillFlip();
        ApplyPictureOverlap();

        if (playFlip)
        {
            FlipReveal(sprite, delay);
        }
        else
        {
            transform.localScale = defaultPieceScale;
            SetSprite(sprite);
        }
    }

    public void SetPosInBoard(int x, int y)
    {
        posInBoard = new Vector2Int(x, y);
    }

    public void SetSnapPosition(Vector3 position)
    {
        snapPosition = position;
        transform.position = position;
    }

    public void SetSnapPositionOnly(Vector3 position)
    {
        snapPosition = position;
    }

    public Vector3 GetSnapPosition()
    {
        return snapPosition;
    }

    public void MoveToSnapPosition()
    {
        transform.DOKill();
        spriteRenderer.sortingOrder += 7;
        SetBorderOrder(spriteRenderer.sortingOrder + 1);
        transform.DOMove(snapPosition, 0.15f).SetEase(Ease.OutQuad)
            .OnComplete(() => ResetOrder());
    }

    public void ForceToSnapPosition()
    {
        transform.DOKill();
        transform.position = snapPosition;
        transform.localScale = defaultPieceScale;
        ResetOrder();
        ApplyPictureOverlap();
    }

    void SetSprite(Sprite sprite)
    {
        SpriteRenderer targetSprite = GetTargetSprite();

        if (targetSprite == null || sprite == null)
        {
            return;
        }

        targetSprite.sprite = sprite;
        ApplyPictureOverlap();
    }

    void ApplyPictureOverlap()
    {
        if (pieceSprite != null)
        {
            pieceSprite.transform.localScale = defaultPictureScale * pictureOverlapScale;
        }
    }

    void ResetVisualState()
    {
        transform.DOKill();
        transform.localScale = defaultPieceScale;

        if (pieceSprite != null)
        {
            pieceSprite.DOKill();
            pieceSprite.color = defaultPictureColor;
            pieceSprite.transform.localScale = defaultPictureScale;
        }
    }

    void FlipReveal(Sprite sprite, float delay)
    {
        KillFlip();
        _col.enabled = false;
        transform.localScale = defaultPieceScale;
        SetSprite(backSprite);

        float halfDuration = flipDuration * 0.5f;
        Vector3 closedScale = new Vector3(defaultPieceScale.x, 0f, defaultPieceScale.z);

        flipSequence = DOTween.Sequence();
        flipSequence.SetDelay(delay);
        flipSequence.Append(transform.DOScale(closedScale, halfDuration).SetEase(Ease.InQuad));
        flipSequence.AppendCallback(() => SetSprite(sprite));
        flipSequence.Append(transform.DOScale(defaultPieceScale, halfDuration).SetEase(Ease.OutQuad));
        flipSequence.OnKill(() => flipSequence = null);
        flipSequence.OnComplete(() =>
        {
            transform.localScale = defaultPieceScale;
            flipSequence = null;
            _col.enabled = true;
        });
    }

    void KillFlip()
    {
        if (flipSequence != null)
        {
            flipSequence.Kill();
            flipSequence = null;
        }
    }

    SpriteRenderer GetTargetSprite()
    {
        return pieceSprite != null ? pieceSprite : spriteRenderer;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isLock || (IngameManager.ins != null && IngameManager.ins.IsLockedRow(posInBoard)))
        {
            return;
        }

        pointerDownGroup = GetCurrentGroup();
        if (pointerDownGroup != null)
        {
            SetGroupOrderOffset(pointerDownGroup, 100);
        }
        else
        {
            SetOrderOffset(100);
        }

        Debug.Log("OnPointerDown: " + posInBoard);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetPointerDownOrder();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLock || (IngameManager.ins != null && (IngameManager.ins.IsInputLocked || IngameManager.ins.IsLockedRow(posInBoard))))
        {
            ResetPointerDownOrder();
            dragTransform = null;
            dragGroup = null;
            return;
        }

        dragGroup = GetCurrentGroup();
        dragTransform = dragGroup != null ? dragGroup.transform : transform;
        dragTransform.DOKill();
        dragStartPosition = dragTransform.position;
        dragZ = dragTransform.position.z;
        dragOffset = dragTransform.position - GetPointerWorldPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragTransform == null || (IngameManager.ins != null && IngameManager.ins.IsInputLocked))
        {
            return;
        }

        Vector3 targetPosition = GetPointerWorldPosition(eventData) + dragOffset;
        targetPosition.z = dragZ;
        dragTransform.position = targetPosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragTransform == null)
        {
            ResetPointerDownOrder();
            return;
        }

        ResetPointerDownOrder();

        Vector2Int targetCell;
        bool hasTarget = false;
        if (IngameManager.ins != null)
        {
            hasTarget = dragGroup != null
                ? IngameManager.ins.GetNearestCellForGroup(dragGroup, this, out targetCell)
                : IngameManager.ins.GetNearestCell(transform.position, this, dragGroup, out targetCell);
        }
        else
        {
            targetCell = Vector2Int.zero;
        }

        if (hasTarget)
        {
            if (dragGroup != null)
            {
                IngameManager.ins.MoveGroupToCell(dragGroup, this, targetCell);
            }
            else
            {
                IngameManager.ins.MovePieceToCell(this, targetCell);
            }

            dragTransform = null;
            dragGroup = null;
            return;
        }

        MoveDragTransformBack();
        dragTransform = null;
        dragGroup = null;
    }

    void ResetPointerDownOrder()
    {
        if (pointerDownGroup != null)
        {
            ResetGroupOrder(pointerDownGroup);
            pointerDownGroup = null;
            return;
        }

        ResetOrder();
    }

    Vector3 GetPointerWorldPosition(PointerEventData eventData)
    {
        Camera cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
        if (cam == null)
        {
            return transform.position;
        }

        Vector3 screenPosition = eventData.position;
        screenPosition.z = Mathf.Abs(cam.transform.position.z - dragZ);
        return cam.ScreenToWorldPoint(screenPosition);
    }

    void ResetOrder()
    {
        spriteRenderer.sortingOrder = defOrder;
        SetBorderOrder(spriteRenderer.sortingOrder + 1);
    }

    void SetOrderOffset(int offset)
    {
        spriteRenderer.sortingOrder = defOrder + offset;
        SetBorderOrder(spriteRenderer.sortingOrder + 1);
    }

    void SetBorderOrder(int sortingOrder)
    {
        if (borders == null)
        {
            return;
        }

        for (int i = 0; i < borders.Length; i++)
        {
            if (borders[i] != null)
            {
                borders[i].sortingOrder = sortingOrder;
            }
        }
    }

    public void ResetBorders()
    {
        SetBorderVisible(Vector2Int.up, true);
        SetBorderVisible(Vector2Int.right, true);
        SetBorderVisible(Vector2Int.down, true);
        SetBorderVisible(Vector2Int.left, true);
    }

    void ResetBordersImmediate()
    {
        SetBorderVisibleImmediate(Vector2Int.up, true);
        SetBorderVisibleImmediate(Vector2Int.right, true);
        SetBorderVisibleImmediate(Vector2Int.down, true);
        SetBorderVisibleImmediate(Vector2Int.left, true);
    }

    void SetBorderVisibleImmediate(Vector2Int direction, bool visible)
    {
        SpriteRenderer border = GetBorder(direction);
        if (border == null)
        {
            return;
        }

        border.DOKill();
        border.gameObject.SetActive(visible);
        Color color = border.color;
        color.a = visible ? 1f : 0f;
        border.color = color;
    }

    public void SetBorderVisible(Vector2Int direction, bool visible)
    {
        SpriteRenderer border = GetBorder(direction);
        if (border != null)
        {
            border.DOKill();
            if (visible)
            {
                if (border.gameObject.activeSelf && border.color.a >= 0.99f)
                {
                    return;
                }

                border.gameObject.SetActive(true);
                Color color = border.color;
                color.a = 1f;
                border.color = color;
                return;
            }

            if (!border.gameObject.activeSelf)
            {
                return;
            }

            border.DOFade(0f, borderFadeDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => border.gameObject.SetActive(false));
        }
    }

    SpriteRenderer GetBorder(Vector2Int direction)
    {
        if (borders == null)
        {
            return null;
        }

        string borderName = GetBorderName(direction);
        for (int i = 0; i < borders.Length; i++)
        {
            if (borders[i] != null && borders[i].name.ToLower() == borderName)
            {
                return borders[i];
            }
        }

        return null;
    }

    string GetBorderName(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
        {
            return "top";
        }

        if (direction == Vector2Int.right)
        {
            return "right";
        }

        if (direction == Vector2Int.down)
        {
            return "bot";
        }

        return "left";
    }

    void SetGroupOrderOffset(PieceGroup group, int offset)
    {
        for (int i = 0; i < group.pieces.Count; i++)
        {
            if (group.pieces[i] != null)
            {
                group.pieces[i].SetOrderOffset(offset);
            }
        }
    }

    void ResetGroupOrder(PieceGroup group)
    {
        for (int i = 0; i < group.pieces.Count; i++)
        {
            if (group.pieces[i] != null)
            {
                group.pieces[i].ResetOrder();
            }
        }
    }

    PieceGroup GetCurrentGroup()
    {
        return transform.parent != null ? transform.parent.GetComponent<PieceGroup>() : null;
    }

    void MoveDragTransformBack()
    {
        if (dragGroup != null && dragTransform != null)
        {
            if (IngameManager.ins != null)
            {
                IngameManager.ins.LockInputForMove();
            }

            dragTransform.DOKill();
            SetGroupOrderOffset(dragGroup, 7);
            dragTransform.DOMove(dragStartPosition, 0.15f).SetEase(Ease.OutQuad)
                .OnComplete(() => ResetGroupOrder(dragGroup));
            return;
        }

        if (IngameManager.ins != null)
        {
            IngameManager.ins.LockInputForMove();
        }

        MoveToSnapPosition();
    }

    public void CheckPieceLock()
    {
        if(isLock)
        {
            if (numberOfLock_txt != null)
            {
                numberOfLock_txt.text = numberOfLock.ToString();
            }

            if (lockObj != null)
            {
                lockObj.SetActive(true);
            }
        }
        else if (lockObj != null)
        {
            lockObj.SetActive(false);
        }
    }

    void KillLockTween()
    {
        if (lockTween != null)
        {
            lockTween.Kill();
            lockTween = null;
        }
    }

    public void SetLock(int lockCount)
    {
        SetLock(lockCount, 0f);
    }

    public void SetLock(int lockCount, float showDelay)
    {
        KillLockTween();
        isLock = lockCount > 0;
        numberOfLock = Mathf.Max(0, lockCount);

        if (!isLock)
        {
            CheckPieceLock();
            return;
        }

        if (lockObj != null)
        {
            lockObj.SetActive(false);
        }

        if (showDelay <= 0f)
        {
            CheckPieceLock();
            return;
        }

        lockTween = DOVirtual.DelayedCall(showDelay, () =>
        {
            lockTween = null;
            CheckPieceLock();
        });
    }

    public void ClearLock()
    {
        KillLockTween();
        isLock = false;
        numberOfLock = 0;
        CheckPieceLock();
    }

    public void DecreaseLock()
    {
        if (!isLock)
        {
            return;
        }

        numberOfLock = Mathf.Max(0, numberOfLock - 1);
        isLock = numberOfLock > 0;
        CheckPieceLock();
    }
}
