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
    public bool playSpawnFlip = true;
    public float flipDelayEachPiece = 0.02f;
    const float PieceMoveDuration = 0.17f;
    const float CompleteClearDuration = 0.5f;

    public Piece[,] pieces;
    Queue<SpawnPieceData>[] columnDecks;
    readonly List<PieceGroup> activeGroups = new List<PieceGroup>();
    Transform boardParent;
    Tween rebuildTween;
    bool isResolvingComplete;

    public bool IsInputLocked { get; private set; }

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

        SortSpawnPiecesByPictureSize(spawnPieces);

        Vector2 pieceSize = GetPieceSize(piecePrb);
        Vector2 boardOffset = new Vector2(
            (width - 1) * pieceSize.x * 0.5f,
            (height - 1) * pieceSize.y * 0.5f
        );

        int spawnCount = GetSpawnCount(spawnPieces.Count);
        BuildColumnDecks(spawnPieces, spawnCount);
        int spawnIndex = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (spawnIndex >= spawnCount)
                {
                    RebuildGroups();
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
                piece.SetSnapPosition(piece.transform.position);

                pieces[x, y] = piece;
                spawnIndex++;
            }
        }

        RebuildGroups();
    }

    int GetSpawnCount(int availablePieceCount)
    {
        int boardCapacity = width * height;
        return Mathf.Min(boardCapacity, availablePieceCount);
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
                if (piece == ignorePiece || (ignoreGroup != null && piece != null && ignoreGroup.Contains(piece)))
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

    public void MovePieceToCell(Piece piece, Vector2Int targetCell)
    {
        if (IsInputLocked || piece == null || !IsInBoard(targetCell))
        {
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
            SwapPieces(piece, targetPiece);
            return;
        }

        IsInputLocked = true;
        pieces[oldCell.x, oldCell.y] = null;
        pieces[targetCell.x, targetCell.y] = piece;

        piece.SetPosInBoard(targetCell.x, targetCell.y);
        piece.SetSnapPositionOnly(GetBoardWorldPosition(targetCell.x, targetCell.y));
        piece.MoveToSnapPosition();
        ApplyGravity();

        RebuildGroupsAfterMove();
    }

    public void SwapPieces(Piece firstPiece, Piece secondPiece)
    {
        if (IsInputLocked)
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

        if (group == null || grabbedPiece == null || !IsInBoard(targetCell))
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
            if (!IsInBoard(newCell))
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
            ApplyGravity();
        }

        RebuildGroupsAfterMove();
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

    Vector3 GetSnapPositionBeforeSwap(Vector2Int cell, List<Vector2Int> cells, List<Vector3> snaps)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] == cell)
            {
                return snaps[i];
            }
        }

        Piece piece = pieces[cell.x, cell.y];
        return piece != null ? piece.GetSnapPosition() : Vector3.zero;
    }

    void RebuildGroups()
    {
        if (pieces == null || pieceGroupPrb == null)
        {
            return;
        }

        List<HashSet<string>> oldGroupSets = GetActiveGroupSets();
        ClearGroups();

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

                List<Piece> groupPieces = BuildGroup(piece, visited);
                if (groupPieces.Count >= 2)
                {
                    CreateGroup(groupPieces, oldGroupSets);
                }
            }
        }

        CheckCompletedGroups();
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
                IsInputLocked = false;
            }
            rebuildTween = null;
        });
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

    void ClearGroups()
    {
        Transform parent = boardParent != null ? boardParent : transform;

        for (int i = 0; i < activeGroups.Count; i++)
        {
            if (activeGroups[i] != null)
            {
                activeGroups[i].transform.DOKill();
                activeGroups[i].transform.localScale = Vector3.one;
            }
        }

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

        for (int i = 0; i < activeGroups.Count; i++)
        {
            if (activeGroups[i] != null)
            {
                SimplePool.Despawn(activeGroups[i]);
            }
        }

        activeGroups.Clear();
    }

    void CheckCompletedGroups()
    {
        List<PieceGroup> completedGroups = new List<PieceGroup>();

        for (int i = 0; i < activeGroups.Count; i++)
        {
            PieceGroup group = activeGroups[i];
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
            group.transform.DOScale(Vector3.zero, CompleteClearDuration).SetEase(Ease.InBack);
        }

        DOVirtual.DelayedCall(CompleteClearDuration, () =>
        {
            ClearCompletedGroups(completedGroups);
            ApplyGravity();
            RebuildGroupsAfterMove();
            isResolvingComplete = false;
        });
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

            activeGroups.Remove(group);
            SimplePool.Despawn(group);
        }
    }

    void ApplyGravity()
    {
        ClearGroups();

        for (int x = 0; x < width; x++)
        {
            int writeY = 0;

            for (int readY = 0; readY < height; readY++)
            {
                Piece piece = pieces[x, readY];
                if (piece == null)
                {
                    continue;
                }

                if (readY != writeY)
                {
                    pieces[x, writeY] = piece;
                    pieces[x, readY] = null;

                    piece.SetPosInBoard(x, writeY);
                    piece.SetSnapPositionOnly(GetBoardWorldPosition(x, writeY));
                    piece.MoveToSnapPosition();
                }

                writeY++;
            }

            FillColumnFromDeck(x, writeY);
        }
    }

    void FillColumnFromDeck(int x, int startY)
    {
        if (columnDecks == null || x < 0 || x >= columnDecks.Length || columnDecks[x] == null)
        {
            return;
        }

        int spawnOrder = 0;
        for (int y = startY; y < height; y++)
        {
            if (columnDecks[x].Count == 0)
            {
                break;
            }

            SpawnPieceData data = columnDecks[x].Dequeue();
            Piece piece = SimplePool.Spawn<Piece>(piecePrb);
            if (boardParent != null)
            {
                piece.transform.SetParent(boardParent, false);
            }

            Vector3 targetPosition = GetBoardWorldPosition(x, y);
            Vector3 spawnPosition = GetBoardWorldPosition(x, height + spawnOrder);
            piece.transform.position = spawnPosition;
            piece.transform.localScale = Vector3.one;

            piece.Setup(data.pictureSO, data.localCell, data.sprite, x, y, false, 0f);
            piece.SetSnapPositionOnly(targetPosition);
            pieces[x, y] = piece;
            piece.MoveToSnapPosition();
            spawnOrder++;
        }
    }

    List<Piece> BuildGroup(Piece startPiece, HashSet<Piece> visited)
    {
        List<Piece> groupPieces = new List<Piece>();
        Queue<Piece> queue = new Queue<Piece>();

        visited.Add(startPiece);
        queue.Enqueue(startPiece);

        while (queue.Count > 0)
        {
            Piece piece = queue.Dequeue();
            groupPieces.Add(piece);

            TryAddNeighbor(piece, Vector2Int.up, visited, queue);
            TryAddNeighbor(piece, Vector2Int.right, visited, queue);
            TryAddNeighbor(piece, Vector2Int.down, visited, queue);
            TryAddNeighbor(piece, Vector2Int.left, visited, queue);
        }

        return groupPieces;
    }

    void TryAddNeighbor(Piece piece, Vector2Int direction, HashSet<Piece> visited, Queue<Piece> queue)
    {
        Vector2Int neighborCell = piece.posInBoard + direction;
        if (!IsInBoard(neighborCell))
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

    bool CanGroup(Piece a, Piece b)
    {
        if (a == null || b == null || a.pictureSO != b.pictureSO)
        {
            return false;
        }

        Vector2Int boardDelta = b.posInBoard - a.posInBoard;
        Vector2Int localDelta = b.localCell - a.localCell;
        return boardDelta == localDelta && Mathf.Abs(boardDelta.x) + Mathf.Abs(boardDelta.y) == 1;
    }

    void CreateGroup(List<Piece> groupPieces, List<HashSet<string>> oldGroupSets)
    {
        Vector3 center = Vector3.zero;
        for (int i = 0; i < groupPieces.Count; i++)
        {
            center += groupPieces[i].transform.position;
        }

        center /= groupPieces.Count;

        PieceGroup group = SimplePool.Spawn<PieceGroup>(pieceGroupPrb);
        group.transform.position = center;
        group.transform.rotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
        group.Setup(groupPieces);
        activeGroups.Add(group);

        for (int i = 0; i < groupPieces.Count; i++)
        {
            groupPieces[i].transform.SetParent(group.transform, true);
        }

        if (!IsCompletedGroup(group) && ShouldScaleNewGroup(groupPieces, oldGroupSets))
        {
            PlayGroupMergeScaleWhenStable(group);
        }
    }

    List<HashSet<string>> GetActiveGroupSets()
    {
        List<HashSet<string>> sets = new List<HashSet<string>>();
        for (int i = 0; i < activeGroups.Count; i++)
        {
            PieceGroup group = activeGroups[i];
            if (group != null && group.pieces.Count >= 2)
            {
                sets.Add(BuildGroupIdSet(group.pieces));
            }
        }

        return sets;
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
        for (int i = 0; i < oldGroupSets.Count; i++)
        {
            if (newSet.SetEquals(oldGroupSets[i]))
            {
                return false;
            }
        }

        return true;
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
            if (group != null && activeGroups.Contains(group) && !IsCompletedGroup(group))
            {
                PlayGroupMergeScale(group);
            }
        });
    }

    bool IsInBoard(Vector2Int cell)
    {
        return pieces != null
            && cell.x >= 0
            && cell.x < pieces.GetLength(0)
            && cell.y >= 0
            && cell.y < pieces.GetLength(1);
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
    
    //Drag drop
}
