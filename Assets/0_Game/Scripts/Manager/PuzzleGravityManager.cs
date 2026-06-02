using System;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleGravityManager : Singleton<PuzzleGravityManager>
{
    Func<Vector2Int, bool> isInBoard;
    Func<Piece, Piece, bool> canGroup;
    Func<int, int, Vector3> getBoardWorldPosition;

    public void Initialize(
        Func<Vector2Int, bool> isInBoard,
        Func<Piece, Piece, bool> canGroup,
        Func<int, int, Vector3> getBoardWorldPosition)
    {
        this.isInBoard = isInBoard;
        this.canGroup = canGroup;
        this.getBoardWorldPosition = getBoardWorldPosition;
    }

    public void Apply(
        Piece[,] pieces,
        Queue<IngameManager.SpawnPieceData>[] columnDecks,
        Piece piecePrefab,
        Transform boardParent,
        Transform fallbackParent,
        int width,
        int height,
        HashSet<Piece> pinnedPieces,
        PuzzleGroupManager groupManager)
    {
        if (pinnedPieces == null)
        {
            pinnedPieces = new HashSet<Piece>();
        }

        if (groupManager != null)
        {
            groupManager.PendingOldGroupSetsForScale = groupManager.GetActiveGroupIdSets();
            groupManager.ClearGroups(pieces, boardParent, fallbackParent);
        }

        for (int x = 0; x < width; x++)
        {
            int segmentStartY = 0;
            for (int y = 0; y <= height; y++)
            {
                bool isEnd = y == height;
                bool isLockBlocker = !isEnd && pieces[x, y] != null && pieces[x, y].IsLock;
                if (isEnd || isLockBlocker)
                {
                    ApplyGravitySegment(
                        pieces,
                        columnDecks,
                        piecePrefab,
                        boardParent,
                        x,
                        segmentStartY,
                        y - 1,
                        pinnedPieces,
                        isEnd,
                        height);
                    segmentStartY = y + 1;
                }
            }
        }
    }

    public HashSet<Piece> BuildPinnedPieces(Piece[,] pieces, Piece pinnedPiece, bool keepConnectedPieces)
    {
        HashSet<Piece> pinnedPieces = new HashSet<Piece>();
        if (pinnedPiece != null)
        {
            pinnedPieces.Add(pinnedPiece);
        }

        if (!keepConnectedPieces || pieces == null)
        {
            return pinnedPieces;
        }

        HashSet<Piece> visited = new HashSet<Piece>();
        for (int x = 0; x < pieces.GetLength(0); x++)
        {
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                Piece piece = pieces[x, y];
                if (piece == null || piece.IsLock || visited.Contains(piece) || !CanConnectWithNeighbor(pieces, piece))
                {
                    continue;
                }

                List<Piece> connectedPieces = GetConnectedPieces(pieces, new List<Piece> { piece });
                for (int i = 0; i < connectedPieces.Count; i++)
                {
                    visited.Add(connectedPieces[i]);
                }

                if (ContainsPinnedPiece(connectedPieces, pinnedPieces) || !CanConnectedGroupFallOneCell(pieces, connectedPieces))
                {
                    for (int i = 0; i < connectedPieces.Count; i++)
                    {
                        pinnedPieces.Add(connectedPieces[i]);
                    }
                }
            }
        }

        return pinnedPieces;
    }

    public List<Piece> GetConnectedPieces(Piece[,] pieces, List<Piece> startPieces)
    {
        List<Piece> result = new List<Piece>();
        if (startPieces == null || startPieces.Count == 0)
        {
            return result;
        }

        HashSet<Piece> visited = new HashSet<Piece>();
        Queue<Piece> queue = new Queue<Piece>();

        for (int i = 0; i < startPieces.Count; i++)
        {
            Piece piece = startPieces[i];
            if (piece != null && visited.Add(piece))
            {
                queue.Enqueue(piece);
            }
        }

        while (queue.Count > 0)
        {
            Piece piece = queue.Dequeue();
            result.Add(piece);

            AddConnectedNeighbor(pieces, piece, Vector2Int.up, visited, queue);
            AddConnectedNeighbor(pieces, piece, Vector2Int.right, visited, queue);
            AddConnectedNeighbor(pieces, piece, Vector2Int.down, visited, queue);
            AddConnectedNeighbor(pieces, piece, Vector2Int.left, visited, queue);
        }

        return result;
    }

    public bool CanConnectWithNeighbor(Piece[,] pieces, Piece piece)
    {
        return CanConnectWithNeighbor(pieces, piece, Vector2Int.up)
            || CanConnectWithNeighbor(pieces, piece, Vector2Int.right)
            || CanConnectWithNeighbor(pieces, piece, Vector2Int.down)
            || CanConnectWithNeighbor(pieces, piece, Vector2Int.left);
    }

    bool ContainsPinnedPiece(List<Piece> connectedPieces, HashSet<Piece> pinnedPieces)
    {
        for (int i = 0; i < connectedPieces.Count; i++)
        {
            if (pinnedPieces.Contains(connectedPieces[i]))
            {
                return true;
            }
        }

        return false;
    }

    bool CanConnectedGroupFallOneCell(Piece[,] pieces, List<Piece> connectedPieces)
    {
        HashSet<Piece> groupSet = new HashSet<Piece>(connectedPieces);
        for (int i = 0; i < connectedPieces.Count; i++)
        {
            Piece piece = connectedPieces[i];
            if (piece == null)
            {
                return false;
            }

            Vector2Int belowCell = piece.posInBoard + Vector2Int.down;
            if (!isInBoard(belowCell))
            {
                return false;
            }

            Piece belowPiece = pieces[belowCell.x, belowCell.y];
            if (belowPiece != null && !groupSet.Contains(belowPiece))
            {
                return false;
            }
        }

        return true;
    }

    void ApplyGravitySegment(
        Piece[,] pieces,
        Queue<IngameManager.SpawnPieceData>[] columnDecks,
        Piece piecePrefab,
        Transform boardParent,
        int x,
        int startY,
        int endY,
        HashSet<Piece> pinnedPieces,
        bool canRefillFromDeck,
        int height)
    {
        if (startY > endY)
        {
            return;
        }

        List<Piece> movablePieces = new List<Piece>();
        for (int y = startY; y <= endY; y++)
        {
            Piece piece = pieces[x, y];
            if (piece == null || pinnedPieces.Contains(piece))
            {
                continue;
            }

            movablePieces.Add(piece);
            pieces[x, y] = null;
        }

        int movableIndex = 0;
        for (int y = startY; y <= endY; y++)
        {
            if (pieces[x, y] != null)
            {
                continue;
            }

            if (movableIndex >= movablePieces.Count)
            {
                break;
            }

            Piece piece = movablePieces[movableIndex];
            pieces[x, y] = piece;

            if (piece.posInBoard.x != x || piece.posInBoard.y != y)
            {
                piece.SetPosInBoard(x, y);
                piece.SetSnapPositionOnly(getBoardWorldPosition(x, y));
                piece.MoveToSnapPosition();
            }

            movableIndex++;
        }

        if (canRefillFromDeck)
        {
            FillEmptyCellsFromDeck(pieces, columnDecks, piecePrefab, boardParent, x, startY, endY, height);
        }
    }

    void FillEmptyCellsFromDeck(
        Piece[,] pieces,
        Queue<IngameManager.SpawnPieceData>[] columnDecks,
        Piece piecePrefab,
        Transform boardParent,
        int x,
        int startY,
        int endY,
        int height)
    {
        if (columnDecks == null || x < 0 || x >= columnDecks.Length || columnDecks[x] == null)
        {
            return;
        }

        int spawnOrder = 0;
        for (int y = startY; y <= endY; y++)
        {
            if (pieces[x, y] != null)
            {
                continue;
            }

            if (columnDecks[x].Count == 0)
            {
                return;
            }

            IngameManager.SpawnPieceData data = columnDecks[x].Dequeue();
            Piece piece = SimplePool.Spawn<Piece>(piecePrefab);
            if (boardParent != null)
            {
                piece.transform.SetParent(boardParent, false);
            }

            Vector3 targetPosition = getBoardWorldPosition(x, y);
            Vector3 spawnPosition = getBoardWorldPosition(x, height + spawnOrder);
            piece.transform.position = spawnPosition;
            piece.transform.localScale = Vector3.one;

            piece.Setup(data.pictureSO, data.localCell, data.sprite, x, y, false, 0f);
            piece.SetSnapPositionOnly(targetPosition);
            pieces[x, y] = piece;
            piece.MoveToSnapPosition();
            spawnOrder++;
        }
    }

    void AddConnectedNeighbor(Piece[,] pieces, Piece piece, Vector2Int direction, HashSet<Piece> visited, Queue<Piece> queue)
    {
        Vector2Int neighborCell = piece.posInBoard + direction;
        if (!isInBoard(neighborCell))
        {
            return;
        }

        Piece neighbor = pieces[neighborCell.x, neighborCell.y];
        if (neighbor == null || visited.Contains(neighbor) || !canGroup(piece, neighbor))
        {
            return;
        }

        visited.Add(neighbor);
        queue.Enqueue(neighbor);
    }

    bool CanConnectWithNeighbor(Piece[,] pieces, Piece piece, Vector2Int direction)
    {
        if (piece == null)
        {
            return false;
        }

        Vector2Int neighborCell = piece.posInBoard + direction;
        if (!isInBoard(neighborCell))
        {
            return false;
        }

        Piece neighbor = pieces[neighborCell.x, neighborCell.y];
        return neighbor != null && canGroup(piece, neighbor);
    }
}
