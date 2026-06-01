using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class PuzzleLevelBuilderWindow : EditorWindow
{
    class ImageInfo
    {
        public Texture2D texture;
        public Vector2Int size = Vector2Int.one;
        public bool validName;
    }

    DefaultAsset imageFolder;
    string levelNamePrefix = "Level_01";
    Vector2Int boardSize = new Vector2Int(6, 6);
    bool shufflePieces = true;
    bool lockTopRows;
    string levelSOOutputRoot = "Assets/0_Game/Level/_SO";
    readonly int[] levelImageCounts = { 14, 16, 18, 20, 20 };
    float previewSize = 160f;

    readonly List<ImageInfo> images = new List<ImageInfo>();
    Vector2 scroll;

    [MenuItem("Tools/Puzzle/Level Builder")]
    static void Open()
    {
        GetWindow<PuzzleLevelBuilderWindow>("Puzzle Level Builder");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
        levelNamePrefix = EditorGUILayout.TextField("First Level Name", levelNamePrefix);
        boardSize = EditorGUILayout.Vector2IntField("Board Size", boardSize);
        shufflePieces = EditorGUILayout.Toggle("Shuffle Pieces", shufflePieces);
        lockTopRows = EditorGUILayout.Toggle("Lock Top Rows", lockTopRows);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Images", EditorStyles.boldLabel);
        imageFolder = (DefaultAsset)EditorGUILayout.ObjectField("Resources Folder", imageFolder, typeof(DefaultAsset), false);
        levelSOOutputRoot = NormalizeAssetFolderPath(EditorGUILayout.TextField("Level SO Folder", levelSOOutputRoot));
        previewSize = Mathf.Clamp(EditorGUILayout.FloatField("Preview Size", previewSize), 64f, 220f);

        DrawLevelImageCounts();

        using (new EditorGUI.DisabledScope(imageFolder == null))
        {
            if (GUILayout.Button("Scan Image Folder"))
            {
                ScanFolder();
            }
        }

        DrawImages();
        DrawSummary();

        using (new EditorGUI.DisabledScope(imageFolder == null))
        {
            if (GUILayout.Button("Generate 5 Level SOs"))
            {
                GenerateLevelSOs();
            }
        }
    }

    void DrawLevelImageCounts()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Image Count Per Level", EditorStyles.boldLabel);

        for (int i = 0; i < levelImageCounts.Length; i++)
        {
            string levelName = GetLevelName(i);
            levelImageCounts[i] = Mathf.Max(1, EditorGUILayout.IntField(levelName, levelImageCounts[i]));
        }
    }

    void DrawImages()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Images", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(360f), GUILayout.ExpandHeight(true));

        for (int i = 0; i < images.Count; i++)
        {
            ImageInfo image = images[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.Width(previewSize), GUILayout.Height(previewSize));
            DrawTexturePreview(previewRect, image.texture);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Name", image.texture != null ? image.texture.name : "None");
            if (image.texture != null)
            {
                EditorGUILayout.LabelField("Source Size", image.texture.width + " x " + image.texture.height);
            }

            EditorGUILayout.LabelField("Picture Size", image.validName ? image.size.x + " x " + image.size.y : "Invalid name");
            EditorGUILayout.LabelField("Pieces", image.validName ? (image.size.x * image.size.y).ToString() : "0");

            if (!image.validName)
            {
                EditorGUILayout.HelpBox("Image name must contain size like 2x3 or 2x1.", MessageType.Warning);
            }

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
        int validImages = 0;
        int pieceCount = 0;

        for (int i = 0; i < images.Count; i++)
        {
            if (!images[i].validName)
            {
                continue;
            }

            validImages++;
            pieceCount += images[i].size.x * images[i].size.y;
        }

        EditorGUILayout.HelpBox(
            $"Valid images: {validImages}\nPieces if all used: {pieceCount}\nBoard capacity: {boardSize.x * boardSize.y}",
            MessageType.Info
        );
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

            bool validName = TryParseImageSize(texture.name, out Vector2Int size);
            images.Add(new ImageInfo
            {
                texture = texture,
                size = validName ? size : Vector2Int.one,
                validName = validName
            });
        }

        images.Sort((a, b) => string.Compare(a.texture.name, b.texture.name, System.StringComparison.Ordinal));
    }

    void GenerateLevelSOs()
    {
        string resourcesFolderPath = GetResourcesFolderPath();
        if (string.IsNullOrEmpty(resourcesFolderPath))
        {
            return;
        }

        string outputFolder = NormalizeAssetFolderPath(levelSOOutputRoot);
        if (string.IsNullOrEmpty(outputFolder))
        {
            Debug.LogError("Level SO Folder is empty.");
            return;
        }

        if (!EnsureFolder(outputFolder))
        {
            return;
        }

        LevelSO lastLevel = null;

        for (int i = 0; i < levelImageCounts.Length; i++)
        {
            LevelSO levelSO = CreateInstance<LevelSO>();
            levelSO.boardSize = boardSize;
            levelSO.initialPieceCount = 0;
            levelSO.shufflePieces = shufflePieces;
            levelSO.lockTopRows = lockTopRows;
            levelSO.resourcesImageFolder = resourcesFolderPath;
            levelSO.imageCount = levelImageCounts[i];

            string levelName = GetLevelName(i);
            string levelPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{levelName}.asset");
            AssetDatabase.CreateAsset(levelSO, levelPath);
            lastLevel = levelSO;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = lastLevel;
        Debug.Log($"Generated {levelImageCounts.Length} levels: {resourcesFolderPath}");
    }

    string GetLevelName(int offset)
    {
        Match match = Regex.Match(levelNamePrefix, @"^(.*?)(\d+)$");
        if (!match.Success)
        {
            return $"{levelNamePrefix}_{offset + 1:00}";
        }

        string prefix = match.Groups[1].Value;
        string numberText = match.Groups[2].Value;
        int number = int.Parse(numberText) + offset;
        return prefix + number.ToString(new string('0', numberText.Length));
    }

    string GetResourcesFolderPath()
    {
        string folderPath = NormalizeAssetFolderPath(AssetDatabase.GetAssetPath(imageFolder));
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError("Please select a valid image folder.");
            return string.Empty;
        }

        const string resourcesFolderName = "/Resources/";
        int resourcesIndex = folderPath.IndexOf(resourcesFolderName, System.StringComparison.Ordinal);
        if (resourcesIndex >= 0)
        {
            return folderPath.Substring(resourcesIndex + resourcesFolderName.Length);
        }

        const string resourcesSuffix = "/Resources";
        if (folderPath.EndsWith(resourcesSuffix, System.StringComparison.Ordinal))
        {
            Debug.LogError("Please select an image folder inside Resources, not the Resources root folder.");
            return string.Empty;
        }

        Debug.LogError("Runtime image folders must be inside a Resources folder, for example Assets/Resources/PuzzleImages/Pack01 or Assets/0_Game/Resources/PuzzleImages/Pack01.");
        return string.Empty;
    }

    bool TryParseImageSize(string imageName, out Vector2Int size)
    {
        size = Vector2Int.one;
        Match match = Regex.Match(imageName, @"(\d+)\s*x\s*(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        size = new Vector2Int(
            Mathf.Max(1, int.Parse(match.Groups[1].Value)),
            Mathf.Max(1, int.Parse(match.Groups[2].Value))
        );
        return true;
    }

    string NormalizeAssetFolderPath(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return string.Empty;
        }

        folderPath = folderPath.Replace('\\', '/').Trim();
        while (folderPath.StartsWith("/"))
        {
            folderPath = folderPath.Substring(1);
        }

        return folderPath.TrimEnd('/');
    }

    bool EnsureFolder(string folderPath)
    {
        folderPath = NormalizeAssetFolderPath(folderPath);
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return true;
        }

        if (!folderPath.StartsWith("Assets/", System.StringComparison.Ordinal))
        {
            Debug.LogError("Level SO Folder must be inside Assets.");
            return false;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"Failed to create Level SO Folder: {folderPath}");
            return false;
        }

        return true;
    }
}
