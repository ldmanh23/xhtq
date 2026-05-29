using UnityEngine;

public class Piece : GameUnit
{
    public SpriteRenderer spriteRenderer;

    public Vector2Int posInBoard;

    public void Setup(Sprite sprite, int x, int y)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
        }
        SetPosInBoard(x, y);
    }

    public override void OnInit()
    {
        
    }

    public override void OnDespawn()
    {

    }

    public void SetPosInBoard(int x, int y)
    {
        posInBoard = new Vector2Int(x, y);
    }
}
