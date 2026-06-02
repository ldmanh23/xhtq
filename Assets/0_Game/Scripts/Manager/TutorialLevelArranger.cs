using System.Collections.Generic;
using UnityEngine;

public static class TutorialLevelArranger
{
    public static void Apply(LevelSO level, List<IngameManager.SpawnPieceData> spawnPieces, int width, int height)
    {
        if (level == null || spawnPieces == null || level.tutorialGroupCells == null || level.tutorialGroupCells.Count != 3)
        {
            Debug.LogWarning("Tutorial level is missing tutorial cells.");
            return;
        }

        PictureSO tutorialPicture = FindTutorialPicture(spawnPieces);
        if (tutorialPicture == null)
        {
            Debug.LogWarning("Tutorial level needs at least one 2x2 picture.");
            return;
        }

        List<IngameManager.SpawnPieceData> tutorialPieces = TakePicturePieces(spawnPieces, tutorialPicture);
        if (tutorialPieces.Count < 4)
        {
            Debug.LogWarning("Tutorial 2x2 picture does not have enough pieces.");
            spawnPieces.AddRange(tutorialPieces);
            return;
        }

        List<Vector2Int> groupCells = new List<Vector2Int>(level.tutorialGroupCells);
        Vector2Int pieceCell = level.tutorialPieceCell;
        Vector2Int minCell = GetMinCell(groupCells);
        List<IngameManager.SpawnPieceData> orderedTutorialPieces = new List<IngameManager.SpawnPieceData>();

        for (int i = 0; i < groupCells.Count; i++)
        {
            Vector2Int localCell = groupCells[i] - minCell;
            IngameManager.SpawnPieceData pieceData = FindPieceByLocalCell(tutorialPieces, localCell);
            if (pieceData.sprite == null)
            {
                Debug.LogWarning("Tutorial group cells must fit a 2x2 shape.");
                spawnPieces.AddRange(tutorialPieces);
                return;
            }

            orderedTutorialPieces.Add(pieceData);
        }

        IngameManager.SpawnPieceData remainingPiece = FindRemainingTutorialPiece(tutorialPieces, orderedTutorialPieces);
        if (remainingPiece.sprite == null)
        {
            Debug.LogWarning("Tutorial missing remaining 2x2 piece.");
            spawnPieces.AddRange(tutorialPieces);
            return;
        }

        orderedTutorialPieces.Add(remainingPiece);
        PlaceTutorialPieces(spawnPieces, width, height, groupCells, pieceCell, orderedTutorialPieces);
    }

    static PictureSO FindTutorialPicture(List<IngameManager.SpawnPieceData> spawnPieces)
    {
        for (int i = 0; i < spawnPieces.Count; i++)
        {
            PictureSO picture = spawnPieces[i].pictureSO;
            if (picture != null && picture.size == new Vector2Int(2, 2) && CountPiecesOfPicture(spawnPieces, picture) >= 4)
            {
                return picture;
            }
        }

        return null;
    }

    static int CountPiecesOfPicture(List<IngameManager.SpawnPieceData> spawnPieces, PictureSO picture)
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

    static List<IngameManager.SpawnPieceData> TakePicturePieces(List<IngameManager.SpawnPieceData> spawnPieces, PictureSO picture)
    {
        List<IngameManager.SpawnPieceData> picturePieces = new List<IngameManager.SpawnPieceData>();
        for (int i = spawnPieces.Count - 1; i >= 0; i--)
        {
            if (spawnPieces[i].pictureSO == picture)
            {
                picturePieces.Add(spawnPieces[i]);
                spawnPieces.RemoveAt(i);
            }
        }

        return picturePieces;
    }

    static Vector2Int GetMinCell(List<Vector2Int> cells)
    {
        Vector2Int minCell = cells[0];
        for (int i = 1; i < cells.Count; i++)
        {
            minCell = new Vector2Int(Mathf.Min(minCell.x, cells[i].x), Mathf.Min(minCell.y, cells[i].y));
        }

        return minCell;
    }

    static IngameManager.SpawnPieceData FindPieceByLocalCell(List<IngameManager.SpawnPieceData> piecesList, Vector2Int localCell)
    {
        for (int i = 0; i < piecesList.Count; i++)
        {
            if (piecesList[i].localCell == localCell)
            {
                return piecesList[i];
            }
        }

        return new IngameManager.SpawnPieceData();
    }

    static IngameManager.SpawnPieceData FindRemainingTutorialPiece(
        List<IngameManager.SpawnPieceData> tutorialPieces,
        List<IngameManager.SpawnPieceData> usedPieces)
    {
        for (int i = 0; i < tutorialPieces.Count; i++)
        {
            bool used = false;
            for (int j = 0; j < usedPieces.Count; j++)
            {
                if (tutorialPieces[i].localCell == usedPieces[j].localCell)
                {
                    used = true;
                    break;
                }
            }

            if (!used)
            {
                return tutorialPieces[i];
            }
        }

        return new IngameManager.SpawnPieceData();
    }

    static void PlaceTutorialPieces(
        List<IngameManager.SpawnPieceData> spawnPieces,
        int width,
        int height,
        List<Vector2Int> groupCells,
        Vector2Int pieceCell,
        List<IngameManager.SpawnPieceData> tutorialPieces)
    {
        int boardCapacity = width * height;
        IngameManager.SpawnPieceData[] arrangedBoardPieces = new IngameManager.SpawnPieceData[boardCapacity];
        bool[] occupied = new bool[boardCapacity];

        for (int i = 0; i < groupCells.Count; i++)
        {
            int index = GetBoardSpawnIndex(groupCells[i], width, height);
            if (index >= 0 && index < boardCapacity)
            {
                arrangedBoardPieces[index] = tutorialPieces[i];
                occupied[index] = true;
            }
        }

        int pieceIndex = GetBoardSpawnIndex(pieceCell, width, height);
        if (pieceIndex >= 0 && pieceIndex < boardCapacity)
        {
            arrangedBoardPieces[pieceIndex] = tutorialPieces[tutorialPieces.Count - 1];
            occupied[pieceIndex] = true;
        }

        List<IngameManager.SpawnPieceData> sourcePieces = new List<IngameManager.SpawnPieceData>(spawnPieces);
        spawnPieces.Clear();

        int sourceIndex = 0;
        for (int i = 0; i < boardCapacity; i++)
        {
            if (occupied[i])
            {
                spawnPieces.Add(arrangedBoardPieces[i]);
                continue;
            }

            if (sourceIndex < sourcePieces.Count)
            {
                spawnPieces.Add(sourcePieces[sourceIndex]);
                sourceIndex++;
            }
        }

        for (; sourceIndex < sourcePieces.Count; sourceIndex++)
        {
            spawnPieces.Add(sourcePieces[sourceIndex]);
        }
    }

    static int GetBoardSpawnIndex(Vector2Int cell, int width, int height)
    {
        if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height)
        {
            return -1;
        }

        return cell.y * width + cell.x;
    }
}
