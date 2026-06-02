using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class BoosterManager : Singleton<BoosterManager>
{
    struct SortAssignment
    {
        public IngameManager.SpawnPieceData data;
        public Vector2Int cell;
    }

    struct ClearAssignment
    {
        public Piece piece;
        public IngameManager.SpawnPieceData data;
        public Vector2Int cell;
        public bool replaceDeckData;
        public IngameManager.SpawnPieceData oldData;
    }

    public void BoosterHint(IngameManager manager)
    {
        Piece pieceA = null, pieceB = null;
        List<Piece> boardPieces = new List<Piece>();

        for (int x = 0; x < manager.width; x++)
        {
            for (int y = 0; y < manager.height; y++)
            {
                Piece piece = manager.pieces[x, y];
                if (piece == null || piece.IsLock || manager.IsLockedRow(piece.posInBoard))
                {
                    continue;
                }

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
                {
                    continue;
                }

                Vector2Int localDelta = b.localCell - a.localCell;
                if (Mathf.Abs(localDelta.x) + Mathf.Abs(localDelta.y) != 1)
                {
                    continue;
                }

                Vector2Int boardDelta = b.posInBoard - a.posInBoard;
                if (boardDelta == localDelta)
                {
                    continue;
                }

                pieceA = a;
                pieceB = b;
            }
        }

        if (pieceA == null || pieceB == null)
        {
            return;
        }

        pieceA.SetOrderOffset(7);
        pieceB.SetOrderOffset(7);
        pieceA.transform.DOScale(Vector3.one * 1.2f, 0.3f).SetLoops(2, LoopType.Yoyo).OnComplete(() => pieceA.SetOrderOffset(0));
        pieceB.transform.DOScale(Vector3.one * 1.2f, 0.3f).SetLoops(2, LoopType.Yoyo).OnComplete(() => pieceB.SetOrderOffset(0));
    }

    public void BoosterClear(IngameManager manager)
    {
        if (manager.IsInputLocked || manager.pieces == null)
        {
            return;
        }

        List<Piece> candidates = GetBoosterClearCandidates(manager);
        while (candidates.Count > 0)
        {
            int index = Random.Range(0, candidates.Count);
            Piece anchorPiece = candidates[index];
            candidates.RemoveAt(index);

            if (TryApplyBoosterClear(manager, anchorPiece))
            {
                return;
            }
        }

        Debug.Log("No valid piece for BoosterClear.");
    }

    List<Piece> GetBoosterClearCandidates(IngameManager manager)
    {
        List<Piece> candidates = new List<Piece>();
        for (int x = 0; x < manager.width; x++)
        {
            for (int y = 0; y < manager.height; y++)
            {
                Piece piece = manager.pieces[x, y];
                if (piece == null || piece.IsLock || manager.IsLockedRow(piece.posInBoard) || piece.pictureSO == null)
                {
                    continue;
                }

                candidates.Add(piece);
            }
        }

        return candidates;
    }

    bool TryApplyBoosterClear(IngameManager manager, Piece anchorPiece)
    {
        if (anchorPiece == null || anchorPiece.pictureSO == null)
        {
            return false;
        }

        PictureSO picture = anchorPiece.pictureSO;
        int requiredCount = GetPictureRequiredCount(picture);
        if (requiredCount <= 0)
        {
            return false;
        }

        List<Piece> picturePieces = GetBoardPiecesOfPicture(manager, picture);
        Dictionary<Vector2Int, Piece> boardPiecesByLocalCell = BuildBoardPiecesByLocalCell(picturePieces, picture);
        if (boardPiecesByLocalCell.Count != picturePieces.Count)
        {
            return false;
        }

        List<IngameManager.SpawnPieceData> deckData = GetAllDeckSpawnData(manager);
        List<ClearAssignment> assignments = new List<ClearAssignment>();
        HashSet<Vector2Int> targetCells = new HashSet<Vector2Int>();
        HashSet<Piece> assignmentPieces = new HashSet<Piece>(picturePieces);

        for (int y = 0; y < picture.size.y; y++)
        {
            for (int x = 0; x < picture.size.x; x++)
            {
                Vector2Int localCell = new Vector2Int(x, y);
                Vector2Int targetCell = anchorPiece.posInBoard + (localCell - anchorPiece.localCell);
                if (!manager.IsInBoard(targetCell) || manager.IsLockedRow(targetCell) || !targetCells.Add(targetCell))
                {
                    return false;
                }

                Piece targetPiece = manager.pieces[targetCell.x, targetCell.y];
                if (targetPiece != null && targetPiece.IsLock)
                {
                    return false;
                }

                if (boardPiecesByLocalCell.TryGetValue(localCell, out Piece boardPiece))
                {
                    assignments.Add(new ClearAssignment
                    {
                        piece = boardPiece,
                        data = GetSpawnData(boardPiece),
                        cell = targetCell
                    });
                    assignmentPieces.Add(boardPiece);
                    continue;
                }

                if (!TryFindSpawnData(deckData, picture, localCell, out IngameManager.SpawnPieceData missingData))
                {
                    return false;
                }

                Piece carrier = FindClearCarrierPiece(manager, assignmentPieces, targetCells);
                if (carrier == null)
                {
                    return false;
                }

                assignments.Add(new ClearAssignment
                {
                    piece = carrier,
                    data = missingData,
                    cell = targetCell,
                    replaceDeckData = true,
                    oldData = GetSpawnData(carrier)
                });
                assignmentPieces.Add(carrier);
            }
        }

        List<Piece> displacedPieces = new List<Piece>();
        foreach (Vector2Int targetCell in targetCells)
        {
            Piece targetPiece = manager.pieces[targetCell.x, targetCell.y];
            if (targetPiece != null && !assignmentPieces.Contains(targetPiece))
            {
                displacedPieces.Add(targetPiece);
            }
        }

        List<Vector2Int> freeSourceCells = new List<Vector2Int>();
        for (int i = 0; i < assignments.Count; i++)
        {
            Vector2Int sourceCell = assignments[i].piece.posInBoard;
            if (!targetCells.Contains(sourceCell))
            {
                freeSourceCells.Add(sourceCell);
            }
        }

        if (displacedPieces.Count > freeSourceCells.Count)
        {
            return false;
        }

        for (int i = 0; i < assignments.Count; i++)
        {
            if (!assignments[i].replaceDeckData)
            {
                continue;
            }

            if (!RemoveSpawnDataFromDeck(manager, assignments[i].data))
            {
                return false;
            }

            AddSpawnDataToDeck(manager, assignments[i].oldData, i);
        }

        manager.IsInputLocked = true;
        manager.ClearGroupsForBooster();

        for (int i = 0; i < assignments.Count; i++)
        {
            ClearBoardCellIfContains(manager, assignments[i].piece);
        }

        for (int i = 0; i < displacedPieces.Count; i++)
        {
            ClearBoardCellIfContains(manager, displacedPieces[i]);
        }

        for (int i = 0; i < assignments.Count; i++)
        {
            ClearAssignment assignment = assignments[i];
            if (assignment.replaceDeckData)
            {
                assignment.piece.ApplySpawnData(assignment.data);
            }

            PlacePieceAtCell(manager, assignment.piece, assignment.cell, assignment.piece != anchorPiece);
        }

        for (int i = 0; i < displacedPieces.Count; i++)
        {
            PlacePieceAtCell(manager, displacedPieces[i], freeSourceCells[i], true);
        }

        manager.RebuildGroupsAfterMove();
        return true;
    }

    Dictionary<Vector2Int, Piece> BuildBoardPiecesByLocalCell(List<Piece> picturePieces, PictureSO picture)
    {
        Dictionary<Vector2Int, Piece> result = new Dictionary<Vector2Int, Piece>();
        for (int i = 0; i < picturePieces.Count; i++)
        {
            Piece piece = picturePieces[i];
            Vector2Int localCell = piece.localCell;
            if (localCell.x < 0 || localCell.x >= picture.size.x || localCell.y < 0 || localCell.y >= picture.size.y)
            {
                return new Dictionary<Vector2Int, Piece>();
            }

            if (result.ContainsKey(localCell))
            {
                return new Dictionary<Vector2Int, Piece>();
            }

            result.Add(localCell, piece);
        }

        return result;
    }

    Piece FindClearCarrierPiece(IngameManager manager, HashSet<Piece> usedPieces, HashSet<Vector2Int> targetCells)
    {
        foreach (Vector2Int targetCell in targetCells)
        {
            Piece piece = manager.pieces[targetCell.x, targetCell.y];
            if (CanUseClearCarrier(manager, piece, usedPieces))
            {
                return piece;
            }
        }

        for (int x = 0; x < manager.width; x++)
        {
            for (int y = 0; y < manager.height; y++)
            {
                Piece piece = manager.pieces[x, y];
                if (CanUseClearCarrier(manager, piece, usedPieces))
                {
                    return piece;
                }
            }
        }

        return null;
    }

    bool CanUseClearCarrier(IngameManager manager, Piece piece, HashSet<Piece> usedPieces)
    {
        return piece != null
            && !piece.IsLock
            && !manager.IsLockedRow(piece.posInBoard)
            && piece.pictureSO != null
            && !usedPieces.Contains(piece);
    }

    List<Piece> GetBoardPiecesOfPicture(IngameManager manager, PictureSO picture)
    {
        List<Piece> result = new List<Piece>();
        for (int x = 0; x < manager.width; x++)
        {
            for (int y = 0; y < manager.height; y++)
            {
                Piece piece = manager.pieces[x, y];
                if (piece == null || piece.IsLock || manager.IsLockedRow(piece.posInBoard) || piece.pictureSO != picture)
                {
                    continue;
                }

                result.Add(piece);
            }
        }

        return result;
    }

    IngameManager.SpawnPieceData GetSpawnData(Piece piece)
    {
        return new IngameManager.SpawnPieceData
        {
            pictureSO = piece.pictureSO,
            localCell = piece.localCell,
            sprite = piece.pictureSO != null ? piece.pictureSO.GetSprite(piece.localCell) : null
        };
    }

    void ClearBoardCellIfContains(IngameManager manager, Piece piece)
    {
        Vector2Int cell = piece.posInBoard;
        if (manager.IsInBoard(cell) && manager.pieces[cell.x, cell.y] == piece)
        {
            manager.pieces[cell.x, cell.y] = null;
        }
    }

    void PlacePieceAtCell(IngameManager manager, Piece piece, Vector2Int cell, bool animate)
    {
        manager.pieces[cell.x, cell.y] = piece;
        piece.SetPosInBoard(cell.x, cell.y);
        piece.SetSnapPositionOnly(manager.GetBoardWorldPosition(cell.x, cell.y));

        if (animate)
        {
            piece.MoveToSnapPosition();
        }
        else
        {
            piece.SetSnapPosition(manager.GetBoardWorldPosition(cell.x, cell.y));
        }
    }

    public void BoosterSort(IngameManager manager)
    {
        if (manager.IsInputLocked || manager.pieces == null)
        {
            return;
        }

        List<Piece> movablePieces = GetMovableBoardPieces(manager);
        if (movablePieces.Count == 0)
        {
            return;
        }

        List<Vector2Int> movableCells = GetCellsOfPieces(movablePieces);
        List<IngameManager.SpawnPieceData> sortData = GetSpawnDataOfPieces(movablePieces);

        PictureSO completePicture = FindCompletePictureForSort(sortData);
        if (completePicture == null)
        {
            completePicture = TryPromoteCompletePictureFromDeck(manager, sortData);
        }

        if (completePicture == null)
        {
            Debug.Log("No complete picture available for BoosterSort.");
            return;
        }

        List<SortAssignment> assignments = BuildSortAssignments(sortData, movableCells);
        if (assignments.Count != movablePieces.Count)
        {
            Debug.Log("Cannot build BoosterSort assignments.");
            return;
        }

        PlayBoosterSortSequence(manager, movablePieces, assignments);
    }

    List<Piece> GetMovableBoardPieces(IngameManager manager)
    {
        List<Piece> result = new List<Piece>();
        for (int x = 0; x < manager.width; x++)
        {
            for (int y = 0; y < manager.height; y++)
            {
                Piece piece = manager.pieces[x, y];
                if (piece == null || piece.IsLock || manager.IsLockedRow(piece.posInBoard))
                {
                    continue;
                }

                result.Add(piece);
            }
        }

        return result;
    }

    List<Vector2Int> GetCellsOfPieces(List<Piece> boardPieces)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int i = 0; i < boardPieces.Count; i++)
        {
            cells.Add(boardPieces[i].posInBoard);
        }

        SortCells(cells);
        return cells;
    }

    List<IngameManager.SpawnPieceData> GetSpawnDataOfPieces(List<Piece> boardPieces)
    {
        List<IngameManager.SpawnPieceData> result = new List<IngameManager.SpawnPieceData>();
        for (int i = 0; i < boardPieces.Count; i++)
        {
            Piece piece = boardPieces[i];
            result.Add(new IngameManager.SpawnPieceData
            {
                pictureSO = piece.pictureSO,
                localCell = piece.localCell,
                sprite = piece.pictureSO != null ? piece.pictureSO.GetSprite(piece.localCell) : null
            });
        }

        return result;
    }

    PictureSO FindCompletePictureForSort(List<IngameManager.SpawnPieceData> sortData)
    {
        PictureSO bestPicture = null;
        int bestCount = -1;

        for (int i = 0; i < sortData.Count; i++)
        {
            PictureSO picture = sortData[i].pictureSO;
            if (picture == null || picture == bestPicture)
            {
                continue;
            }

            int requiredCount = GetPictureRequiredCount(picture);
            if (requiredCount <= 0 || requiredCount > sortData.Count)
            {
                continue;
            }

            int localCount = CountUniqueLocalCells(sortData, picture);
            if (localCount == requiredCount && localCount > bestCount)
            {
                bestPicture = picture;
                bestCount = localCount;
            }
        }

        return bestPicture;
    }

    PictureSO TryPromoteCompletePictureFromDeck(IngameManager manager, List<IngameManager.SpawnPieceData> sortData)
    {
        List<IngameManager.SpawnPieceData> deckData = GetAllDeckSpawnData(manager);
        List<PictureSO> candidatePictures = GetCandidatePictures(sortData, deckData);
        PictureSO selectedPicture = null;
        List<IngameManager.SpawnPieceData> selectedCompleteData = null;
        int bestCurrentCount = -1;

        for (int i = 0; i < candidatePictures.Count; i++)
        {
            PictureSO picture = candidatePictures[i];
            int requiredCount = GetPictureRequiredCount(picture);
            if (requiredCount <= 0 || requiredCount > sortData.Count)
            {
                continue;
            }

            List<IngameManager.SpawnPieceData> completeData = BuildCompletePictureData(picture, sortData, deckData);
            if (completeData.Count != requiredCount)
            {
                continue;
            }

            int currentCount = CountPiecesOfPictureInData(sortData, picture);
            if (currentCount > bestCurrentCount)
            {
                bestCurrentCount = currentCount;
                selectedPicture = picture;
                selectedCompleteData = completeData;
            }
        }

        if (selectedPicture == null || selectedCompleteData == null)
        {
            return null;
        }

        return PromotePictureDataIntoSortList(manager, sortData, selectedPicture, selectedCompleteData) ? selectedPicture : null;
    }

    List<IngameManager.SpawnPieceData> GetAllDeckSpawnData(IngameManager manager)
    {
        List<IngameManager.SpawnPieceData> result = new List<IngameManager.SpawnPieceData>();
        if (manager.columnDecks == null)
        {
            return result;
        }

        for (int x = 0; x < manager.columnDecks.Length; x++)
        {
            if (manager.columnDecks[x] == null)
            {
                continue;
            }

            IngameManager.SpawnPieceData[] columnData = manager.columnDecks[x].ToArray();
            for (int i = 0; i < columnData.Length; i++)
            {
                result.Add(columnData[i]);
            }
        }

        return result;
    }

    List<PictureSO> GetCandidatePictures(List<IngameManager.SpawnPieceData> sortData, List<IngameManager.SpawnPieceData> deckData)
    {
        List<PictureSO> pictures = new List<PictureSO>();
        AddCandidatePictures(pictures, sortData);
        AddCandidatePictures(pictures, deckData);
        return pictures;
    }

    void AddCandidatePictures(List<PictureSO> pictures, List<IngameManager.SpawnPieceData> dataList)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            PictureSO picture = dataList[i].pictureSO;
            if (picture != null && !pictures.Contains(picture))
            {
                pictures.Add(picture);
            }
        }
    }

    List<IngameManager.SpawnPieceData> BuildCompletePictureData(
        PictureSO picture,
        List<IngameManager.SpawnPieceData> sortData,
        List<IngameManager.SpawnPieceData> deckData)
    {
        List<IngameManager.SpawnPieceData> result = new List<IngameManager.SpawnPieceData>();
        for (int y = 0; y < picture.size.y; y++)
        {
            for (int x = 0; x < picture.size.x; x++)
            {
                Vector2Int localCell = new Vector2Int(x, y);
                if (TryFindSpawnData(sortData, picture, localCell, out IngameManager.SpawnPieceData data)
                    || TryFindSpawnData(deckData, picture, localCell, out data))
                {
                    result.Add(data);
                }
            }
        }

        return result;
    }

    bool PromotePictureDataIntoSortList(
        IngameManager manager,
        List<IngameManager.SpawnPieceData> sortData,
        PictureSO picture,
        List<IngameManager.SpawnPieceData> completeData)
    {
        List<IngameManager.SpawnPieceData> missingData = new List<IngameManager.SpawnPieceData>();
        for (int i = 0; i < completeData.Count; i++)
        {
            if (!ContainsSpawnData(sortData, completeData[i]))
            {
                missingData.Add(completeData[i]);
            }
        }

        if (missingData.Count == 0)
        {
            return true;
        }

        List<int> replaceIndices = new List<int>();
        for (int i = sortData.Count - 1; i >= 0; i--)
        {
            if (sortData[i].pictureSO != picture)
            {
                replaceIndices.Add(i);
                if (replaceIndices.Count == missingData.Count)
                {
                    break;
                }
            }
        }

        if (replaceIndices.Count < missingData.Count)
        {
            return false;
        }

        for (int i = 0; i < missingData.Count; i++)
        {
            if (!RemoveSpawnDataFromDeck(manager, missingData[i]))
            {
                return false;
            }

            IngameManager.SpawnPieceData replacedData = sortData[replaceIndices[i]];
            sortData[replaceIndices[i]] = missingData[i];
            AddSpawnDataToDeck(manager, replacedData, i);
        }

        return true;
    }

    bool RemoveSpawnDataFromDeck(IngameManager manager, IngameManager.SpawnPieceData targetData)
    {
        if (manager.columnDecks == null)
        {
            return false;
        }

        for (int x = 0; x < manager.columnDecks.Length; x++)
        {
            if (manager.columnDecks[x] == null)
            {
                continue;
            }

            Queue<IngameManager.SpawnPieceData> rebuiltQueue = new Queue<IngameManager.SpawnPieceData>();
            bool removed = false;
            while (manager.columnDecks[x].Count > 0)
            {
                IngameManager.SpawnPieceData data = manager.columnDecks[x].Dequeue();
                if (!removed && IsSameSpawnData(data, targetData))
                {
                    removed = true;
                    continue;
                }

                rebuiltQueue.Enqueue(data);
            }

            manager.columnDecks[x] = rebuiltQueue;
            if (removed)
            {
                return true;
            }
        }

        return false;
    }

    void AddSpawnDataToDeck(IngameManager manager, IngameManager.SpawnPieceData data, int index)
    {
        if (manager.columnDecks == null || manager.columnDecks.Length == 0)
        {
            return;
        }

        int column = index % manager.columnDecks.Length;
        if (manager.columnDecks[column] == null)
        {
            manager.columnDecks[column] = new Queue<IngameManager.SpawnPieceData>();
        }

        manager.columnDecks[column].Enqueue(data);
    }

    List<SortAssignment> BuildSortAssignments(List<IngameManager.SpawnPieceData> sortData, List<Vector2Int> movableCells)
    {
        List<SortAssignment> assignments = new List<SortAssignment>();
        ShuffleSpawnData(sortData);
        ShuffleCells(movableCells);

        for (int i = 0; i < sortData.Count && i < movableCells.Count; i++)
        {
            assignments.Add(new SortAssignment { data = sortData[i], cell = movableCells[i] });
        }

        BreakCompletedSortAssignments(assignments);
        return assignments;
    }

    void BreakCompletedSortAssignments(List<SortAssignment> assignments)
    {
        int guard = 0;
        while (TryFindCompletedSortPicture(assignments, out PictureSO completedPicture, out List<int> completedIndices)
               && guard < assignments.Count)
        {
            if (!SwapTwoSortDataOfPicture(assignments, completedPicture, completedIndices))
            {
                return;
            }

            guard++;
        }
    }

    bool TryFindCompletedSortPicture(List<SortAssignment> assignments, out PictureSO completedPicture, out List<int> completedIndices)
    {
        completedPicture = null;
        completedIndices = null;
        List<PictureSO> checkedPictures = new List<PictureSO>();

        for (int i = 0; i < assignments.Count; i++)
        {
            PictureSO picture = assignments[i].data.pictureSO;
            if (picture == null || checkedPictures.Contains(picture))
            {
                continue;
            }

            checkedPictures.Add(picture);
            int requiredCount = GetPictureRequiredCount(picture);
            if (requiredCount <= 1)
            {
                continue;
            }

            List<int> indices = new List<int>();
            HashSet<Vector2Int> localCells = new HashSet<Vector2Int>();
            bool isCompleted = true;
            bool hasOrigin = false;
            Vector2Int origin = Vector2Int.zero;

            for (int j = 0; j < assignments.Count; j++)
            {
                SortAssignment assignment = assignments[j];
                if (assignment.data.pictureSO != picture)
                {
                    continue;
                }

                Vector2Int localCell = assignment.data.localCell;
                if (localCell.x < 0 || localCell.x >= picture.size.x || localCell.y < 0 || localCell.y >= picture.size.y)
                {
                    isCompleted = false;
                    break;
                }

                Vector2Int currentOrigin = assignment.cell - localCell;
                if (!hasOrigin)
                {
                    origin = currentOrigin;
                    hasOrigin = true;
                }
                else if (origin != currentOrigin)
                {
                    isCompleted = false;
                    break;
                }

                indices.Add(j);
                localCells.Add(localCell);
            }

            if (isCompleted && indices.Count == requiredCount && localCells.Count == requiredCount)
            {
                completedPicture = picture;
                completedIndices = indices;
                return true;
            }
        }

        return false;
    }

    bool SwapTwoSortDataOfPicture(List<SortAssignment> assignments, PictureSO picture, List<int> indices)
    {
        for (int i = 0; i < indices.Count; i++)
        {
            for (int j = i + 1; j < indices.Count; j++)
            {
                int firstIndex = indices[i];
                int secondIndex = indices[j];
                SortAssignment first = assignments[firstIndex];
                SortAssignment second = assignments[secondIndex];

                if (first.data.pictureSO != picture
                    || second.data.pictureSO != picture
                    || first.data.localCell == second.data.localCell)
                {
                    continue;
                }

                IngameManager.SpawnPieceData tempData = first.data;
                first.data = second.data;
                second.data = tempData;
                assignments[firstIndex] = first;
                assignments[secondIndex] = second;
                return true;
            }
        }

        return false;
    }

    void PlayBoosterSortSequence(IngameManager manager, List<Piece> movablePieces, List<SortAssignment> assignments)
    {
        manager.IsInputLocked = true;
        manager.ClearGroupsForBooster();

        Vector3 center = GetBoardCenterWorldPosition(manager);
        for (int i = 0; i < movablePieces.Count; i++)
        {
            movablePieces[i].SetOrderOffset(40);
            movablePieces[i].FlipToBack(0f);
        }

        float flipToBackTime = manager.piecePrb != null ? manager.piecePrb.flipDuration : 0.18f;
        float centerMoveDuration = 0.5f;
        float dealMoveDuration = 0.5f;

        DOVirtual.DelayedCall(flipToBackTime, () =>
        {
            for (int i = 0; i < movablePieces.Count; i++)
            {
                movablePieces[i].transform.DOMove(center, centerMoveDuration).SetEase(Ease.InOutQuad);
            }
        });

        DOVirtual.DelayedCall(flipToBackTime + centerMoveDuration, () =>
        {
            for (int x = 0; x < manager.width; x++)
            {
                for (int y = 0; y < manager.height; y++)
                {
                    Piece piece = manager.pieces[x, y];
                    if (piece != null && movablePieces.Contains(piece))
                    {
                        manager.pieces[x, y] = null;
                    }
                }
            }

            for (int i = 0; i < movablePieces.Count && i < assignments.Count; i++)
            {
                Piece piece = movablePieces[i];
                SortAssignment assignment = assignments[i];
                Vector3 targetPosition = manager.GetBoardWorldPosition(assignment.cell.x, assignment.cell.y);

                piece.ApplySpawnData(assignment.data, false);
                piece.SetPosInBoard(assignment.cell.x, assignment.cell.y);
                piece.SetSnapPositionOnly(targetPosition);
                manager.pieces[assignment.cell.x, assignment.cell.y] = piece;
                piece.transform.DOMove(targetPosition, dealMoveDuration).SetEase(Ease.OutQuad);
            }
        });

        DOVirtual.DelayedCall(flipToBackTime + centerMoveDuration + dealMoveDuration, () =>
        {
            for (int i = 0; i < movablePieces.Count; i++)
            {
                movablePieces[i].FlipRevealCurrent(0f);
            }
        });

        DOVirtual.DelayedCall(flipToBackTime + centerMoveDuration + dealMoveDuration + flipToBackTime, () =>
        {
            for (int i = 0; i < movablePieces.Count; i++)
            {
                movablePieces[i].SetOrderOffset(0);
            }

            manager.RebuildGroupsForBooster();
            if (!manager.IsResolvingCompleteForBooster)
            {
                manager.CheckWin();
                manager.IsInputLocked = false;
            }
        });
    }

    Vector3 GetBoardCenterWorldPosition(IngameManager manager)
    {
        return manager.boardParent != null ? manager.boardParent.TransformPoint(Vector3.zero) : Vector3.zero;
    }

    bool TryFindSpawnData(
        List<IngameManager.SpawnPieceData> dataList,
        PictureSO picture,
        Vector2Int localCell,
        out IngameManager.SpawnPieceData data)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            if (dataList[i].pictureSO == picture && dataList[i].localCell == localCell)
            {
                data = dataList[i];
                return true;
            }
        }

        data = default(IngameManager.SpawnPieceData);
        return false;
    }

    bool ContainsSpawnData(List<IngameManager.SpawnPieceData> dataList, IngameManager.SpawnPieceData targetData)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            if (IsSameSpawnData(dataList[i], targetData))
            {
                return true;
            }
        }

        return false;
    }

    bool IsSameSpawnData(IngameManager.SpawnPieceData a, IngameManager.SpawnPieceData b)
    {
        return a.pictureSO == b.pictureSO && a.localCell == b.localCell;
    }

    int CountUniqueLocalCells(List<IngameManager.SpawnPieceData> dataList, PictureSO picture)
    {
        HashSet<Vector2Int> localCells = new HashSet<Vector2Int>();
        for (int i = 0; i < dataList.Count; i++)
        {
            if (dataList[i].pictureSO == picture)
            {
                localCells.Add(dataList[i].localCell);
            }
        }

        return localCells.Count;
    }

    int CountPiecesOfPictureInData(List<IngameManager.SpawnPieceData> dataList, PictureSO picture)
    {
        int count = 0;
        for (int i = 0; i < dataList.Count; i++)
        {
            if (dataList[i].pictureSO == picture)
            {
                count++;
            }
        }

        return count;
    }

    int GetPictureRequiredCount(PictureSO picture)
    {
        return picture != null ? picture.size.x * picture.size.y : 0;
    }

    void ShuffleSpawnData(List<IngameManager.SpawnPieceData> dataList)
    {
        for (int i = dataList.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            IngameManager.SpawnPieceData temp = dataList[i];
            dataList[i] = dataList[randomIndex];
            dataList[randomIndex] = temp;
        }
    }

    void SortCells(List<Vector2Int> cells)
    {
        cells.Sort((a, b) =>
        {
            int yCompare = a.y.CompareTo(b.y);
            return yCompare != 0 ? yCompare : a.x.CompareTo(b.x);
        });
    }

    void ShuffleCells(List<Vector2Int> cells)
    {
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Vector2Int temp = cells[i];
            cells[i] = cells[randomIndex];
            cells[randomIndex] = temp;
        }
    }
}
