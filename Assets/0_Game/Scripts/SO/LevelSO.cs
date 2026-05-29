using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level_", menuName = "Puzzle/Level")]
public class LevelSO : ScriptableObject
{
    public int levelIndex;
    public Vector2Int boardSize = new Vector2Int(6, 6);
    public int initialPieceCount;
    public bool shufflePieces = true;
    public List<PictureSO> pictures = new List<PictureSO>();
}
