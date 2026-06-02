using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class IngameManager : SingletonMonoBehaviour<IngameManager>
{
    public LevelSO curLevel;

    public Piece piecePrb;
    public PieceGroup pieceGroupPrb;
    public int height = 6;
    public int width = 6;
    public int lockedTopRows = 1;
    public bool playSpawnFlip = true;
    public float flipDelayEachPiece = 0.02f;
    const float PieceMoveDuration = 0.17f;
    const float CompleteClearDuration = 0.35f;
    const float CompleteFlashDuration = 0.08f;

    public Piece[,] pieces;
    Queue<SpawnPieceData>[] columnDecks;
    PuzzleGroupManager groupManager;
    PuzzleGravityManager gravityManager;
    Transform boardParent;
    Tween rebuildTween;
    Tween showHandTutTween;
    bool isResolvingComplete;

    public bool IsInputLocked { get; private set; }

    public struct SpawnPieceData
    {
        public PictureSO pictureSO;
        public Vector2Int localCell;
        public Sprite sprite;
    }

    private void Start()
    {
        EnsureGroupManager();
        BuildBoard();
        UIManager.ins.OpenUI(UIID.UICGamePlay);
    }

    void EnsureGroupManager()
    {
        if (groupManager == null)
        {
            groupManager = new PuzzleGroupManager(IsInBoard, IsLockedRow, IsCompletedGroup);
        }
    }

    void EnsureGravityManager()
    {
        if (gravityManager == null)
        {
            gravityManager = new PuzzleGravityManager(IsInBoard, CanGroup, GetBoardWorldPosition);
        }
    }

    public void BuildBoard()
    {
        ResolveCurrentLevel();

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

        SortSpawnPiecesByPictureSize(spawnPieces);

        if (curLevel.isTutorialLevel)
        {
            TutorialLevelArranger.Apply(curLevel, spawnPieces, width, height);
        }

        Vector2 pieceSize = GetPieceSize(piecePrb);
        Vector2 boardOffset = new Vector2(
            (width - 1) * pieceSize.x * 0.5f,
            (height - 1) * pieceSize.y * 0.5f
        );

        int spawnCount = GetSpawnCount(spawnPieces.Count);
        if (!curLevel.isTutorialLevel)
        {
            EnsureOneCompletePictureInBoard(spawnPieces, spawnCount);
        }
        BuildColumnDecks(spawnPieces, spawnCount);
        int spawnIndex = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (spawnIndex >= spawnCount)
                {
                    FinishInitialBoard(spawnCount);
                    return;
                }

                Piece piece = SimplePool.Spawn<Piece>(piecePrb);
                if (boardParent == null)
                {
                    boardParent = piece.transform.parent;
                }

                piece.transform.localPosition = new Vector3(
                    x * pieceSize.x - boardOffset.x,
                    y * pieceSize.y - boardOffset.y,
                    0
                );

                SpawnPieceData data = spawnPieces[spawnIndex];
                float delay = spawnIndex * flipDelayEachPiece;
                piece.Setup(data.pictureSO, data.localCell, data.sprite, x, y, playSpawnFlip, delay);
                ApplyInitialLock(piece, x, y, playSpawnFlip ? delay + piece.flipDuration : 0f);
                piece.SetSnapPosition(piece.transform.position);

                pieces[x, y] = piece;
                spawnIndex++;
            }
        }

        FinishInitialBoard(spawnCount);
    }

    void FinishInitialBoard(int spawnedPieceCount)
    {
        PrepareInitialBoardGroups();
        ScheduleShowHandTutAfterSpawnFlip(spawnedPieceCount);
    }

    void ScheduleShowHandTutAfterSpawnFlip(int spawnedPieceCount)
    {
        if (showHandTutTween != null)
        {
            showHandTutTween.Kill();
            showHandTutTween = null;
        }

        CanvasGameplay gameplay = UIManager.ins != null
            ? UIManager.ins.GetUI<CanvasGameplay>(UIID.UICGamePlay)
            : null;

        if (gameplay != null)
        {
            gameplay.ShowHandTut(false);
        }

        if (curLevel == null || !curLevel.isTutorialLevel)
        {
            return;
        }

        float delay = 0f;
        if (playSpawnFlip && spawnedPieceCount > 0 && piecePrb != null)
        {
            delay = (spawnedPieceCount - 1) * flipDelayEachPiece + piecePrb.flipDuration;
        }

        showHandTutTween = DOVirtual.DelayedCall(delay, () =>
        {
            CanvasGameplay currentGameplay = UIManager.ins != null
                ? UIManager.ins.GetUI<CanvasGameplay>(UIID.UICGamePlay)
                : null;

            if (currentGameplay != null)
            {
                currentGameplay.ShowHandTut(true);
            }

            showHandTutTween = null;
        });
    }

    void ResolveCurrentLevel()
    {
        if (GameManager.ins == null || GameManager.ins.levels == null || GameManager.ins.levels.Count == 0)
        {
            return;
        }

        int levelIndex = GetCurrentLevelIndex();
        curLevel = GameManager.ins.levels[levelIndex % GameManager.ins.levels.Count];
    }

    int GetSpawnCount(int availablePieceCount)
    {
        int boardCapacity = width * height;
        return Mathf.Min(boardCapacity, availablePieceCount);
    }

    void ApplyInitialLock(Piece piece, int x, int y, float showDelay)
    {
        if (piece == null || curLevel == null || !curLevel.hasLockPieces || curLevel.lockPieces == null)
        {
            return;
        }

        for (int i = 0; i < curLevel.lockPieces.Count; i++)
        {
            LevelLockPieceData lockData = curLevel.lockPieces[i];
            if (lockData != null && lockData.cell == new Vector2Int(x, y))
            {
                piece.SetLock(lockData.unlockImageCount, showDelay);
                return;
            }
        }
    }

    List<SpawnPieceData> BuildSpawnPieces()
    {
        return RuntimePictureBuilder.BuildSpawnPieces(curLevel, piecePrb);
    }

    int GetCurrentLevelIndex()
    {
        if (DataManager.ins == null || DataManager.ins.dt == null)
        {
            return 0;
        }

        return Mathf.Max(0, DataManager.ins.dt.level);
    }

    void BuildColumnDecks(List<SpawnPieceData> spawnPieces, int startIndex)
    {
        columnDecks = new Queue<SpawnPieceData>[width];
        for (int x = 0; x < width; x++)
        {
            columnDecks[x] = new Queue<SpawnPieceData>();
        }

        for (int i = startIndex; i < spawnPieces.Count; i++)
        {
            int column = (i - startIndex) % width;
            columnDecks[column].Enqueue(spawnPieces[i]);
        }
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

    void SortSpawnPiecesByPictureSize(List<SpawnPieceData> spawnPieces)
    {
        spawnPieces.Sort((a, b) =>
        {
            int aCount = GetPicturePieceCount(a.pictureSO);
            int bCount = GetPicturePieceCount(b.pictureSO);
            return aCount.CompareTo(bCount);
        });
    }

    int GetPicturePieceCount(PictureSO picture)
    {
        if (picture == null)
        {
            return int.MaxValue;
        }

        int sizeCount = picture.size.x * picture.size.y;
        return sizeCount > 0 ? sizeCount : picture.pieces.Count;
    }

    void EnsureOneCompletePictureInBoard(List<SpawnPieceData> spawnPieces, int boardPieceCount)
    {
        PictureSO selectedPicture = FindCompletePictureForBoard(spawnPieces, boardPieceCount);
        if (selectedPicture == null)
        {
            return;
        }

        List<SpawnPieceData> selectedPieces = new List<SpawnPieceData>();
        List<SpawnPieceData> otherPieces = new List<SpawnPieceData>();

        for (int i = 0; i < spawnPieces.Count; i++)
        {
            if (spawnPieces[i].pictureSO == selectedPicture)
            {
                selectedPieces.Add(spawnPieces[i]);
            }
            else
            {
                otherPieces.Add(spawnPieces[i]);
            }
        }

        spawnPieces.Clear();
        spawnPieces.AddRange(selectedPieces);
        spawnPieces.AddRange(otherPieces);
    }

    PictureSO FindCompletePictureForBoard(List<SpawnPieceData> spawnPieces, int boardPieceCount)
    {
        for (int i = 0; i < spawnPieces.Count; i++)
        {
            PictureSO picture = spawnPieces[i].pictureSO;
            if (picture == null)
            {
                continue;
            }

            int pieceCount = GetPicturePieceCount(picture);
            if (pieceCount <= boardPieceCount && CountPiecesOfPicture(spawnPieces, picture) >= pieceCount)
            {
                return picture;
            }
        }

        return null;
    }

    int CountPiecesOfPicture(List<SpawnPieceData> spawnPieces, PictureSO picture)
    {
        int count = 0;
        for (int i = 0; i < spawnPieces.Count; i++)
        {
            if (spawnPieces[i].pictureSO == picture)
            {
                count++;
            }
        }

        return count;
    }

    public Piece GetNearestPiece(Vector3 position, Piece ignorePiece)
    {
        return GetNearestPiece(position, ignorePiece, null);
    }

    public Piece GetNearestPiece(Vector3 position, Piece ignorePiece, PieceGroup ignoreGroup)
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

    public bool GetNearestCell(Vector3 position, Piece ignorePiece, PieceGroup ignoreGroup, out Vector2Int nearestCell)
    {
        nearestCell = Vector2Int.zero;
        if (pieces == null)
        {
            return false;
        }

        bool foundCell = false;
        float nearestDistance = GetPieceSize(piecePrb).magnitude * 0.5f;

        for (int x = 0; x < pieces.GetLength(0); x++)
        {
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                Piece piece = pieces[x, y];
                if (piece == ignorePiece
                    || (piece != null && piece.IsLock)
                    || (ignoreGroup != null && piece != null && ignoreGroup.Contains(piece)))
                {
                    continue;
                }

                float distance = Vector3.Distance(position, GetBoardWorldPosition(x, y));
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

    public bool GetNearestCellForGroup(PieceGroup group, Piece grabbedPiece, out Vector2Int targetCell)
    {
        targetCell = Vector2Int.zero;
        if (pieces == null || group == null || grabbedPiece == null)
        {
            return false;
        }

        bool foundCell = false;
        float nearestDistance = GetPieceSize(piecePrb).magnitude * 0.5f;
        float bestScore = float.MinValue;

        for (int i = 0; i < group.pieces.Count; i++)
        {
            Piece groupPiece = group.pieces[i];
            if (groupPiece == null)
            {
                continue;
            }

            for (int x = 0; x < pieces.GetLength(0); x++)
            {
                for (int y = 0; y < pieces.GetLength(1); y++)
                {
                    float distance = Vector3.Distance(groupPiece.transform.position, GetBoardWorldPosition(x, y));
                    if (distance > nearestDistance)
                    {
                        continue;
                    }

                    Vector2Int offset = new Vector2Int(x, y) - groupPiece.posInBoard;
                    if (offset == Vector2Int.zero || !IsValidGroupMoveOffset(group, offset))
                    {
                        continue;
                    }

                    int connectionCount = CountGroupOutsideConnections(group, offset);
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

    bool IsValidGroupMoveOffset(PieceGroup group, Vector2Int offset)
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
            if (!IsInBoard(newCell) || IsLockedRow(newCell))
            {
                return false;
            }

            oldCells.Add(oldCell);
            newCells.Add(newCell);
        }

        for (int i = 0; i < newCells.Count; i++)
        {
            Piece targetPiece = pieces[newCells[i].x, newCells[i].y];
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

    int CountGroupOutsideConnections(PieceGroup group, Vector2Int offset)
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
            count += CountOutsideConnection(piece, futureCell, Vector2Int.up, group, newCells);
            count += CountOutsideConnection(piece, futureCell, Vector2Int.right, group, newCells);
            count += CountOutsideConnection(piece, futureCell, Vector2Int.down, group, newCells);
            count += CountOutsideConnection(piece, futureCell, Vector2Int.left, group, newCells);
        }

        return count;
    }

    int CountOutsideConnection(Piece piece, Vector2Int futureCell, Vector2Int direction, PieceGroup group, List<Vector2Int> newCells)
    {
        Vector2Int neighborCell = futureCell + direction;
        if (!IsInBoard(neighborCell)
            || newCells.Contains(neighborCell)
            || IsLockedRow(futureCell)
            || IsLockedRow(neighborCell))
        {
            return 0;
        }

        Piece neighbor = pieces[neighborCell.x, neighborCell.y];
        if (neighbor == null || neighbor.IsLock || group.Contains(neighbor) || piece.pictureSO != neighbor.pictureSO)
        {
            return 0;
        }

        Vector2Int boardDelta = neighbor.posInBoard - futureCell;
        Vector2Int localDelta = neighbor.localCell - piece.localCell;
        return boardDelta == localDelta && Mathf.Abs(boardDelta.x) + Mathf.Abs(boardDelta.y) == 1 ? 1 : 0;
    }

    public void MovePieceToCell(Piece piece, Vector2Int targetCell)
    {
        if (IsInputLocked || piece == null || piece.IsLock || !IsInBoard(targetCell) || IsLockedRow(targetCell))
        {
            if (piece != null)
            {
                LockInputForMove();
                piece.MoveToSnapPosition();
            }

            return;
        }

        Vector2Int oldCell = piece.posInBoard;
        if (oldCell == targetCell)
        {
            LockInputForMove();
            piece.MoveToSnapPosition();
            return;
        }

        Piece targetPiece = pieces[targetCell.x, targetCell.y];
        if (targetPiece != null)
        {
            if (targetPiece.IsLock || IsLockedRow(targetCell))
            {
                LockInputForMove();
                piece.MoveToSnapPosition();
                return;
            }

            SwapPieces(piece, targetPiece);
            return;
        }

        IsInputLocked = true;
        pieces[oldCell.x, oldCell.y] = null;
        pieces[targetCell.x, targetCell.y] = piece;

        piece.SetPosInBoard(targetCell.x, targetCell.y);
        piece.SetSnapPositionOnly(GetBoardWorldPosition(targetCell.x, targetCell.y));
        piece.MoveToSnapPosition();

        if (CanConnectWithNeighbor(piece))
        {
            ApplyGravity(GetConnectedPieces(new List<Piece> { piece }));
        }
        else
        {
            ApplyGravityKeepingConnectedPieces();
        }

        RebuildGroupsAfterMove();
    }

    public void SwapPieces(Piece firstPiece, Piece secondPiece)
    {
        if (IsInputLocked || firstPiece == null || secondPiece == null || firstPiece.IsLock || secondPiece.IsLock)
        {
            return;
        }

        IsInputLocked = true;

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
        ApplyGravityKeepingConnectedPieces();

        RebuildGroupsAfterMove();
    }

    public void SwapGroup(PieceGroup group, Piece grabbedPiece, Piece targetPiece)
    {
        if (targetPiece == null)
        {
            MoveGroupBack(group);
            return;
        }

        MoveGroupToCell(group, grabbedPiece, targetPiece.posInBoard);
    }

    public void MoveGroupToCell(PieceGroup group, Piece grabbedPiece, Vector2Int targetCell)
    {
        if (IsInputLocked)
        {
            return;
        }

        if (group == null || grabbedPiece == null || GroupHasLockedPiece(group) || !IsInBoard(targetCell) || IsLockedRow(targetCell))
        {
            MoveGroupBack(group);
            return;
        }

        Vector2Int offset = targetCell - grabbedPiece.posInBoard;
        if (offset == Vector2Int.zero)
        {
            MoveGroupBack(group);
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
                MoveGroupBack(group);
                return;
            }

            Vector2Int oldCell = piece.posInBoard;
            Vector2Int newCell = oldCell + offset;
            if (!IsInBoard(newCell) || IsLockedRow(newCell))
            {
                MoveGroupBack(group);
                return;
            }

            oldCells.Add(oldCell);
            newCells.Add(newCell);
        }

        for (int i = 0; i < newCells.Count; i++)
        {
            Piece swapPiece = pieces[newCells[i].x, newCells[i].y];
            if (swapPiece == null)
            {
                hasEmptyTargetCell = true;
            }
            else if (!group.Contains(swapPiece))
            {
                if (swapPiece.IsLock || IsLockedRow(newCells[i]))
                {
                    MoveGroupBack(group);
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
            MoveGroupBack(group);
            return;
        }

        IsInputLocked = true;

        for (int i = 0; i < oldCells.Count; i++)
        {
            pieces[oldCells[i].x, oldCells[i].y] = null;
        }

        for (int i = 0; i < groupPieces.Count; i++)
        {
            Piece groupPiece = groupPieces[i];
            Vector2Int newCell = newCells[i];

            pieces[newCell.x, newCell.y] = groupPiece;
            groupPiece.SetPosInBoard(newCell.x, newCell.y);
            groupPiece.SetSnapPositionOnly(GetBoardWorldPosition(newCell.x, newCell.y));
        }

        for (int i = 0; i < swapPieces.Count; i++)
        {
            Piece swapPiece = swapPieces[i];
            Vector2Int swapToCell = swapToCells[i];

            pieces[swapToCell.x, swapToCell.y] = swapPiece;
            swapPiece.SetPosInBoard(swapToCell.x, swapToCell.y);
            swapPiece.SetSnapPositionOnly(GetBoardWorldPosition(swapToCell.x, swapToCell.y));
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
            ApplyGravityKeepingConnectedPieces();
        }

        RebuildGroupsAfterMove();
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

    void MoveGroupBack(PieceGroup group)
    {
        if (group == null)
        {
            return;
        }

        LockInputForMove();

        for (int i = 0; i < group.pieces.Count; i++)
        {
            if (group.pieces[i] != null)
            {
                group.pieces[i].MoveToSnapPosition();
            }
        }
    }

    void RebuildGroups()
    {
        RebuildGroups(true);
    }

    void RebuildGroups(bool checkCompleted)
    {
        EnsureGroupManager();
        groupManager.Rebuild(pieces, pieceGroupPrb, boardParent, transform, checkCompleted, CheckCompletedGroups);
    }

    void RebuildGroupsAfterMove()
    {
        if (rebuildTween != null)
        {
            rebuildTween.Kill();
        }

        rebuildTween = DOVirtual.DelayedCall(PieceMoveDuration, () =>
        {
            RebuildGroups();
            if (!isResolvingComplete)
            {
                CheckWin();

                IsInputLocked = false;
            }
            rebuildTween = null;
        });
    }

    void PrepareInitialBoardGroups()
    {
        RebuildGroups(false);
        BreakInitialCompletedGroups();
        RebuildGroups(false);
    }

    void BreakInitialCompletedGroups()
    {
        int guard = 0;
        while (TryGetCompletedGroup(out PieceGroup completedGroup) && guard < 100)
        {
            Piece pieceInCompletedGroup = GetFirstPiece(completedGroup);
            Piece swapPiece = FindSwapPieceOutsideGroup(completedGroup);
            if (pieceInCompletedGroup == null || swapPiece == null)
            {
                return;
            }

            SwapPieceDataOnly(pieceInCompletedGroup, swapPiece);
            RebuildGroups(false);
            guard++;
        }
    }

    bool TryGetCompletedGroup(out PieceGroup completedGroup)
    {
        completedGroup = null;
        EnsureGroupManager();
        for (int i = 0; i < groupManager.ActiveGroups.Count; i++)
        {
            PieceGroup group = groupManager.ActiveGroups[i];
            if (group != null && IsCompletedGroup(group))
            {
                completedGroup = group;
                return true;
            }
        }

        return false;
    }

    Piece GetFirstPiece(PieceGroup group)
    {
        if (group == null)
        {
            return null;
        }

        for (int i = 0; i < group.pieces.Count; i++)
        {
            if (group.pieces[i] != null)
            {
                return group.pieces[i];
            }
        }

        return null;
    }

    Piece FindSwapPieceOutsideGroup(PieceGroup group)
    {
        for (int x = 0; x < pieces.GetLength(0); x++)
        {
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                Piece piece = pieces[x, y];
                if (piece != null && !piece.IsLock && !group.Contains(piece))
                {
                    return piece;
                }
            }
        }

        return null;
    }

    void SwapPieceDataOnly(Piece firstPiece, Piece secondPiece)
    {
        Vector2Int firstCell = firstPiece.posInBoard;
        Vector2Int secondCell = secondPiece.posInBoard;
        Vector3 firstSnap = firstPiece.GetSnapPosition();
        Vector3 secondSnap = secondPiece.GetSnapPosition();

        pieces[firstCell.x, firstCell.y] = secondPiece;
        pieces[secondCell.x, secondCell.y] = firstPiece;

        firstPiece.SetPosInBoard(secondCell.x, secondCell.y);
        secondPiece.SetPosInBoard(firstCell.x, firstCell.y);

        firstPiece.SetSnapPositionOnly(secondSnap);
        secondPiece.SetSnapPositionOnly(firstSnap);

        firstPiece.SetSnapPosition(secondSnap);
        secondPiece.SetSnapPosition(firstSnap);
    }

    public void LockInputForMove()
    {
        IsInputLocked = true;

        if (rebuildTween != null)
        {
            rebuildTween.Kill();
        }

        rebuildTween = DOVirtual.DelayedCall(PieceMoveDuration, () =>
        {
            IsInputLocked = false;
            rebuildTween = null;
        });
    }
    void CheckCompletedGroups()
    {
        List<PieceGroup> completedGroups = new List<PieceGroup>();

        EnsureGroupManager();
        for (int i = 0; i < groupManager.ActiveGroups.Count; i++)
        {
            PieceGroup group = groupManager.ActiveGroups[i];
            if (group != null && IsCompletedGroup(group))
            {
                completedGroups.Add(group);
            }
        }

        if (completedGroups.Count == 0)
        {
            return;
        }

        isResolvingComplete = true;
        IsInputLocked = true;

        for (int i = 0; i < completedGroups.Count; i++)
        {
            PieceGroup group = completedGroups[i];
            group.transform.DOKill();
            PlayCompleteFlash(group);
            DOVirtual.DelayedCall(CompleteFlashDuration, () =>
            {
                if (group != null)
                {
                    group.transform.DOScale(Vector3.zero, CompleteClearDuration).SetEase(Ease.InBack);
                }
            });
        }

        DOVirtual.DelayedCall(CompleteFlashDuration + CompleteClearDuration, () =>
        {
            ClearCompletedGroups(completedGroups);
            DecreaseAllLockPieces();
            ApplyGravity();
            RebuildGroupsAfterMove();
            isResolvingComplete = false;
        });
    }

    void PlayCompleteFlash(PieceGroup group)
    {
        if (group == null)
        {
            return;
        }

        for (int i = 0; i < group.pieces.Count; i++)
        {
            Piece piece = group.pieces[i];
            if (piece == null || piece.pieceSprite == null)
            {
                continue;
            }

            SpriteRenderer renderer = piece.pieceSprite;
            Color startColor = renderer.color;
            renderer.DOKill();
            renderer.DOColor(Color.white, CompleteFlashDuration * 0.45f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (renderer != null)
                    {
                        renderer.DOColor(startColor, CompleteFlashDuration * 0.55f).SetEase(Ease.InQuad);
                    }
                });
        }
    }

    bool IsCompletedGroup(PieceGroup group)
    {
        if (group == null || group.pieces.Count == 0)
        {
            return false;
        }

        PictureSO picture = group.pieces[0] != null ? group.pieces[0].pictureSO : null;
        if (picture == null)
        {
            return false;
        }

        int requiredCount = picture.size.x * picture.size.y;
        if (requiredCount <= 0 || group.pieces.Count != requiredCount)
        {
            return false;
        }

        HashSet<Vector2Int> localCells = new HashSet<Vector2Int>();
        for (int i = 0; i < group.pieces.Count; i++)
        {
            Piece piece = group.pieces[i];
            if (piece == null || piece.pictureSO != picture)
            {
                return false;
            }

            if (piece.localCell.x < 0 || piece.localCell.x >= picture.size.x
                || piece.localCell.y < 0 || piece.localCell.y >= picture.size.y)
            {
                return false;
            }

            localCells.Add(piece.localCell);
        }

        return localCells.Count == requiredCount;
    }

    void ClearCompletedGroups(List<PieceGroup> completedGroups)
    {
        Transform parent = boardParent != null ? boardParent : transform;

        for (int i = 0; i < completedGroups.Count; i++)
        {
            PieceGroup group = completedGroups[i];
            if (group == null)
            {
                continue;
            }

            for (int j = 0; j < group.pieces.Count; j++)
            {
                Piece piece = group.pieces[j];
                if (piece == null)
                {
                    continue;
                }

                Vector2Int cell = piece.posInBoard;
                if (IsInBoard(cell) && pieces[cell.x, cell.y] == piece)
                {
                    pieces[cell.x, cell.y] = null;
                }

                piece.transform.SetParent(parent, true);
                piece.transform.localScale = Vector3.one;
                SimplePool.Despawn(piece);
            }

            groupManager.ActiveGroups.Remove(group);
            SimplePool.Despawn(group);
        }
    }

    void DecreaseAllLockPieces()
    {
        if (pieces == null)
        {
            return;
        }

        for (int x = 0; x < pieces.GetLength(0); x++)
        {
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                Piece piece = pieces[x, y];
                if (piece != null && piece.IsLock)
                {
                    piece.DecreaseLock();
                }
            }
        }
    }

    void ApplyGravity()
    {
        ApplyGravity((Piece)null, false);
    }

    void ApplyGravity(List<Piece> pinnedPieces)
    {
        HashSet<Piece> pinnedSet = new HashSet<Piece>();
        if (pinnedPieces != null)
        {
            for (int i = 0; i < pinnedPieces.Count; i++)
            {
                if (pinnedPieces[i] != null)
                {
                    pinnedSet.Add(pinnedPieces[i]);
                }
            }
        }

        ApplyGravity(pinnedSet);
    }

    void ApplyGravityKeepingConnectedPieces()
    {
        ApplyGravity((Piece)null, true);
    }

    void ApplyGravity(Piece pinnedPiece, bool keepConnectedPieces)
    {
        HashSet<Piece> pinnedPieces = BuildGravityPinnedPieces(pinnedPiece, keepConnectedPieces);
        ApplyGravity(pinnedPieces);
    }

    void ApplyGravity(HashSet<Piece> pinnedPieces)
    {
        EnsureGroupManager();
        EnsureGravityManager();
        gravityManager.Apply(pieces, columnDecks, piecePrb, boardParent, transform, width, height, pinnedPieces, groupManager);
    }

    HashSet<Piece> BuildGravityPinnedPieces(Piece pinnedPiece, bool keepConnectedPieces)
    {
        EnsureGravityManager();
        return gravityManager.BuildPinnedPieces(pieces, pinnedPiece, keepConnectedPieces);
    }

    List<Piece> GetConnectedPieces(List<Piece> startPieces)
    {
        EnsureGravityManager();
        return gravityManager.GetConnectedPieces(pieces, startPieces);
    }

    bool CanConnectWithNeighbor(Piece piece)
    {
        EnsureGravityManager();
        return gravityManager.CanConnectWithNeighbor(pieces, piece);
    }

    bool CanGroup(Piece a, Piece b)
    {
        EnsureGroupManager();
        return groupManager.CanGroup(a, b);
    }

    bool IsInBoard(Vector2Int cell)
    {
        return pieces != null
            && cell.x >= 0
            && cell.x < pieces.GetLength(0)
            && cell.y >= 0
            && cell.y < pieces.GetLength(1);
    }

    public bool IsLockedRow(Vector2Int cell)
    {
        return curLevel != null
            && curLevel.lockTopRows
            && lockedTopRows > 0
            && IsInBoard(cell)
            && cell.y >= height - lockedTopRows;
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

    Vector3 GetBoardWorldPosition(int x, int y)
    {
        Vector2 pieceSize = GetPieceSize(piecePrb);
        Vector2 boardOffset = new Vector2(
            (width - 1) * pieceSize.x * 0.5f,
            (height - 1) * pieceSize.y * 0.5f
        );

        Vector3 localPosition = new Vector3(
            x * pieceSize.x - boardOffset.x,
            y * pieceSize.y - boardOffset.y,
            0
        );

        return boardParent != null ? boardParent.TransformPoint(localPosition) : localPosition;
    }

    public void BoosterHint()
    {
        Piece pieceA = null, pieceB = null;

        List<Piece> boardPieces = new List<Piece>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Piece piece = pieces[x, y];
                if (piece == null)
                    continue;

                if (piece.IsLock)
                    continue;

                if (IsLockedRow(piece.posInBoard))
                    continue;

                boardPieces.Add(piece);
            }
        }

        for (int i = 0; i < boardPieces.Count; i++)
        {
            for (int j = i + 1; j < boardPieces.Count; j++)
            {
                Piece a = boardPieces[i];
                Piece b = boardPieces[j];

                if (a.pictureSO != b.pictureSO)
                    continue;

                Vector2Int localDelta = b.localCell - a.localCell;

                bool isNeighborInOriginal =
                    Mathf.Abs(localDelta.x) + Mathf.Abs(localDelta.y) == 1;

                if (!isNeighborInOriginal)
                    continue;

                Vector2Int boardDelta = b.posInBoard - a.posInBoard;

                bool alreadyCorrect =
                    boardDelta == localDelta;

                if (alreadyCorrect)
                    continue;

                pieceA = a;
                pieceB = b;
            }
        }

        if (pieceA != null && pieceB != null)
        {
            int orgOd = pieceA.spriteRenderer.sortingOrder;
            pieceA.SetOrderOffset(orgOd + 7);
            pieceB.SetOrderOffset(orgOd + 7);
            pieceA.transform.DOScale(Vector3.one * 1.2f, 0.3f).SetLoops(2, LoopType.Yoyo).OnComplete(() => pieceA.SetOrderOffset(orgOd));
            pieceB.transform.DOScale(Vector3.one * 1.2f, 0.3f).SetLoops(2, LoopType.Yoyo).OnComplete(() => pieceB.SetOrderOffset(orgOd));
        }
    }

    public bool IsWin()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (pieces[x, y] != null)
                {
                    return false;
                }
            }
        }

        if (columnDecks != null)
        {
            for (int i = 0; i < columnDecks.Length; i++)
            {
                if (columnDecks[i] != null && columnDecks[i].Count > 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public void CheckWin()
    {
        if (IsWin())
        {
            UIManager.ins.OpenUI(UIID.UICVictory);
            DataManager.ins.dt.level++;
            Debug.Log("You win!");
        }
    }
}

