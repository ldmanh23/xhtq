using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    internal Queue<SpawnPieceData>[] columnDecks;
    PuzzleBoardSpawner boardSpawner;
    PuzzleMoveManager moveManager;
    PuzzleGroupManager groupManager;
    PuzzleGravityManager gravityManager;
    internal Transform boardParent;
    Tween rebuildTween;
    Tween showHandTutTween;
    float remainingTime;
    bool isResolvingComplete;
    bool isTimerRunning;
    bool isGameEnded;

    public bool IsInputLocked { get; internal set; }
    public float RemainingTime => remainingTime;

    public struct SpawnPieceData
    {
        public PictureSO pictureSO;
        public Vector2Int localCell;
        public Sprite sprite;
    }

    private void Start()
    {
        EnsureGroupManager();
        UIManager.ins.OpenUI(UIID.UICGamePlay);
        BuildBoard();
        StartLevelTimer();
    }

    private void Update()
    {
        UpdateLevelTimer();
    }

    void StartLevelTimer()
    {
        isGameEnded = false;
        remainingTime = curLevel != null ? Mathf.Max(0, curLevel.timer) : 0;
        isTimerRunning = remainingTime > 0;
        UpdateTimerUI();
    }

    void UpdateLevelTimer()
    {
        if (!isTimerRunning || isGameEnded)
        {
            return;
        }

        remainingTime -= Time.deltaTime;
        UpdateTimerUI();
        if (remainingTime > 0)
        {
            return;
        }

        remainingTime = 0;
        isTimerRunning = false;
        UpdateTimerUI();
        CheckLose();
    }

    void UpdateTimerUI()
    {
        CanvasGameplay gameplay = UIManager.ins != null
            ? UIManager.ins.GetUI<CanvasGameplay>(UIID.UICGamePlay)
            : null;

        if (gameplay != null)
        {
            gameplay.SetTimer(remainingTime);
        }
    }

    void EnsureGroupManager()
    {
        if (groupManager == null)
        {
            groupManager = PuzzleGroupManager.Instance;
        }

        groupManager.Initialize(IsInBoard, IsLockedRow, IsCompletedGroup);
    }

    void EnsureBoardSpawner()
    {
        if (boardSpawner == null)
        {
            boardSpawner = PuzzleBoardSpawner.Instance;
        }
    }

    void EnsureGravityManager()
    {
        if (gravityManager == null)
        {
            gravityManager = PuzzleGravityManager.Instance;
        }

        gravityManager.Initialize(IsInBoard, CanGroup, GetBoardWorldPosition);
    }

    public void BuildBoard()
    {
        EnsureBoardSpawner();
        boardSpawner.BuildBoard(this);
    }

    void EnsureMoveManager()
    {
        if (moveManager == null)
        {
            moveManager = PuzzleMoveManager.Instance;
        }
    }

    internal void FinishInitialBoard(int spawnedPieceCount)
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

    public Piece GetNearestPiece(Vector3 position, Piece ignorePiece)
    {
        return GetNearestPiece(position, ignorePiece, null);
    }

    public Piece GetNearestPiece(Vector3 position, Piece ignorePiece, PieceGroup ignoreGroup)
    {
        EnsureMoveManager();
        return moveManager.GetNearestPiece(this, position, ignorePiece, ignoreGroup);
    }

    public bool GetNearestCell(Vector3 position, Piece ignorePiece, PieceGroup ignoreGroup, out Vector2Int nearestCell)
    {
        EnsureMoveManager();
        return moveManager.GetNearestCell(this, position, ignorePiece, ignoreGroup, out nearestCell);
    }

    public bool GetNearestCellForGroup(PieceGroup group, Piece grabbedPiece, out Vector2Int targetCell)
    {
        EnsureMoveManager();
        return moveManager.GetNearestCellForGroup(this, group, grabbedPiece, out targetCell);
    }

    public void MovePieceToCell(Piece piece, Vector2Int targetCell)
    {
        EnsureMoveManager();
        moveManager.MovePieceToCell(this, piece, targetCell);
    }

    public void SwapPieces(Piece firstPiece, Piece secondPiece)
    {
        EnsureMoveManager();
        moveManager.SwapPieces(this, firstPiece, secondPiece);
    }

    public void SwapGroup(PieceGroup group, Piece grabbedPiece, Piece targetPiece)
    {
        EnsureMoveManager();
        moveManager.SwapGroup(this, group, grabbedPiece, targetPiece);
    }

    public void MoveGroupToCell(PieceGroup group, Piece grabbedPiece, Vector2Int targetCell)
    {
        EnsureMoveManager();
        moveManager.MoveGroupToCell(this, group, grabbedPiece, targetCell);
    }

    void MoveGroupBack(PieceGroup group)
    {
        EnsureMoveManager();
        moveManager.MoveGroupBack(this, group);
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

    internal void ClearGroupsForBooster()
    {
        EnsureGroupManager();
        groupManager.ClearGroups(pieces, boardParent, transform);
    }

    internal void RebuildGroupsForBooster()
    {
        RebuildGroups();
    }

    internal bool IsResolvingCompleteForBooster => isResolvingComplete;
    internal bool IsGameEndedForBooster => isGameEnded;

    internal void RebuildGroupsAfterMove()
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

                if (!isGameEnded)
                {
                    IsInputLocked = false;
                }
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
            if (!isGameEnded)
            {
                IsInputLocked = false;
            }
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
                piece.SetCurrentGroup(null);
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

    internal void ApplyGravity(List<Piece> pinnedPieces)
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

    internal void ApplyGravityKeepingConnectedPieces()
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

    internal List<Piece> GetConnectedPieces(List<Piece> startPieces)
    {
        EnsureGravityManager();
        return gravityManager.GetConnectedPieces(pieces, startPieces);
    }

    internal bool CanConnectWithNeighbor(Piece piece)
    {
        EnsureGravityManager();
        return gravityManager.CanConnectWithNeighbor(pieces, piece);
    }

    internal bool CanGroup(Piece a, Piece b)
    {
        EnsureGroupManager();
        return groupManager.CanGroup(a, b);
    }

    internal bool IsInBoard(Vector2Int cell)
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

    public void UnlockTopLockedRows()
    {
        if (lockedTopRows <= 0)
        {
            return;
        }

        lockedTopRows = 0;
        RebuildGroups();
    }

    internal Vector2 GetPieceSize(Piece piece)
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

    internal Vector3 GetBoardWorldPosition(int x, int y)
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
        BoosterManager.Instance.BoosterHint(this);
    }

    public void BoosterClear()
    {
        BoosterManager.Instance.BoosterClear(this);
    }

    public void BoosterSort()
    {
        BoosterManager.Instance.BoosterSort(this);
    }

    public void RestartLevel()
    {
        if (rebuildTween != null)
        {
            rebuildTween.Kill();
            rebuildTween = null;
        }

        if (showHandTutTween != null)
        {
            showHandTutTween.Kill();
            showHandTutTween = null;
        }

        isTimerRunning = false;
        isGameEnded = true;
        IsInputLocked = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(Constant.SCENE_GAMEPLAY);
    }

    public bool HasMergeablePairOnBoard()
    {
        if (pieces == null)
        {
            return false;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Piece piece = pieces[x, y];
                if (!CanCheckMergeablePairPiece(piece))
                {
                    continue;
                }

                if (CanMergeWithAnyBoardPiece(piece))
                {
                    return true;
                }
            }
        }

        return false;
    }

    bool CanMergeWithAnyBoardPiece(Piece piece)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Piece other = pieces[x, y];
                if (other == piece || !CanCheckMergeablePairPiece(other) || piece.pictureSO != other.pictureSO)
                {
                    continue;
                }

                Vector2Int localDelta = other.localCell - piece.localCell;
                if (Mathf.Abs(localDelta.x) + Mathf.Abs(localDelta.y) != 1)
                {
                    continue;
                }

                if (CanPlaceForMerge(piece, other, localDelta) || CanPlaceForMerge(other, piece, -localDelta))
                {
                    return true;
                }
            }
        }

        return false;
    }

    bool CanPlaceForMerge(Piece fixedPiece, Piece movingPiece, Vector2Int localDelta)
    {
        Vector2Int targetCell = fixedPiece.posInBoard + localDelta;
        if (!IsInBoard(targetCell) || IsLockedRow(targetCell))
        {
            return false;
        }

        Piece targetPiece = pieces[targetCell.x, targetCell.y];
        return targetPiece == movingPiece || targetPiece == null || !targetPiece.IsLock;
    }

    bool CanCheckMergeablePairPiece(Piece piece)
    {
        return piece != null
            && !piece.IsLock
            && !IsLockedRow(piece.posInBoard)
            && piece.pictureSO != null;
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
        if (isGameEnded || !IsWin())
        {
            return;
        }

        isGameEnded = true;
        isTimerRunning = false;
        IsInputLocked = true;
        UIManager.ins.OpenUI(UIID.UICVictory);
        DataManager.ins.dt.level++;
        Debug.Log("You win!");
    }

    public bool IsLose()
    {
        return !isGameEnded && curLevel != null && curLevel.timer > 0 && remainingTime <= 0 && !IsWin();
    }

    public void CheckLose()
    {
        if (!IsLose())
        {
            return;
        }

        isGameEnded = true;
        isTimerRunning = false;
        IsInputLocked = true;
        UIManager.ins.OpenUI(UIID.UICFail);
        Debug.Log("You lose!");
    }
}

