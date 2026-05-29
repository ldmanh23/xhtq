using UnityEngine;

public class IngameManager : MonoBehaviour
{
    public Piece piecePrb;
    public int height = 6;
    public int width = 6;

    public Piece[,] pieces;

    private void Start()
    {
        BuildBoard();
    }

    public void BuildBoard()
    {
        pieces = new Piece[width, height];

        Vector2 pieceSize = GetPieceSize(piecePrb);
        Vector2 boardOffset = new Vector2(
            (width - 1) * pieceSize.x * 0.5f,
            (height - 1) * pieceSize.y * 0.5f
        );

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Piece piece = SimplePool.Spawn<Piece>(piecePrb);

                piece.transform.localPosition = new Vector3(
                    x * pieceSize.x - boardOffset.x,
                    y * pieceSize.y - boardOffset.y,
                    0
                );

                pieces[x, y] = piece;
            }
        }
    }

    Vector2 GetPieceSize(Piece piece)
    {
        if (piece == null || piece.spriteRenderer == null || piece.spriteRenderer.sprite == null)
        {
            Debug.LogError("Piece prefab is missing spriteRenderer or sprite.");
            return Vector2.one;
        }

        Vector2 spriteSize = piece.spriteRenderer.sprite.bounds.size;
        Vector3 scale = piece.spriteRenderer.transform.lossyScale;

        return new Vector2(
            spriteSize.x * scale.x,
            spriteSize.y * scale.y
        );
    }    
}
