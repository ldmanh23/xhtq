using System.Collections.Generic;
using UnityEngine;

public class PuzzleBoardSpawner : Singleton<PuzzleBoardSpawner>
{
    public void BuildBoard(IngameManager manager)
    {
        ResolveCurrentLevel(manager);

        if (manager.curLevel == null || manager.piecePrb == null)
        {
            Debug.LogError("Missing level or piece prefab.");
            return;
        }

        manager.pieces = new Piece[manager.curLevel.boardSize.x, manager.curLevel.boardSize.y];
        manager.width = manager.curLevel.boardSize.x;
        manager.height = manager.curLevel.boardSize.y;

        List<IngameManager.SpawnPieceData> spawnPieces = RuntimePictureBuilder.BuildSpawnPieces(manager.curLevel, manager.piecePrb);
        if (spawnPieces.Count == 0)
        {
            Debug.LogError("Level has no valid picture pieces.");
            return;
        }

        if (manager.curLevel.shufflePieces)
        {
            Shuffle(spawnPieces);
        }

        SortSpawnPiecesByPictureSize(spawnPieces);

        if (manager.curLevel.isTutorialLevel)
        {
            TutorialLevelArranger.Apply(manager.curLevel, spawnPieces, manager.width, manager.height);
        }

        Vector2 pieceSize = manager.GetPieceSize(manager.piecePrb);
        Vector2 boardOffset = new Vector2(
            (manager.width - 1) * pieceSize.x * 0.5f,
            (manager.height - 1) * pieceSize.y * 0.5f
        );

        int spawnCount = GetSpawnCount(manager, spawnPieces.Count);
        if (!manager.curLevel.isTutorialLevel)
        {
            EnsureOneCompletePictureInBoard(spawnPieces, spawnCount);
        }

        BuildColumnDecks(manager, spawnPieces, spawnCount);
        int spawnIndex = 0;

        for (int y = 0; y < manager.height; y++)
        {
            for (int x = 0; x < manager.width; x++)
            {
                if (spawnIndex >= spawnCount)
                {
                    manager.FinishInitialBoard(spawnCount);
                    return;
                }

                Piece piece = SimplePool.Spawn<Piece>(manager.piecePrb);
                if (manager.boardParent == null)
                {
                    manager.boardParent = piece.transform.parent;
                }

                piece.transform.localPosition = new Vector3(
                    x * pieceSize.x - boardOffset.x,
                    y * pieceSize.y - boardOffset.y,
                    0
                );

                IngameManager.SpawnPieceData data = spawnPieces[spawnIndex];
                float delay = spawnIndex * manager.flipDelayEachPiece;
                piece.Setup(data.pictureSO, data.localCell, data.sprite, x, y, manager.playSpawnFlip, delay);
                ApplyInitialLock(manager, piece, x, y, manager.playSpawnFlip ? delay + piece.flipDuration : 0f);
                piece.SetSnapPosition(piece.transform.position);

                manager.pieces[x, y] = piece;
                spawnIndex++;
            }
        }

        manager.FinishInitialBoard(spawnCount);
    }

    void ResolveCurrentLevel(IngameManager manager)
    {
        if (GameManager.ins == null || GameManager.ins.levels == null || GameManager.ins.levels.Count == 0)
        {
            return;
        }

        int levelIndex = GetCurrentLevelIndex();
        manager.curLevel = GameManager.ins.levels[levelIndex % GameManager.ins.levels.Count];
    }

    int GetCurrentLevelIndex()
    {
        if (DataManager.ins == null || DataManager.ins.dt == null)
        {
            return 0;
        }

        return Mathf.Max(0, DataManager.ins.dt.level);
    }

    int GetSpawnCount(IngameManager manager, int availablePieceCount)
    {
        int boardCapacity = manager.width * manager.height;
        return Mathf.Min(boardCapacity, availablePieceCount);
    }

    void ApplyInitialLock(IngameManager manager, Piece piece, int x, int y, float showDelay)
    {
        if (piece == null || manager.curLevel == null || !manager.curLevel.hasLockPieces || manager.curLevel.lockPieces == null)
        {
            return;
        }

        for (int i = 0; i < manager.curLevel.lockPieces.Count; i++)
        {
            LevelLockPieceData lockData = manager.curLevel.lockPieces[i];
            if (lockData != null && lockData.cell == new Vector2Int(x, y))
            {
                piece.SetLock(lockData.unlockImageCount, showDelay);
                return;
            }
        }
    }

    void BuildColumnDecks(IngameManager manager, List<IngameManager.SpawnPieceData> spawnPieces, int startIndex)
    {
        manager.columnDecks = new Queue<IngameManager.SpawnPieceData>[manager.width];
        for (int x = 0; x < manager.width; x++)
        {
            manager.columnDecks[x] = new Queue<IngameManager.SpawnPieceData>();
        }

        for (int i = startIndex; i < spawnPieces.Count; i++)
        {
            int column = (i - startIndex) % manager.width;
            manager.columnDecks[column].Enqueue(spawnPieces[i]);
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

    void SortSpawnPiecesByPictureSize(List<IngameManager.SpawnPieceData> spawnPieces)
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

    void EnsureOneCompletePictureInBoard(List<IngameManager.SpawnPieceData> spawnPieces, int boardPieceCount)
    {
        PictureSO selectedPicture = FindCompletePictureForBoard(spawnPieces, boardPieceCount);
        if (selectedPicture == null)
        {
            return;
        }

        List<IngameManager.SpawnPieceData> selectedPieces = new List<IngameManager.SpawnPieceData>();
        List<IngameManager.SpawnPieceData> otherPieces = new List<IngameManager.SpawnPieceData>();

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

    PictureSO FindCompletePictureForBoard(List<IngameManager.SpawnPieceData> spawnPieces, int boardPieceCount)
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

    int CountPiecesOfPicture(List<IngameManager.SpawnPieceData> spawnPieces, PictureSO picture)
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
}
