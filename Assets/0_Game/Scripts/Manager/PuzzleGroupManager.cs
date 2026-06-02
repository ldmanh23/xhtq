using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PuzzleGroupManager
{
    public readonly List<PieceGroup> ActiveGroups = new List<PieceGroup>();
    public List<HashSet<string>> PendingOldGroupSetsForScale;

    readonly Func<Vector2Int, bool> isInBoard;
    readonly Func<Vector2Int, bool> isLockedRow;
    readonly Func<PieceGroup, bool> isCompletedGroup;

    public PuzzleGroupManager(
        Func<Vector2Int, bool> isInBoard,
        Func<Vector2Int, bool> isLockedRow,
        Func<PieceGroup, bool> isCompletedGroup)
    {
        this.isInBoard = isInBoard;
        this.isLockedRow = isLockedRow;
        this.isCompletedGroup = isCompletedGroup;
    }

    public void Rebuild(
        Piece[,] pieces,
        PieceGroup pieceGroupPrefab,
        Transform boardParent,
        Transform fallbackParent,
        bool checkCompleted,
        Action checkCompletedGroups)
    {
        if (pieces == null || pieceGroupPrefab == null)
        {
            return;
        }

        List<HashSet<string>> oldGroupSets = PendingOldGroupSetsForScale ?? GetActiveGroupIdSets();
        PendingOldGroupSetsForScale = null;
        ClearGroups(pieces, boardParent, fallbackParent);

        HashSet<Piece> visited = new HashSet<Piece>();

        for (int x = 0; x < pieces.GetLength(0); x++)
        {
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                Piece piece = pieces[x, y];
                if (piece == null || visited.Contains(piece))
                {
                    continue;
                }

                List<Piece> groupPieces = BuildGroup(pieces, piece, visited);
                if (groupPieces.Count >= 2)
                {
                    CreateGroup(groupPieces, pieceGroupPrefab, oldGroupSets);
                }
            }
        }

        UpdateAllPieceBorders(pieces);
        if (checkCompleted)
        {
            checkCompletedGroups?.Invoke();
        }
    }

    public void ClearGroups(Piece[,] pieces, Transform boardParent, Transform fallbackParent)
    {
        Transform parent = boardParent != null ? boardParent : fallbackParent;

        for (int i = 0; i < ActiveGroups.Count; i++)
        {
            if (ActiveGroups[i] != null)
            {
                ActiveGroups[i].transform.DOKill();
                ActiveGroups[i].transform.localScale = Vector3.one;
            }
        }

        if (pieces != null)
        {
            for (int x = 0; x < pieces.GetLength(0); x++)
            {
                for (int y = 0; y < pieces.GetLength(1); y++)
                {
                    Piece piece = pieces[x, y];
                    if (piece != null)
                    {
                        piece.transform.SetParent(parent, true);
                    }
                }
            }
        }

        for (int i = 0; i < ActiveGroups.Count; i++)
        {
            if (ActiveGroups[i] != null)
            {
                SimplePool.Despawn(ActiveGroups[i]);
            }
        }

        ActiveGroups.Clear();
    }

    public bool CanGroup(Piece a, Piece b)
    {
        if (a == null || b == null || a.IsLock || b.IsLock || a.pictureSO != b.pictureSO)
        {
            return false;
        }

        if (isLockedRow(a.posInBoard) || isLockedRow(b.posInBoard))
        {
            return false;
        }

        Vector2Int boardDelta = b.posInBoard - a.posInBoard;
        Vector2Int localDelta = b.localCell - a.localCell;
        return boardDelta == localDelta && Mathf.Abs(boardDelta.x) + Mathf.Abs(boardDelta.y) == 1;
    }

    public List<HashSet<string>> GetActiveGroupIdSets()
    {
        List<HashSet<string>> sets = new List<HashSet<string>>();
        for (int i = 0; i < ActiveGroups.Count; i++)
        {
            PieceGroup group = ActiveGroups[i];
            if (group != null && group.pieces.Count >= 2)
            {
                sets.Add(BuildGroupIdSet(group.pieces));
            }
        }

        return sets;
    }

    List<Piece> BuildGroup(Piece[,] pieces, Piece startPiece, HashSet<Piece> visited)
    {
        List<Piece> groupPieces = new List<Piece>();
        Queue<Piece> queue = new Queue<Piece>();

        visited.Add(startPiece);
        queue.Enqueue(startPiece);

        while (queue.Count > 0)
        {
            Piece piece = queue.Dequeue();
            groupPieces.Add(piece);

            TryAddNeighbor(pieces, piece, Vector2Int.up, visited, queue);
            TryAddNeighbor(pieces, piece, Vector2Int.right, visited, queue);
            TryAddNeighbor(pieces, piece, Vector2Int.down, visited, queue);
            TryAddNeighbor(pieces, piece, Vector2Int.left, visited, queue);
        }

        return groupPieces;
    }

    void TryAddNeighbor(Piece[,] pieces, Piece piece, Vector2Int direction, HashSet<Piece> visited, Queue<Piece> queue)
    {
        Vector2Int neighborCell = piece.posInBoard + direction;
        if (!isInBoard(neighborCell))
        {
            return;
        }

        Piece neighbor = pieces[neighborCell.x, neighborCell.y];
        if (neighbor == null || visited.Contains(neighbor) || !CanGroup(piece, neighbor))
        {
            return;
        }

        visited.Add(neighbor);
        queue.Enqueue(neighbor);
    }

    void CreateGroup(List<Piece> groupPieces, PieceGroup pieceGroupPrefab, List<HashSet<string>> oldGroupSets)
    {
        Vector3 center = Vector3.zero;
        for (int i = 0; i < groupPieces.Count; i++)
        {
            groupPieces[i].ForceToSnapPosition();
            center += groupPieces[i].transform.position;
        }

        center /= groupPieces.Count;

        PieceGroup group = SimplePool.Spawn<PieceGroup>(pieceGroupPrefab);
        group.transform.position = center;
        group.transform.rotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
        group.Setup(groupPieces);
        ActiveGroups.Add(group);

        for (int i = 0; i < groupPieces.Count; i++)
        {
            groupPieces[i].transform.SetParent(group.transform, true);
        }

        if (!isCompletedGroup(group) && ShouldScaleNewGroup(groupPieces, oldGroupSets))
        {
            PlayGroupMergeScaleWhenStable(group);
        }
    }

    void UpdateAllPieceBorders(Piece[,] pieces)
    {
        if (pieces == null)
        {
            return;
        }

        for (int x = 0; x < pieces.GetLength(0); x++)
        {
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                if (pieces[x, y] != null)
                {
                    UpdatePieceBorders(pieces, pieces[x, y]);
                }
            }
        }
    }

    void UpdatePieceBorders(Piece[,] pieces, Piece piece)
    {
        SetBorderByNeighbor(pieces, piece, Vector2Int.up);
        SetBorderByNeighbor(pieces, piece, Vector2Int.right);
        SetBorderByNeighbor(pieces, piece, Vector2Int.down);
        SetBorderByNeighbor(pieces, piece, Vector2Int.left);
    }

    void SetBorderByNeighbor(Piece[,] pieces, Piece piece, Vector2Int direction)
    {
        Vector2Int neighborCell = piece.posInBoard + direction;
        if (!isInBoard(neighborCell))
        {
            piece.SetBorderVisible(direction, true);
            return;
        }

        Piece neighbor = pieces[neighborCell.x, neighborCell.y];
        bool shouldHide = neighbor != null && CanGroup(piece, neighbor);
        piece.SetBorderVisible(direction, !shouldHide);
    }

    HashSet<string> BuildGroupIdSet(List<Piece> groupPieces)
    {
        HashSet<string> ids = new HashSet<string>();
        for (int i = 0; i < groupPieces.Count; i++)
        {
            Piece piece = groupPieces[i];
            if (piece != null)
            {
                ids.Add(piece.pictureId + "_" + piece.localCell.x + "_" + piece.localCell.y);
            }
        }

        return ids;
    }

    bool ShouldScaleNewGroup(List<Piece> groupPieces, List<HashSet<string>> oldGroupSets)
    {
        HashSet<string> newSet = BuildGroupIdSet(groupPieces);
        int bestOldOverlap = 0;

        for (int i = 0; i < oldGroupSets.Count; i++)
        {
            if (newSet.SetEquals(oldGroupSets[i]))
            {
                return false;
            }

            int overlap = 0;
            foreach (string id in newSet)
            {
                if (oldGroupSets[i].Contains(id))
                {
                    overlap++;
                }
            }

            if (overlap > bestOldOverlap)
            {
                bestOldOverlap = overlap;
            }
        }

        return newSet.Count > bestOldOverlap;
    }

    void PlayGroupMergeScale(PieceGroup group)
    {
        group.transform.DOKill();
        Vector3 startPosition = group.transform.position;
        Vector3 startScale = group.transform.localScale;

        group.transform.DOScale(startScale * 1.06f, 0.08f)
            .SetEase(Ease.OutQuad)
            .SetLoops(2, LoopType.Yoyo)
            .OnComplete(() =>
            {
                group.transform.position = startPosition;
                group.transform.localScale = startScale;
            });
    }

    void PlayGroupMergeScaleWhenStable(PieceGroup group)
    {
        DOVirtual.DelayedCall(0.02f, () =>
        {
            if (group != null && ActiveGroups.Contains(group) && !isCompletedGroup(group))
            {
                PlayGroupMergeScale(group);
            }
        });
    }
}
