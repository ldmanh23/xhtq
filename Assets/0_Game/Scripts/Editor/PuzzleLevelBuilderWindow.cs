using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class PuzzleLevelBuilderWindow : EditorWindow
{
    [System.Serializable]
    class ImageConfig
    {
        public Texture2D texture;
        public int columns = 2;
        public int rows = 2;
        public bool include = true;
    }

    DefaultAsset imageFolder;
    Piece piecePrefab;
    string levelName = "Level_01";
    int levelIndex = 1;
    Vector2Int boardSize = new Vector2Int(6, 6);
    bool shufflePieces = true;
    bool lockTopRows;
    int startPictureId = 1;
    string outputRoot = "Assets/0_Game/PuzzlePieces";
    string pictureSOOutputRoot = "Assets/0_Game/Level/_SO/Pictures";
    string levelSOOutputRoot = "Assets/0_Game/Level/_SO";
    float fallbackPixelsPerUnit = 100f;
    float previewSize = 140f;

    readonly List<ImageConfig> images = new List<ImageConfig>();
    Vector2 scroll;

    [MenuItem("Tools/Puzzle/Level Builder")]
    static void Open()
    {
        GetWindow<PuzzleLevelBuilderWindow>("Puzzle Level Builder");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
        levelName = EditorGUILayout.TextField("Level Name", levelName);
        levelIndex = EditorGUILayout.IntField("Level Index", levelIndex);
        boardSize = EditorGUILayout.Vector2IntField("Board Size", boardSize);
        shufflePieces = EditorGUILayout.Toggle("Shuffle Pieces", shufflePieces);
        lockTopRows = EditorGUILayout.Toggle("Lock Top Rows", lockTopRows);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        imageFolder = (DefaultAsset)EditorGUILayout.ObjectField("Image Folder", imageFolder, typeof(DefaultAsset), false);
        piecePrefab = (Piece)EditorGUILayout.ObjectField("Piece Prefab", piecePrefab, typeof(Piece), false);
        startPictureId = EditorGUILayout.IntField("Start Picture Id", startPictureId);
        outputRoot = EditorGUILayout.TextField("Piece Output Root", outputRoot);
        pictureSOOutputRoot = EditorGUILayout.TextField("Picture SO Folder", pictureSOOutputRoot);
        levelSOOutputRoot = EditorGUILayout.TextField("Level SO Folder", levelSOOutputRoot);
        fallbackPixelsPerUnit = Mathf.Max(1f, EditorGUILayout.FloatField("Fallback PPU", fallbackPixelsPerUnit));
        previewSize = Mathf.Clamp(EditorGUILayout.FloatField("Preview Size", previewSize), 48f, 180f);

        using (new EditorGUI.DisabledScope(imageFolder == null))
        {
            if (GUILayout.Button("Scan Image Folder"))
            {
                ScanFolder();
            }
        }

        EditorGUILayout.Space();
        DrawImageConfigs();

        EditorGUILayout.Space();
        DrawSummary();

        using (new EditorGUI.DisabledScope(images.Count == 0 || piecePrefab == null))
        {
            if (GUILayout.Button("Generate Level"))
            {
                GenerateLevel();
            }
        }
    }

    void DrawImageConfigs()
    {
        EditorGUILayout.LabelField("Images", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(420f), GUILayout.ExpandHeight(true));

        for (int i = 0; i < images.Count; i++)
        {
            ImageConfig config = images[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.Width(previewSize), GUILayout.Height(previewSize));
            DrawTexturePreview(previewRect, config.texture);

            EditorGUILayout.BeginVertical();
            config.include = EditorGUILayout.Toggle("Include", config.include);
            config.texture = (Texture2D)EditorGUILayout.ObjectField("Image", config.texture, typeof(Texture2D), false);

            if (config.texture != null)
            {
                EditorGUILayout.LabelField("Name", config.texture.name);
                EditorGUILayout.LabelField("Source Size", config.texture.width + " x " + config.texture.height);
            }

            EditorGUILayout.BeginHorizontal();
            config.columns = Mathf.Max(1, EditorGUILayout.IntField("Width", config.columns));
            config.rows = Mathf.Max(1, EditorGUILayout.IntField("Height", config.rows));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Pieces", (config.columns * config.rows).ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawTexturePreview(Rect rect, Texture2D texture)
    {
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
        if (texture == null)
        {
            return;
        }

        Rect drawRect = GetAspectFitRect(rect, texture.width, texture.height);
        GUI.DrawTexture(drawRect, texture, ScaleMode.ScaleToFit);
    }

    Rect GetAspectFitRect(Rect rect, float textureWidth, float textureHeight)
    {
        if (textureWidth <= 0f || textureHeight <= 0f)
        {
            return rect;
        }

        float textureAspect = textureWidth / textureHeight;
        float rectAspect = rect.width / rect.height;

        if (textureAspect > rectAspect)
        {
            float height = rect.width / textureAspect;
            return new Rect(rect.x, rect.y + (rect.height - height) * 0.5f, rect.width, height);
        }

        float width = rect.height * textureAspect;
        return new Rect(rect.x + (rect.width - width) * 0.5f, rect.y, width, rect.height);
    }

    void DrawSummary()
    {
        int pictureCount = 0;
        int pieceCount = 0;
        for (int i = 0; i < images.Count; i++)
        {
            if (images[i].include && images[i].texture != null)
            {
                pictureCount++;
                pieceCount += images[i].columns * images[i].rows;
            }
        }

        int boardCapacity = boardSize.x * boardSize.y;
        EditorGUILayout.HelpBox(
            $"Pictures: {pictureCount}\nPieces: {pieceCount}\nBoard capacity: {boardCapacity}\nDeck pieces: {Mathf.Max(0, pieceCount - boardCapacity)}",
            MessageType.Info
        );

        if (pieceCount < boardCapacity)
        {
            EditorGUILayout.HelpBox("Not enough pieces to fill the board.", MessageType.Warning);
        }
    }

    void ScanFolder()
    {
        string folderPath = AssetDatabase.GetAssetPath(imageFolder);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError("Please select a valid folder inside Assets.");
            return;
        }

        images.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                continue;
            }

            images.Add(new ImageConfig
            {
                texture = texture,
                columns = 2,
                rows = 2,
                include = true
            });
        }

        images.Sort((a, b) => string.Compare(a.texture.name, b.texture.name, System.StringComparison.Ordinal));
    }

    void GenerateLevel()
    {
        EnsureFolder(outputRoot);
        EnsureFolder(pictureSOOutputRoot);
        EnsureFolder(levelSOOutputRoot);

        List<PictureSO> pictureSOs = new List<PictureSO>();
        int pictureId = startPictureId;

        for (int i = 0; i < images.Count; i++)
        {
            ImageConfig config = images[i];
            if (!config.include || config.texture == null)
            {
                continue;
            }

            PictureSO pictureSO = GeneratePicture(config, pictureId);
            if (pictureSO != null)
            {
                pictureSOs.Add(pictureSO);
                pictureId++;
            }
        }

        LevelSO levelSO = CreateInstance<LevelSO>();
        levelSO.levelIndex = levelIndex;
        levelSO.boardSize = boardSize;
        levelSO.initialPieceCount = 0;
        levelSO.shufflePieces = shufflePieces;
        levelSO.lockTopRows = lockTopRows;
        levelSO.pictures.AddRange(pictureSOs);

        string levelPath = AssetDatabase.GenerateUniqueAssetPath($"{levelSOOutputRoot}/{levelName}.asset");
        AssetDatabase.CreateAsset(levelSO, levelPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = levelSO;
        Debug.Log($"Generated {levelName}: {pictureSOs.Count} pictures");
    }

    PictureSO GeneratePicture(ImageConfig config, int pictureId)
    {
        string sourcePath = AssetDatabase.GetAssetPath(config.texture);
        if (string.IsNullOrEmpty(sourcePath))
        {
            return null;
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
        float targetAspect = (config.columns * pieceLocalSize.x) / (config.rows * pieceLocalSize.y);

        int cropWidth;
        int cropHeight;
        GetCropSize(config.texture.width, config.texture.height, targetAspect, config.columns, config.rows, out cropWidth, out cropHeight);

        int startX = (config.texture.width - cropWidth) / 2;
        int startY = (config.texture.height - cropHeight) / 2;
        int pieceWidth = cropWidth / config.columns;
        int pieceHeight = cropHeight / config.rows;
        float piecePixelsPerUnit = GetPixelsPerUnit(pieceWidth, pieceHeight, pieceLocalSize);

        PictureSO pictureSO = CreateInstance<PictureSO>();
        pictureSO.pictureId = pictureId;
        pictureSO.pictureName = pictureName;
        pictureSO.size = new Vector2Int(config.columns, config.rows);

        for (int y = 0; y < config.rows; y++)
        {
            for (int x = 0; x < config.columns; x++)
            {
                Texture2D pieceTexture = new Texture2D(pieceWidth, pieceHeight, TextureFormat.RGBA32, false);
                Color[] pixels = config.texture.GetPixels(
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

        SetupSpriteImporter(sourcePath, fallbackPixelsPerUnit);
        pictureSO.fullSprite = AssetDatabase.LoadAssetAtPath<Sprite>(sourcePath);

        string soPath = AssetDatabase.GenerateUniqueAssetPath($"{pictureSOOutputRoot}/Picture_{pictureName}.asset");
        AssetDatabase.CreateAsset(pictureSO, soPath);

        if (sourceImporter != null && sourceImporter.isReadable != oldReadable)
        {
            sourceImporter.isReadable = oldReadable;
            sourceImporter.SaveAndReimport();
        }

        return pictureSO;
    }

    string GetUniqueOutputFolder(string root, string folderName)
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

    void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        Directory.CreateDirectory(folderPath);
        AssetDatabase.Refresh();
    }

    Vector2 GetPieceLocalSize()
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

    float GetPixelsPerUnit(int pieceWidth, int pieceHeight, Vector2 pieceLocalSize)
    {
        if (pieceLocalSize.x <= 0f || pieceLocalSize.y <= 0f)
        {
            return fallbackPixelsPerUnit;
        }

        float ppuX = pieceWidth / pieceLocalSize.x;
        float ppuY = pieceHeight / pieceLocalSize.y;
        return Mathf.Max(1f, Mathf.Max(ppuX, ppuY));
    }

    void GetCropSize(int sourceWidth, int sourceHeight, float targetAspect, int targetColumns, int targetRows, out int cropWidth, out int cropHeight)
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

    void SetupSpriteImporter(string assetPath, float spritePixelsPerUnit)
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
