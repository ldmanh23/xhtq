using System.IO;
using UnityEditor;
using UnityEngine;

public class PuzzleImageSlicerWindow : EditorWindow
{
    private Texture2D sourceTexture;
    private Piece piecePrefab;
    private int pictureId;
    private int columns = 2;
    private int rows = 2;
    private string outputRoot = "Assets/0_Game/PuzzlePieces";
    private string soOutputRoot = "Assets/0_Game/Level/_SO";
    private float pixelsPerUnit = 100f;

    [MenuItem("Tools/Puzzle/Image Slicer")]
    private static void Open()
    {
        GetWindow<PuzzleImageSlicerWindow>("Puzzle Slicer");
    }

    private void OnGUI()
    {
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source Image", sourceTexture, typeof(Texture2D), false);
        piecePrefab = (Piece)EditorGUILayout.ObjectField("Piece Prefab", piecePrefab, typeof(Piece), false);
        pictureId = EditorGUILayout.IntField("Picture Id", pictureId);
        columns = Mathf.Max(1, EditorGUILayout.IntField("Width Pieces", columns));
        rows = Mathf.Max(1, EditorGUILayout.IntField("Height Pieces", rows));
        outputRoot = EditorGUILayout.TextField("Output Root", outputRoot);
        soOutputRoot = EditorGUILayout.TextField("SO Output Root", soOutputRoot);
        pixelsPerUnit = Mathf.Max(1f, EditorGUILayout.FloatField("Pixels Per Unit", pixelsPerUnit));

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(sourceTexture == null))
        {
            if (GUILayout.Button("Crop To Fit And Slice"))
            {
                Slice();
            }
        }
    }

    private void Slice()
    {
        string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
        if (string.IsNullOrEmpty(sourcePath))
        {
            Debug.LogError("Source image must be inside Assets.");
            return;
        }

        TextureImporter sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        bool oldReadable = false;

        if (sourceImporter != null)
        {
            oldReadable = sourceImporter.isReadable;
            if (!sourceImporter.isReadable)
            {
                sourceImporter.isReadable = true;
                sourceImporter.SaveAndReimport();
            }
        }

        string pictureName = Path.GetFileNameWithoutExtension(sourcePath);
        string outputFolder = GetUniqueOutputFolder(outputRoot, pictureName);
        Directory.CreateDirectory(outputFolder);
        AssetDatabase.Refresh();

        Vector2 pieceLocalSize = GetPieceLocalSize();
        float targetAspect = (columns * pieceLocalSize.x) / (rows * pieceLocalSize.y);

        int cropWidth;
        int cropHeight;
        GetCropSize(sourceTexture.width, sourceTexture.height, targetAspect, columns, rows, out cropWidth, out cropHeight);

        int startX = (sourceTexture.width - cropWidth) / 2;
        int startY = (sourceTexture.height - cropHeight) / 2;
        int pieceWidth = cropWidth / columns;
        int pieceHeight = cropHeight / rows;
        float piecePixelsPerUnit = GetPixelsPerUnit(pieceWidth, pieceHeight, pieceLocalSize);

        PictureSO pictureSO = CreateInstance<PictureSO>();
        pictureSO.pictureId = pictureId;
        pictureSO.pictureName = pictureName;
        pictureSO.size = new Vector2Int(columns, rows);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Texture2D pieceTexture = new Texture2D(pieceWidth, pieceHeight, TextureFormat.RGBA32, false);
                Color[] pixels = sourceTexture.GetPixels(
                    startX + x * pieceWidth,
                    startY + y * pieceHeight,
                    pieceWidth,
                    pieceHeight
                );

                pieceTexture.SetPixels(pixels);
                pieceTexture.Apply();

                string piecePath = $"{outputFolder}/{pictureName}_x{x}_y{y}.png";
                File.WriteAllBytes(piecePath, pieceTexture.EncodeToPNG());
                DestroyImmediate(pieceTexture);

                AssetDatabase.ImportAsset(piecePath);
                SetupSpriteImporter(piecePath, piecePixelsPerUnit);

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(piecePath);
                pictureSO.pieces.Add(new PieceSpriteData
                {
                    localCell = new Vector2Int(x, y),
                    sprite = sprite
                });
            }
        }

        SetupSpriteImporter(sourcePath, pixelsPerUnit);
        pictureSO.fullSprite = AssetDatabase.LoadAssetAtPath<Sprite>(sourcePath);

        EnsureFolder(soOutputRoot);
        string soPath = AssetDatabase.GenerateUniqueAssetPath($"{soOutputRoot}/Picture_{pictureName}.asset");
        AssetDatabase.CreateAsset(pictureSO, soPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (sourceImporter != null && sourceImporter.isReadable != oldReadable)
        {
            sourceImporter.isReadable = oldReadable;
            sourceImporter.SaveAndReimport();
        }

        Selection.activeObject = pictureSO;
        Debug.Log($"Sliced {pictureName} to {columns}x{rows}: {outputFolder}");
    }

    private string GetUniqueOutputFolder(string root, string folderName)
    {
        string folder = $"{root}/{folderName}";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return folder;
        }

        int index = 1;
        while (true)
        {
            string nextFolder = $"{root}/{folderName}_{index:00}";
            if (!AssetDatabase.IsValidFolder(nextFolder))
            {
                return nextFolder;
            }

            index++;
        }
    }

    private void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        Directory.CreateDirectory(folderPath);
        AssetDatabase.Refresh();
    }

    private Vector2 GetPieceLocalSize()
    {
        SpriteRenderer targetSprite = null;
        if (piecePrefab != null)
        {
            targetSprite = piecePrefab.pieceSprite != null ? piecePrefab.pieceSprite : piecePrefab.spriteRenderer;
        }

        if (targetSprite == null || targetSprite.sprite == null)
        {
            return Vector2.one;
        }

        return targetSprite.sprite.bounds.size;
    }

    private float GetPixelsPerUnit(int pieceWidth, int pieceHeight, Vector2 pieceLocalSize)
    {
        if (pieceLocalSize.x <= 0f || pieceLocalSize.y <= 0f)
        {
            return pixelsPerUnit;
        }

        float ppuX = pieceWidth / pieceLocalSize.x;
        float ppuY = pieceHeight / pieceLocalSize.y;
        return Mathf.Max(1f, Mathf.Max(ppuX, ppuY));
    }

    private void GetCropSize(int sourceWidth, int sourceHeight, float targetAspect, int targetColumns, int targetRows, out int cropWidth, out int cropHeight)
    {
        float sourceAspect = (float)sourceWidth / sourceHeight;

        if (sourceAspect > targetAspect)
        {
            cropHeight = sourceHeight;
            cropWidth = Mathf.RoundToInt(cropHeight * targetAspect);
        }
        else
        {
            cropWidth = sourceWidth;
            cropHeight = Mathf.RoundToInt(cropWidth / targetAspect);
        }

        cropWidth = Mathf.Max(targetColumns, cropWidth / targetColumns * targetColumns);
        cropHeight = Mathf.Max(targetRows, cropHeight / targetRows * targetRows);
    }

    private void SetupSpriteImporter(string assetPath, float spritePixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = spritePixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }
}
