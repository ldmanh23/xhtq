using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Level_", menuName = "Puzzle/Level")]
public class LevelSO : ScriptableObject
{
    public Vector2Int boardSize = new Vector2Int(6, 6);
    public int initialPieceCount;
    public int timer = 180;
    public bool shufflePieces = true;
    public bool lockTopRows;

    [Header("Runtime Image Folder")]
    public string resourcesImageFolder;
    public int imageCount = 14;

    [Header("Lock Pieces")]
    public bool hasLockPieces;
    public List<LevelLockPieceData> lockPieces = new List<LevelLockPieceData>();

    [Header("Tutorial")]
    public bool isTutorialLevel;
    public List<Vector2Int> tutorialGroupCells = new List<Vector2Int>();
    public Vector2Int tutorialPieceCell = new Vector2Int(-1, -1);
}

[System.Serializable]
public class LevelLockPieceData
{
    public Vector2Int cell;
    public int unlockImageCount = 1;
}
