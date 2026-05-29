using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class Piece : GameUnit, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    public SpriteRenderer pieceSprite;
    public SpriteRenderer spriteRenderer;
    public float flipDuration = 0.18f;
    public float pictureOverlapScale = 1.01f;

    public Vector2Int posInBoard;
    public PictureSO pictureSO;
    public int pictureId;
    public Vector2Int localCell;

    Sequence flipSequence;
    Sprite backSprite;
    Vector3 defaultPieceScale = Vector3.one;
    Vector3 defaultPictureScale = Vector3.one;
    Vector3 dragOffset;
    Vector3 snapPosition;
    float dragZ;
    int defOrder;
    Transform dragTransform;
    PieceGroup dragGroup;
    Vector3 dragStartPosition;

    public SpriteRenderer[] borders;

    public Collider2D _col;

    public override void OnInit()
    {
    }

    public override void OnDespawn()
    {
        KillFlip();
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
        }

        defOrder = spriteRenderer.sortingOrder;
    }

    public void Setup(PictureSO pictureSO, Vector2Int localCell, Sprite sprite, int x, int y, bool playFlip, float delay)
    {
        this.pictureSO = pictureSO;
        pictureId = pictureSO != null ? pictureSO.pictureId : 0;
        this.localCell = localCell;
        SetPosInBoard(x, y);
        ResetBorders();

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
        if (IngameManager.ins != null && IngameManager.ins.IsInputLocked)
        {
            return;
        }

        PieceGroup group = GetCurrentGroup();
        if (group != null)
        {
            SetGroupOrderOffset(group, 100);
        }
        else
        {
            SetOrderOffset(100);
        }

        Debug.Log("OnPointerDown: " + posInBoard);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IngameManager.ins != null && IngameManager.ins.IsInputLocked)
        {
            ResetOrder();
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
            return;
        }

        if (dragGroup != null)
        {
            ResetGroupOrder(dragGroup);
        }
        else
        {
            ResetOrder();
        }

        Vector2Int targetCell;
        if (IngameManager.ins != null && IngameManager.ins.GetNearestCell(transform.position, this, dragGroup, out targetCell))
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

    public void SetBorderVisible(Vector2Int direction, bool visible)
    {
        SpriteRenderer border = GetBorder(direction);
        if (border != null)
        {
            border.gameObject.SetActive(visible);
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
            dragTransform.DOMove(dragStartPosition, 0.15f).SetEase(Ease.OutQuad);
            return;
        }

        if (IngameManager.ins != null)
        {
            IngameManager.ins.LockInputForMove();
        }

        MoveToSnapPosition();
    }
}
