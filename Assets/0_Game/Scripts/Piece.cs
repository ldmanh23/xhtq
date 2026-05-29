using System.Collections;
using UnityEngine;

public class Piece : GameUnit
{
    public SpriteRenderer pieceSprite;
    public SpriteRenderer spriteRenderer;
    public float flipDuration = 0.18f;

    public Vector2Int posInBoard;
    public PictureSO pictureSO;
    public int pictureId;
    public Vector2Int localCell;

    Coroutine flipRoutine;
    Sprite backSprite;
    Vector3 defaultPieceScale = Vector3.one;

    public override void OnInit()
    {

    }

    public override void OnDespawn()
    {

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

        if (flipRoutine != null)
        {
            StopCoroutine(flipRoutine);
        }

        if (playFlip)
        {
            flipRoutine = StartCoroutine(FlipReveal(sprite, delay));
        }
        else
        {
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

    IEnumerator FlipReveal(Sprite sprite, float delay)
    {
        transform.localScale = defaultPieceScale;
        SetSprite(backSprite);

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float halfDuration = flipDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            transform.localScale = new Vector3(Mathf.Lerp(defaultPieceScale.x, 0f, t), defaultPieceScale.y, defaultPieceScale.z);
            yield return null;
        }

        SetSprite(sprite);

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            transform.localScale = new Vector3(Mathf.Lerp(0f, defaultPieceScale.x, t), defaultPieceScale.y, defaultPieceScale.z);
            yield return null;
        }

        transform.localScale = defaultPieceScale;
        flipRoutine = null;
    }

    SpriteRenderer GetTargetSprite()
    {
        return pieceSprite != null ? pieceSprite : spriteRenderer;
    }

    //Drag and Drop

}
