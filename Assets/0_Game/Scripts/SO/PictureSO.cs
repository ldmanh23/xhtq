using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Picture_", menuName = "Puzzle/Picture")]
public class PictureSO : ScriptableObject
{
    public int pictureId;
    public string pictureName;
    public Vector2Int size = Vector2Int.one;
    public Sprite fullSprite;
    public List<PieceSpriteData> pieces = new List<PieceSpriteData>();

    public Sprite GetSprite(Vector2Int localCell)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null && pieces[i].localCell == localCell)
            {
                return pieces[i].sprite;
            }
        }

        return null;
    }
}

[System.Serializable]
public class PieceSpriteData
{
    public Vector2Int localCell;
    public Sprite sprite;
}
