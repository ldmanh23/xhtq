using UnityEngine;

[CreateAssetMenu(fileName = "Level_", menuName = "Puzzle/Level")]
public class LevelSO : ScriptableObject
{
    public Vector2Int boardSize = new Vector2Int(6, 6);
    public int initialPieceCount;
    public bool shufflePieces = true;
    public bool lockTopRows;

    [Header("Runtime Image Folder")]
    public string resourcesImageFolder;
    public int imageCount = 14;
}
