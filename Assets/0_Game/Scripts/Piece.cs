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
        Debug.Log("Pointer Down");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("Begin Drag");
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("End Drag");
    }
}
