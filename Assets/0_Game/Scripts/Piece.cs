using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class Piece : GameUnit, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    public SpriteRenderer pieceSprite;
    public SpriteRenderer spriteRenderer;
    public float flipDuration = 0.18f;

    public Vector2Int posInBoard;
    public PictureSO pictureSO;
    public int pictureId;
    public Vector2Int localCell;

    Sequence flipSequence;
    Sprite backSprite;
    Vector3 defaultPieceScale = Vector3.one;
    Vector3 dragOffset;
    Vector3 snapPosition;
    float dragZ;
    int defOrder;

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

        defOrder = spriteRenderer.sortingOrder;
    }

    public void Setup(PictureSO pictureSO, Vector2Int localCell, Sprite sprite, int x, int y, bool playFlip, float delay)
    {
        this.pictureSO = pictureSO;
        pictureId = pictureSO != null ? pictureSO.pictureId : 0;
        this.localCell = localCell;
        SetPosInBoard(x, y);

        KillFlip();

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
        spriteRenderer.sortingOrder += 7;
        for (int i = 0; i < borders.Length; i++)
        {
            borders[i].sortingOrder = spriteRenderer.sortingOrder + 1;
        }
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
        spriteRenderer.sortingOrder += 10;
        for (int i = 0; i < borders.Length; i++)
        {
            borders[i].sortingOrder = spriteRenderer.sortingOrder + 1;
        }
        Debug.Log("OnPointerDown: " + posInBoard);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragZ = transform.position.z;
        dragOffset = transform.position - GetPointerWorldPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector3 targetPosition = GetPointerWorldPosition(eventData) + dragOffset;
        targetPosition.z = dragZ;
        transform.position = targetPosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ResetOrder();

        Piece targetPiece = IngameManager.ins != null ? IngameManager.ins.GetNearestPiece(transform.position, this) : null;
        if (targetPiece != null)
        {
            IngameManager.ins.SwapPieces(this, targetPiece);
            return;
        }

        MoveToSnapPosition();
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
        for (int i = 0; i < borders.Length; i++)
        {
            borders[i].sortingOrder = spriteRenderer.sortingOrder + 1;
        }
    }
}
