using System.Collections.Generic;
using UnityEngine;

public class PuzzleMoveManager
{
    public Piece GetNearestPiece(IngameManager manager, Vector3 position, Piece ignorePiece, PieceGroup ignoreGroup)
    {
        if (manager.pieces == null)
        {
            return null;
        }

        Piece nearestPiece = null;
        float nearestDistance = manager.GetPieceSize(manager.piecePrb).magnitude * 0.5f;

        for (int x = 0; x < manager.pieces.GetLength(0); x++)
        {
            for (int y = 0; y < manager.pieces.GetLength(1); y++)
            {
                Piece piece = manager.pieces[x, y];
                if (piece == null || piece == ignorePiece || (ignoreGroup != null && ignoreGroup.Contains(piece)))
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

    public bool GetNearestCell(IngameManager manager, Vector3 position, Piece ignorePiece, PieceGroup ignoreGroup, out Vector2Int nearestCell)
    {
        nearestCell = Vector2Int.zero;
        if (manager.pieces == null)
        {
            return false;
        }

        bool foundCell = false;
        float nearestDistance = manager.GetPieceSize(manager.piecePrb).magnitude * 0.5f;

        for (int x = 0; x < manager.pieces.GetLength(0); x++)
        {
            for (int y = 0; y < manager.pieces.GetLength(1); y++)
            {
                Piece piece = manager.pieces[x, y];
                if (piece == ignorePiece
                    || (piece != null && piece.IsLock)
                    || (ignoreGroup != null && piece != null && ignoreGroup.Contains(piece)))
                {
                    continue;
                }

                float distance = Vector3.Distance(position, manager.GetBoardWorldPosition(x, y));
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearestCell = new Vector2Int(x, y);
                    foundCell = true;
                }
            }
        }

        return foundCell;
    }

    public bool GetNearestCellForGroup(IngameManager manager, PieceGroup group, Piece grabbedPiece, out Vector2Int targetCell)
    {
        targetCell = Vector2Int.zero;
        if (manager.pieces == null || group == null || grabbedPiece == null)
        {
            return false;
        }

        bool foundCell = false;
        float nearestDistance = manager.GetPieceSize(manager.piecePrb).magnitude * 0.5f;
        float bestScore = float.MinValue;

        for (int i = 0; i < group.pieces.Count; i++)
        {
            Piece groupPiece = group.pieces[i];
            if (groupPiece == null)
            {
                continue;
            }

            for (int x = 0; x < manager.pieces.GetLength(0); x++)
            {
                for (int y = 0; y < manager.pieces.GetLength(1); y++)
                {
                    float distance = Vector3.Distance(groupPiece.transform.position, manager.GetBoardWorldPosition(x, y));
                    if (distance > nearestDistance)
                    {
                        continue;
                    }

                    Vector2Int offset = new Vector2Int(x, y) - groupPiece.posInBoard;
                    if (offset == Vector2Int.zero || !IsValidGroupMoveOffset(manager, group, offset))
                    {
                        continue;
                    }

                    int connectionCount = CountGroupOutsideConnections(manager, group, offset);
                    float score = connectionCount * 100f - distance;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        targetCell = grabbedPiece.posInBoard + offset;
                        foundCell = true;
                    }
                }
            }
        }

        return foundCell;
    }

    public void MovePieceToCell(IngameManager manager, Piece piece, Vector2Int targetCell)
    {
        if (manager.IsInputLocked || piece == null || piece.IsLock || !manager.IsInBoard(targetCell) || manager.IsLockedRow(targetCell))
        {
            if (piece != null)
            {
                manager.LockInputForMove();
                piece.MoveToSnapPosition();
            }

            return;
        }

        Vector2Int oldCell = piece.posInBoard;
        if (oldCell == targetCell)
        {
            manager.LockInputForMove();
            piece.MoveToSnapPosition();
            return;
        }

        Piece targetPiece = manager.pieces[targetCell.x, targetCell.y];
        if (targetPiece != null)
        {
            if (targetPiece.IsLock || manager.IsLockedRow(targetCell))
            {
                manager.LockInputForMove();
                piece.MoveToSnapPosition();
                return;
            }

            SwapPieces(manager, piece, targetPiece);
            return;
        }

        manager.IsInputLocked = true;
        manager.pieces[oldCell.x, oldCell.y] = null;
        manager.pieces[targetCell.x, targetCell.y] = piece;

        piece.SetPosInBoard(targetCell.x, targetCell.y);
        piece.SetSnapPositionOnly(manager.GetBoardWorldPosition(targetCell.x, targetCell.y));
        piece.MoveToSnapPosition();

        if (manager.CanConnectWithNeighbor(piece))
        {
            manager.ApplyGravity(manager.GetConnectedPieces(new List<Piece> { piece }));
        }
        else
        {
            manager.ApplyGravityKeepingConnectedPieces();
        }

        manager.RebuildGroupsAfterMove();
    }

    public void SwapPieces(IngameManager manager, Piece firstPiece, Piece secondPiece)
    {
        if (manager.IsInputLocked || firstPiece == null || secondPiece == null || firstPiece.IsLock || secondPiece.IsLock)
        {
            return;
        }

        manager.IsInputLocked = true;

        Vector2Int firstCell = firstPiece.posInBoard;
        Vector2Int secondCell = secondPiece.posInBoard;

        manager.pieces[firstCell.x, firstCell.y] = secondPiece;
        manager.pieces[secondCell.x, secondCell.y] = firstPiece;

        Vector3 firstSnap = firstPiece.GetSnapPosition();
        Vector3 secondSnap = secondPiece.GetSnapPosition();

        firstPiece.SetPosInBoard(secondCell.x, secondCell.y);
        secondPiece.SetPosInBoard(firstCell.x, firstCell.y);

        firstPiece.SetSnapPositionOnly(secondSnap);
        secondPiece.SetSnapPositionOnly(firstSnap);

        firstPiece.MoveToSnapPosition();
        secondPiece.MoveToSnapPosition();
        manager.ApplyGravityKeepingConnectedPieces();

        manager.RebuildGroupsAfterMove();
    }

    public void SwapGroup(IngameManager manager, PieceGroup group, Piece grabbedPiece, Piece targetPiece)
    {
        if (targetPiece == null)
        {
            MoveGroupBack(manager, group);
            return;
        }

        MoveGroupToCell(manager, group, grabbedPiece, targetPiece.posInBoard);
    }

    public void MoveGroupToCell(IngameManager manager, PieceGroup group, Piece grabbedPiece, Vector2Int targetCell)
    {
        if (manager.IsInputLocked)
        {
            return;
        }

        if (group == null || grabbedPiece == null || GroupHasLockedPiece(group) || !manager.IsInBoard(targetCell) || manager.IsLockedRow(targetCell))
        {
            MoveGroupBack(manager, group);
            return;
        }

        Vector2Int offset = targetCell - grabbedPiece.posInBoard;
        if (offset == Vector2Int.zero)
        {
            MoveGroupBack(manager, group);
            return;
        }

        List<Piece> groupPieces = new List<Piece>(group.pieces);
        List<Vector2Int> oldCells = new List<Vector2Int>(groupPieces.Count);
        List<Vector2Int> newCells = new List<Vector2Int>(groupPieces.Count);
        List<Piece> swapPieces = new List<Piece>();
        List<Vector2Int> swapToCells = new List<Vector2Int>();
        bool hasEmptyTargetCell = false;

        for (int i = 0; i < groupPieces.Count; i++)
        {
            Piece piece = groupPieces[i];
            if (piece == null)
            {
                MoveGroupBack(manager, group);
                return;
            }

            Vector2Int oldCell = piece.posInBoard;
            Vector2Int newCell = oldCell + offset;
            if (!manager.IsInBoard(newCell) || manager.IsLockedRow(newCell))
            {
                MoveGroupBack(manager, group);
                return;
            }

            oldCells.Add(oldCell);
            newCells.Add(newCell);
        }

        for (int i = 0; i < newCells.Count; i++)
        {
            Piece swapPiece = manager.pieces[newCells[i].x, newCells[i].y];
            if (swapPiece == null)
            {
                hasEmptyTargetCell = true;
            }
            else if (!group.Contains(swapPiece))
            {
                if (swapPiece.IsLock || manager.IsLockedRow(newCells[i]))
                {
                    MoveGroupBack(manager, group);
                    return;
                }

                swapPieces.Add(swapPiece);
            }
        }

        for (int i = 0; i < oldCells.Count; i++)
        {
            if (!newCells.Contains(oldCells[i]))
            {
                swapToCells.Add(oldCells[i]);
            }
        }

        if (swapPieces.Count > swapToCells.Count)
        {
            MoveGroupBack(manager, group);
            return;
        }

        manager.IsInputLocked = true;

        for (int i = 0; i < oldCells.Count; i++)
        {
            manager.pieces[oldCells[i].x, oldCells[i].y] = null;
        }

        for (int i = 0; i < groupPieces.Count; i++)
        {
            Piece groupPiece = groupPieces[i];
            Vector2Int newCell = newCells[i];

            manager.pieces[newCell.x, newCell.y] = groupPiece;
            groupPiece.SetPosInBoard(newCell.x, newCell.y);
            groupPiece.SetSnapPositionOnly(manager.GetBoardWorldPosition(newCell.x, newCell.y));
        }

        for (int i = 0; i < swapPieces.Count; i++)
        {
            Piece swapPiece = swapPieces[i];
            Vector2Int swapToCell = swapToCells[i];

            manager.pieces[swapToCell.x, swapToCell.y] = swapPiece;
            swapPiece.SetPosInBoard(swapToCell.x, swapToCell.y);
            swapPiece.SetSnapPositionOnly(manager.GetBoardWorldPosition(swapToCell.x, swapToCell.y));
        }

        for (int i = 0; i < groupPieces.Count; i++)
        {
            groupPieces[i].MoveToSnapPosition();
        }

        for (int i = 0; i < swapPieces.Count; i++)
        {
            swapPieces[i].MoveToSnapPosition();
        }

        if (hasEmptyTargetCell)
        {
            manager.ApplyGravityKeepingConnectedPieces();
        }

        manager.RebuildGroupsAfterMove();
    }

    public void MoveGroupBack(IngameManager manager, PieceGroup group)
    {
        if (group == null)
        {
            return;
        }

        manager.LockInputForMove();

        for (int i = 0; i < group.pieces.Count; i++)
        {
            if (group.pieces[i] != null)
            {
                group.pieces[i].MoveToSnapPosition();
            }
        }
    }

    bool GroupHasLockedPiece(PieceGroup group)
    {
        if (group == null)
        {
            return false;
        }

        for (int i = 0; i < group.pieces.Count; i++)
        {
            if (group.pieces[i] != null && group.pieces[i].IsLock)
            {
                return true;
            }
        }

        return false;
    }

    bool IsValidGroupMoveOffset(IngameManager manager, PieceGroup group, Vector2Int offset)
    {
        List<Vector2Int> oldCells = new List<Vector2Int>();
        List<Vector2Int> newCells = new List<Vector2Int>();
        int swapPieceCount = 0;
        int swapToCellCount = 0;

        for (int i = 0; i < group.pieces.Count; i++)
        {
            Piece piece = group.pieces[i];
            if (piece == null)
            {
                return false;
            }

            Vector2Int oldCell = piece.posInBoard;
            Vector2Int newCell = oldCell + offset;
            if (!manager.IsInBoard(newCell) || manager.IsLockedRow(newCell))
            {
                return false;
            }

            oldCells.Add(oldCell);
            newCells.Add(newCell);
        }

        for (int i = 0; i < newCells.Count; i++)
        {
            Piece targetPiece = manager.pieces[newCells[i].x, newCells[i].y];
            if (targetPiece != null && targetPiece.IsLock)
            {
                return false;
            }

            if (targetPiece != null && !group.Contains(targetPiece))
            {
                swapPieceCount++;
            }
        }

        for (int i = 0; i < oldCells.Count; i++)
        {
            if (!newCells.Contains(oldCells[i]))
            {
                swapToCellCount++;
            }
        }

        return swapPieceCount <= swapToCellCount;
    }

    int CountGroupOutsideConnections(IngameManager manager, PieceGroup group, Vector2Int offset)
    {
        int count = 0;
        List<Vector2Int> newCells = new List<Vector2Int>();

        for (int i = 0; i < group.pieces.Count; i++)
        {
            if (group.pieces[i] != null)
            {
                newCells.Add(group.pieces[i].posInBoard + offset);
            }
        }

        for (int i = 0; i < group.pieces.Count; i++)
        {
            Piece piece = group.pieces[i];
            if (piece == null)
            {
                continue;
            }

            Vector2Int futureCell = piece.posInBoard + offset;
            count += CountOutsideConnection(manager, piece, futureCell, Vector2Int.up, group, newCells);
            count += CountOutsideConnection(manager, piece, futureCell, Vector2Int.right, group, newCells);
            count += CountOutsideConnection(manager, piece, futureCell, Vector2Int.down, group, newCells);
            count += CountOutsideConnection(manager, piece, futureCell, Vector2Int.left, group, newCells);
        }

        return count;
    }

    int CountOutsideConnection(IngameManager manager, Piece piece, Vector2Int futureCell, Vector2Int direction, PieceGroup group, List<Vector2Int> newCells)
    {
        Vector2Int neighborCell = futureCell + direction;
        if (!manager.IsInBoard(neighborCell)
            || newCells.Contains(neighborCell)
            || manager.IsLockedRow(futureCell)
            || manager.IsLockedRow(neighborCell))
        {
            return 0;
        }

        Piece neighbor = manager.pieces[neighborCell.x, neighborCell.y];
        if (neighbor == null || neighbor.IsLock || group.Contains(neighbor) || piece.pictureSO != neighbor.pictureSO)
        {
            return 0;
        }

        Vector2Int boardDelta = neighbor.posInBoard - futureCell;
        Vector2Int localDelta = neighbor.localCell - piece.localCell;
        return boardDelta == localDelta && Mathf.Abs(boardDelta.x) + Mathf.Abs(boardDelta.y) == 1 ? 1 : 0;
    }
}
