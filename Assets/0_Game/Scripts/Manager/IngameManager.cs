using System.Collections.Generic;
using UnityEngine;

public class IngameManager : SingletonMonoBehaviour<IngameManager>
{
    public LevelSO curLevel;

    public Piece piecePrb;
    public int height = 6;
    public int width = 6;
    public bool playSpawnFlip = true;
    public float flipDelayEachPiece = 0.02f;

    public Piece[,] pieces;

    struct SpawnPieceData
    {
        public PictureSO pictureSO;
        public Vector2Int localCell;
        public Sprite sprite;
    }

    private void Start()
    {
        BuildBoard();
    }

    public void BuildBoard()
    {
        if (curLevel == null || piecePrb == null)
        {
            Debug.LogError("Missing level or piece prefab.");
            return;
        }

        pieces = new Piece[curLevel.boardSize.x, curLevel.boardSize.y];
        width = curLevel.boardSize.x;
        height = curLevel.boardSize.y;

        List<SpawnPieceData> spawnPieces = BuildSpawnPieces();
        if (spawnPieces.Count == 0)
        {
            Debug.LogError("Level has no valid picture pieces.");
            return;
        }

        if (curLevel.shufflePieces)
        {
            Shuffle(spawnPieces);
        }

        Vector2 pieceSize = GetPieceSize(piecePrb);
        Vector2 boardOffset = new Vector2(
            (width - 1) * pieceSize.x * 0.5f,
            (height - 1) * pieceSize.y * 0.5f
        );

        int spawnCount = GetSpawnCount(spawnPieces.Count);
        int spawnIndex = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (spawnIndex >= spawnCount)
                {
                    return;
                }

                Piece piece = SimplePool.Spawn<Piece>(piecePrb);

                piece.transform.localPosition = new Vector3(
                    x * pieceSize.x - boardOffset.x,
                    y * pieceSize.y - boardOffset.y,
                    0
                );

                SpawnPieceData data = spawnPieces[spawnIndex];
                float delay = spawnIndex * flipDelayEachPiece;
                piece.Setup(data.pictureSO, data.localCell, data.sprite, x, y, playSpawnFlip, delay);
                piece.SetSnapPosition(piece.transform.position);

                pieces[x, y] = piece;
                spawnIndex++;
            }
        }
    }

    int GetSpawnCount(int availablePieceCount)
    {
        int boardCapacity = width * height;
        int requestedCount = curLevel.initialPieceCount > 0 ? curLevel.initialPieceCount : boardCapacity;
        return Mathf.Min(requestedCount, boardCapacity, availablePieceCount);
    }

    List<SpawnPieceData> BuildSpawnPieces()
    {
        List<SpawnPieceData> spawnPieces = new List<SpawnPieceData>();

        for (int i = 0; i < curLevel.pictures.Count; i++)
        {
            PictureSO picture = curLevel.pictures[i];
            if (picture == null)

            {
                continue;
            }

            for (int j = 0; j < picture.pieces.Count; j++)
            {
                PieceSpriteData pieceData = picture.pieces[j];
                if (pieceData == null || pieceData.sprite == null)
                {
                    continue;
                }

                spawnPieces.Add(new SpawnPieceData
                {
                    pictureSO = picture,
                    localCell = pieceData.localCell,
                    sprite = pieceData.sprite
                });
            }
        }

        return spawnPieces;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    public Piece GetNearestPiece(Vector3 position, Piece ignorePiece)
    {
        if (pieces == null)
        {
            return null;
        }

        Piece nearestPiece = null;
        float nearestDistance = GetPieceSize(piecePrb).magnitude * 0.5f;

        for (int x = 0; x < pieces.GetLength(0); x++)
        {
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                Piece piece = pieces[x, y];
                if (piece == null || piece == ignorePiece)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, piece.GetSnapPosition());
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPiece = piece;
                }
            }
        }

        return nearestPiece;
    }

    public void SwapPieces(Piece firstPiece, Piece secondPiece)
    {
        Vector2Int firstCell = firstPiece.posInBoard;
        Vector2Int secondCell = secondPiece.posInBoard;

        pieces[firstCell.x, firstCell.y] = secondPiece;
        pieces[secondCell.x, secondCell.y] = firstPiece;

        Vector3 firstSnap = firstPiece.GetSnapPosition();
        Vector3 secondSnap = secondPiece.GetSnapPosition();

        firstPiece.SetPosInBoard(secondCell.x, secondCell.y);
        secondPiece.SetPosInBoard(firstCell.x, firstCell.y);

        firstPiece.SetSnapPositionOnly(secondSnap);
        secondPiece.SetSnapPositionOnly(firstSnap);

        firstPiece.MoveToSnapPosition();
        secondPiece.MoveToSnapPosition();
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
    
    //Drag drop
}
