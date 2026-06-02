using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public static class RuntimePictureBuilder
{
    public static List<IngameManager.SpawnPieceData> BuildSpawnPieces(LevelSO level, Piece piecePrefab)
    {
        List<IngameManager.SpawnPieceData> spawnPieces = new List<IngameManager.SpawnPieceData>();

        if (level == null)
        {
            return spawnPieces;
        }

        if (string.IsNullOrEmpty(level.resourcesImageFolder))
        {
            Debug.LogError("Runtime image folder is empty. Put images under Assets/Resources and set the folder path.");
            return spawnPieces;
        }

        List<Texture2D> textures = new List<Texture2D>(Resources.LoadAll<Texture2D>(level.resourcesImageFolder));
        if (textures.Count == 0)
        {
            Debug.LogError($"No images found in Resources folder: {level.resourcesImageFolder}");
            return spawnPieces;
        }

        Shuffle(textures);

        int imageCount = Mathf.Clamp(level.imageCount, 0, textures.Count);
        int pictureId = 1;
        int usedImages = 0;

        for (int i = 0; i < textures.Count && usedImages < imageCount; i++)
        {
            Texture2D texture = textures[i];
            if (texture == null)
            {
                continue;
            }

            if (!TryParsePictureSize(texture.name, out Vector2Int pictureSize))
            {
                Debug.LogWarning($"Skip image '{texture.name}'. File name must contain size like 2x3 or 2x1.");
                continue;
            }

            PictureSO picture = CreateRuntimePicture(texture, pictureSize, pictureId, piecePrefab);
            pictureId++;
            usedImages++;

            for (int j = 0; j < picture.pieces.Count; j++)
            {
                PieceSpriteData pieceData = picture.pieces[j];
                if (pieceData == null || pieceData.sprite == null)
                {
                    continue;
                }

                spawnPieces.Add(new IngameManager.SpawnPieceData
                {
                    pictureSO = picture,
                    localCell = pieceData.localCell,
                    sprite = pieceData.sprite
                });
            }
        }

        return spawnPieces;
    }

    static bool TryParsePictureSize(string imageName, out Vector2Int pictureSize)
    {
        pictureSize = Vector2Int.zero;
        Match match = Regex.Match(imageName, @"(\d+)\s*x\s*(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        int widthValue = int.Parse(match.Groups[1].Value);
        int heightValue = int.Parse(match.Groups[2].Value);
        if (widthValue <= 0 || heightValue <= 0)
        {
            return false;
        }

        pictureSize = new Vector2Int(widthValue, heightValue);
        return true;
    }

    static PictureSO CreateRuntimePicture(Texture2D texture, Vector2Int pictureSize, int pictureId, Piece piecePrefab)
    {
        PictureSO picture = ScriptableObject.CreateInstance<PictureSO>();
        picture.pictureId = pictureId;
        picture.pictureName = texture.name;
        picture.size = pictureSize;

        Rect sourceRect = GetRuntimePictureSourceRect(texture, pictureSize, piecePrefab);
        int tileWidth = Mathf.FloorToInt(sourceRect.width / pictureSize.x);
        int tileHeight = Mathf.FloorToInt(sourceRect.height / pictureSize.y);
        float pixelsPerUnit = GetRuntimeTilePixelsPerUnit(tileWidth, piecePrefab);

        picture.fullSprite = Sprite.Create(texture, sourceRect, Vector2.one * 0.5f, pixelsPerUnit);

        for (int y = 0; y < pictureSize.y; y++)
        {
            for (int x = 0; x < pictureSize.x; x++)
            {
                Rect tileRect = new Rect(
                    sourceRect.x + x * tileWidth,
                    sourceRect.y + y * tileHeight,
                    tileWidth,
                    tileHeight
                );

                Sprite tileSprite = Sprite.Create(texture, tileRect, Vector2.one * 0.5f, pixelsPerUnit);
                tileSprite.name = $"{texture.name}_{x}_{y}";

                picture.pieces.Add(new PieceSpriteData
                {
                    localCell = new Vector2Int(x, y),
                    sprite = tileSprite
                });
            }
        }

        return picture;
    }

    static Rect GetRuntimePictureSourceRect(Texture2D texture, Vector2Int pictureSize, Piece piecePrefab)
    {
        Vector2 targetPieceSize = GetPiecePictureLocalSize(piecePrefab);
        if (targetPieceSize.x <= 0f || targetPieceSize.y <= 0f)
        {
            targetPieceSize = Vector2.one;
        }

        float targetPieceAspect = targetPieceSize.x / targetPieceSize.y;
        float targetPictureAspect = pictureSize.x * targetPieceAspect / pictureSize.y;
        float sourceAspect = (float)texture.width / texture.height;

        int cropWidth = texture.width;
        int cropHeight = texture.height;

        if (sourceAspect > targetPictureAspect)
        {
            cropWidth = Mathf.RoundToInt(texture.height * targetPictureAspect);
        }
        else
        {
            cropHeight = Mathf.RoundToInt(texture.width / targetPictureAspect);
        }

        cropWidth = Mathf.Max(pictureSize.x, cropWidth - cropWidth % pictureSize.x);
        cropHeight = Mathf.Max(pictureSize.y, cropHeight - cropHeight % pictureSize.y);

        int cropX = Mathf.Max(0, (texture.width - cropWidth) / 2);
        int cropY = Mathf.Max(0, (texture.height - cropHeight) / 2);

        return new Rect(cropX, cropY, cropWidth, cropHeight);
    }

    static float GetRuntimeTilePixelsPerUnit(int tileWidth, Piece piecePrefab)
    {
        Vector2 targetPieceSize = GetPiecePictureLocalSize(piecePrefab);
        if (targetPieceSize.x <= 0f)
        {
            return 100f;
        }

        return tileWidth / targetPieceSize.x;
    }

    static Vector2 GetPiecePictureLocalSize(Piece piecePrefab)
    {
        SpriteRenderer target = piecePrefab != null && piecePrefab.pieceSprite != null
            ? piecePrefab.pieceSprite
            : piecePrefab != null ? piecePrefab.spriteRenderer : null;

        if (target == null || target.sprite == null)
        {
            return Vector2.one;
        }

        return target.sprite.bounds.size;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
