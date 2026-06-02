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

    class LevelBuildConfig
    {
        public int imageCount;
        public bool hasLockPieces;
        public readonly List<LevelLockPieceData> lockPieces = new List<LevelLockPieceData>();
        public bool isTutorialLevel;
        public readonly List<Vector2Int> tutorialGroupCells = new List<Vector2Int>();
        public Vector2Int tutorialPieceCell = new Vector2Int(-1, -1);

        public LevelBuildConfig(int imageCount)
        {
            this.imageCount = imageCount;
        }
    }

    DefaultAsset imageFolder;
    string levelNamePrefix = "Level_01";
    Vector2Int boardSize = new Vector2Int(6, 6);
    bool shufflePieces = true;
    bool lockTopRows;
    string levelSOOutputRoot = "Assets/0_Game/Level/_SO";
    readonly LevelBuildConfig[] levelConfigs =
    {
        new LevelBuildConfig(14),
        new LevelBuildConfig(16),
        new LevelBuildConfig(18),
        new LevelBuildConfig(20),
        new LevelBuildConfig(20)
    };
    float previewSize = 160f;

    readonly List<ImageInfo> images = new List<ImageInfo>();
    Vector2 scroll;
    Vector2 levelSettingsScroll;
    bool showLevelSettings = true;
    bool showImages = true;

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

        using (new EditorGUI.DisabledScope(imageFolder == null))
        {
            if (GUILayout.Button("Scan Image Folder"))
            {
                ScanFolder();
            }
        }

        using (new EditorGUI.DisabledScope(imageFolder == null))
        {
            if (GUILayout.Button("Generate 5 Level SOs"))
            {
                GenerateLevelSOs();
            }
        }

        DrawLevelConfigs();
        DrawSummary();
        DrawImages();
    }

    void DrawLevelConfigs()
    {
        EditorGUILayout.Space();
        showLevelSettings = EditorGUILayout.Foldout(showLevelSettings, "Level Settings", true);
        if (!showLevelSettings)
        {
            return;
        }

        levelSettingsScroll = EditorGUILayout.BeginScrollView(levelSettingsScroll, GUILayout.Height(260f));

        for (int i = 0; i < levelConfigs.Length; i++)
        {
            string levelName = GetLevelName(i);
            LevelBuildConfig config = levelConfigs[i];

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(levelName, EditorStyles.boldLabel);
            config.imageCount = Mathf.Max(1, EditorGUILayout.IntField("Image Count", config.imageCount));
            config.hasLockPieces = EditorGUILayout.Toggle("Has Lock Pieces", config.hasLockPieces);

            if (config.hasLockPieces)
            {
                DrawLockGrid(config);
            }

            config.isTutorialLevel = EditorGUILayout.Toggle("Tutorial Level", config.isTutorialLevel);
            if (config.isTutorialLevel)
            {
                DrawTutorialGrid(config);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawLockGrid(LevelBuildConfig config)
    {
        EditorGUILayout.LabelField("Lock Board");

        for (int y = boardSize.y - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < boardSize.x; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                LevelLockPieceData lockData = FindLockData(config, cell);
                string label = lockData != null ? lockData.unlockImageCount.ToString() : ".";
                Color oldColor = GUI.backgroundColor;
                GUI.backgroundColor = lockData != null ? new Color(1f, 0.7f, 0.25f) : oldColor;

                if (GUILayout.Button(label, GUILayout.Width(34f), GUILayout.Height(26f)))
                {
                    if (lockData != null)
                    {
                        config.lockPieces.Remove(lockData);
                    }
                    else
                    {
                        config.lockPieces.Add(new LevelLockPieceData
                        {
                            cell = cell,
                            unlockImageCount = 1
                        });
                    }
                }

                GUI.backgroundColor = oldColor;
            }
            EditorGUILayout.EndHorizontal();
        }

        for (int i = config.lockPieces.Count - 1; i >= 0; i--)
        {
            LevelLockPieceData lockData = config.lockPieces[i];
            if (lockData == null || lockData.cell.x < 0 || lockData.cell.x >= boardSize.x || lockData.cell.y < 0 || lockData.cell.y >= boardSize.y)
            {
                config.lockPieces.RemoveAt(i);
                continue;
            }

            lockData.unlockImageCount = Mathf.Max(1, EditorGUILayout.IntField($"Cell {lockData.cell.x},{lockData.cell.y}", lockData.unlockImageCount));
        }
    }

    LevelLockPieceData FindLockData(LevelBuildConfig config, Vector2Int cell)
    {
        for (int i = 0; i < config.lockPieces.Count; i++)
        {
            if (config.lockPieces[i] != null && config.lockPieces[i].cell == cell)
            {
                return config.lockPieces[i];
            }
        }

        return null;
    }

    void DrawTutorialGrid(LevelBuildConfig config)
    {
        EditorGUILayout.LabelField("Tutorial Board: G = group, P = remaining piece");

        for (int y = boardSize.y - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < boardSize.x; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                bool isGroupCell = config.tutorialGroupCells.Contains(cell);
                bool isPieceCell = config.tutorialPieceCell == cell;
                string label = isGroupCell ? "G" : isPieceCell ? "P" : ".";
                Color oldColor = GUI.backgroundColor;
                GUI.backgroundColor = isGroupCell ? new Color(0.3f, 0.9f, 0.45f) : isPieceCell ? new Color(0.35f, 0.65f, 1f) : oldColor;

                if (GUILayout.Button(label, GUILayout.Width(34f), GUILayout.Height(26f)))
                {
                    ToggleTutorialCell(config, cell);
                }

                GUI.backgroundColor = oldColor;
            }
            EditorGUILayout.EndHorizontal();
        }

        string message = $"Group cells: {config.tutorialGroupCells.Count}/3, Piece cell: {(IsValidCell(config.tutorialPieceCell) ? "1/1" : "0/1")}";
        MessageType messageType = IsValidTutorialConfig(config) ? MessageType.Info : MessageType.Warning;
        EditorGUILayout.HelpBox(message, messageType);
    }

    void ToggleTutorialCell(LevelBuildConfig config, Vector2Int cell)
    {
        if (config.tutorialGroupCells.Contains(cell))
        {
            config.tutorialGroupCells.Remove(cell);
            config.tutorialPieceCell = cell;
            return;
        }

        if (config.tutorialPieceCell == cell)
        {
            config.tutorialPieceCell = new Vector2Int(-1, -1);
            return;
        }

        if (config.tutorialGroupCells.Count < 3)
        {
            config.tutorialGroupCells.Add(cell);
            return;
        }

        config.tutorialPieceCell = cell;
    }

    bool IsValidTutorialConfig(LevelBuildConfig config)
    {
        return config.tutorialGroupCells.Count == 3
            && IsValidCell(config.tutorialPieceCell)
            && AreTutorialGroupCellsConnected(config.tutorialGroupCells)
            && DoTutorialGroupCellsFit2x2(config.tutorialGroupCells);
    }

    bool IsValidCell(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < boardSize.x && cell.y >= 0 && cell.y < boardSize.y;
    }

    bool AreTutorialGroupCellsConnected(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count != 3)
        {
            return false;
        }

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        visited.Add(cells[0]);
        queue.Enqueue(cells[0]);

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            TryAddTutorialNeighbor(cell + Vector2Int.up, cells, visited, queue);
            TryAddTutorialNeighbor(cell + Vector2Int.right, cells, visited, queue);
            TryAddTutorialNeighbor(cell + Vector2Int.down, cells, visited, queue);
            TryAddTutorialNeighbor(cell + Vector2Int.left, cells, visited, queue);
        }

        return visited.Count == cells.Count;
    }

    bool DoTutorialGroupCellsFit2x2(List<Vector2Int> cells)
    {
        Vector2Int minCell = cells[0];
        Vector2Int maxCell = cells[0];
        for (int i = 1; i < cells.Count; i++)
        {
            minCell = new Vector2Int(Mathf.Min(minCell.x, cells[i].x), Mathf.Min(minCell.y, cells[i].y));
            maxCell = new Vector2Int(Mathf.Max(maxCell.x, cells[i].x), Mathf.Max(maxCell.y, cells[i].y));
        }

        return maxCell.x - minCell.x <= 1 && maxCell.y - minCell.y <= 1;
    }

    void TryAddTutorialNeighbor(Vector2Int cell, List<Vector2Int> cells, HashSet<Vector2Int> visited, Queue<Vector2Int> queue)
    {
        if (!cells.Contains(cell) || visited.Contains(cell))
        {
            return;
        }

        visited.Add(cell);
        queue.Enqueue(cell);
    }

    void DrawImages()
    {
        EditorGUILayout.Space();
        showImages = EditorGUILayout.Foldout(showImages, "Images", true);
        if (!showImages)
        {
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(300f), GUILayout.ExpandHeight(true));

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

        for (int i = 0; i < levelConfigs.Length; i++)
        {
            LevelBuildConfig config = levelConfigs[i];
            LevelSO levelSO = CreateInstance<LevelSO>();
            levelSO.boardSize = boardSize;
            levelSO.initialPieceCount = 0;
            levelSO.shufflePieces = shufflePieces;
            levelSO.lockTopRows = lockTopRows;
            levelSO.resourcesImageFolder = resourcesFolderPath;
            levelSO.imageCount = config.imageCount;
            levelSO.hasLockPieces = config.hasLockPieces;
            levelSO.isTutorialLevel = config.isTutorialLevel;

            if (config.isTutorialLevel)
            {
                if (!IsValidTutorialConfig(config))
                {
                    Debug.LogError($"{GetLevelName(i)} tutorial config must have 3 connected group cells and 1 piece cell.");
                    return;
                }

                levelSO.tutorialGroupCells.AddRange(config.tutorialGroupCells);
                levelSO.tutorialPieceCell = config.tutorialPieceCell;
            }

            if (config.hasLockPieces)
            {
                for (int j = 0; j < config.lockPieces.Count; j++)
                {
                    LevelLockPieceData lockData = config.lockPieces[j];
                    if (lockData == null)
                    {
                        continue;
                    }

                    levelSO.lockPieces.Add(new LevelLockPieceData
                    {
                        cell = lockData.cell,
                        unlockImageCount = Mathf.Max(1, lockData.unlockImageCount)
                    });
                }
            }

            string levelName = GetLevelName(i);
            string levelPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{levelName}.asset");
            AssetDatabase.CreateAsset(levelSO, levelPath);
            lastLevel = levelSO;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = lastLevel;
        Debug.Log($"Generated {levelConfigs.Length} levels: {resourcesFolderPath}");
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
