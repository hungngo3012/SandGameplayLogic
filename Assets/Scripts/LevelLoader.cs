using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    [Header("Input")]
    string relativeLevelPath = "Levels/Level_";
    
    [Header("Output")]
    public SpriteRenderer targetRenderer;
    public Color32 background = new Color32(0, 0, 0, 0);
    public int pixelsPerUnit = 1;

    ParsedLevelData currentLevel;
    
    public GridManager gridManager;

    private int playerLevel = 1;
    private int maxLevel = 10;

    void Start()
    {
        playerLevel = PlayerPrefs.GetInt("PlayerLevel", 1);
        LoadAndRender();
    }

    [ContextMenu("Load & Render")]
    public void LoadAndRender()
    {
        try
        {
            LoadLevelFromFolder();
            RenderToSpriteRenderer();
            
            gridManager.InitializeGrid();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    /*void LoadLevelFromFolder()
    {
        string fullPath = Path.Combine(Application.dataPath, relativeLevelPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"Level file not found: {fullPath}");
            return;
        }

        string content = File.ReadAllText(fullPath);
        Debug.Log($"Read YAML chars = {content.Length} | path = {fullPath}");

        currentLevel = ParseLevelYAML(content);
        currentLevel.name = Path.GetFileNameWithoutExtension(fullPath);

        Debug.Log($"Parsed: chunkGrid={currentLevel.chunkGrid} chunkSize={currentLevel.chunkSize} " +
                  $"size={currentLevel.width}x{currentLevel.height} " +
                  $"chunksBitsLen={(currentLevel.chunksBits?.Length ?? 0)} palette={currentLevel.palette.Count}");
    }*/
    void LoadLevelFromFolder()
    {
        // 1. relativeLevelPath lúc này không được có đuôi file (ví dụ: "Levels/Level1" thay vì "Levels/Level1.yaml")
        // Nếu relativeLevelPath của bạn đang có đuôi file, hãy dùng Path.GetFileNameWithoutExtension
        string resourcePath = relativeLevelPath +  playerLevel.ToString(); 

        // 2. Load file dưới dạng TextAsset
        TextAsset levelAsset = Resources.Load<TextAsset>(resourcePath);

        // 3. Kiểm tra xem file có tồn tại không
        if (levelAsset == null)
        {
            Debug.LogError($"Level file not found in Resources: {resourcePath}");
            return;
        }

        // 4. Lấy nội dung text từ asset
        string content = levelAsset.text;
        Debug.Log($"Read YAML chars = {content.Length} | path = Resources/{resourcePath}");

        // 5. Parse dữ liệu
        currentLevel = ParseLevelYAML(content);
        currentLevel.name = levelAsset.name;

        Debug.Log($"Parsed: chunkGrid={currentLevel.chunkGrid} chunkSize={currentLevel.chunkSize} " +
                  $"size={currentLevel.width}x{currentLevel.height} " +
                  $"chunksBitsLen={(currentLevel.chunksBits?.Length ?? 0)} palette={currentLevel.palette.Count}");
    
        // 6. (Tùy chọn) Giải phóng bộ nhớ nếu file rất nặng
        // Resources.UnloadAsset(levelAsset);
    }

    void RenderToSpriteRenderer()
    {
        if (currentLevel == null)
        {
            Debug.LogError("No level loaded.");
            return;
        }
        if (targetRenderer == null)
        {
            Debug.LogError("targetRenderer is null.");
            return;
        }

        gridManager.sandTexture = BuildTexture(currentLevel, background);

        Sprite sprite = Sprite.Create(
            gridManager.sandTexture,
            new Rect(0, 0, gridManager.sandTexture.width, gridManager.sandTexture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
        );

        targetRenderer.sprite = sprite;
        Debug.Log($"Render done. sprite null? {targetRenderer.sprite == null}");
    }

    #region Parsing

    ParsedLevelData ParseLevelYAML(string yaml)
    {
        var level = new ParsedLevelData();

        // chunkGrid
        var chunkGridMatch = Regex.Match(yaml, @"chunkGrid:\s*\n\s*x:\s*(\d+)\s*\n\s*y:\s*(\d+)");
        if (chunkGridMatch.Success)
        {
            level.chunkGrid = new Vector2Int(
                int.Parse(chunkGridMatch.Groups[1].Value),
                int.Parse(chunkGridMatch.Groups[2].Value)
            );
        }

        // chunkSize
        var chunkSizeMatch = Regex.Match(yaml, @"chunkSize:\s*(\d+)");
        if (chunkSizeMatch.Success)
        {
            level.chunkSize = int.Parse(chunkSizeMatch.Groups[1].Value);
        }

        level.width = level.chunkGrid.x * level.chunkSize;
        level.height = level.chunkGrid.y * level.chunkSize;

        // chunks: binary string between "chunks:" and "chunkSize:"
        // non-greedy để không nuốt quá
        //var chunksMatch = Regex.Match(yaml, @"chunks:\s*([\s\S]*?)\s*chunkSize:", RegexOptions.Singleline);
        var chunksMatch = Regex.Match(yaml, @"chunks:\s*([0-9a-fA-F]+)");
        if (chunksMatch.Success)
        {
            //level.chunksBits = CleanBinaryString(chunksMatch.Groups[1].Value);
            level.chunksBits = chunksMatch.Groups[1].Value;
        }
        else
        {
            Debug.LogWarning("Cannot parse chunks block.");
            level.chunksBits = "";
        }

        // colors: map Key(Color) -> Value(int). Mình lấy Key làm palette theo thứ tự xuất hiện.
        var colorMatches = Regex.Matches(
            yaml,
            @"Key:\s*\{r:\s*([\d.]+),\s*g:\s*([\d.]+),\s*b:\s*([\d.]+),\s*a:\s*([\d.]+)\}\s*\n\s*Value:\s*(\d+)"
        );

        foreach (Match m in colorMatches)
        {
            Color32 color = new Color32(
                (byte)(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 255.0f),
                (byte)(float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 255.0f),
                (byte)(float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) * 255.0f),
                (byte)(float.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) * 255.0f)
            );

            int valueId = int.Parse(m.Groups[5].Value);

            if (!level.colorByValueId.ContainsKey(valueId))
                level.colorByValueId.Add(valueId, color);

            level.palette.Add(color);
        }

        // name
        var nameMatch = Regex.Match(yaml, @"m_Name:\s*(.+)");
        if (nameMatch.Success)
        {
            level.name = nameMatch.Groups[1].Value.Trim();
        }

        return level;
    }

    static string CleanBinaryString(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '0' || c == '1') sb.Append(c);
        }
        return sb.ToString();
    }

    class ParsedLevelData
    {
        public string name;
        public int width;
        public int height;
        public Vector2Int chunkGrid;
        public int chunkSize = 4;

        public string chunksBits;

        // Key(Value int) -> Color (giữ lại nếu sau này bạn cần)
        public Dictionary<int, Color32> colorByValueId = new Dictionary<int, Color32>();

        // Palette theo thứ tự xuất hiện trong YAML
        public List<Color32> palette = new List<Color32>();
    }

    #endregion

    #region Render

    Texture2D BuildTexture(ParsedLevelData level, Color32 bg)
    {
        int width = level.width;
        int height = level.height;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

        if (string.IsNullOrEmpty(level.chunksBits))
        {
            Debug.LogWarning("chunksBits empty -> render blank.");
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        int chunkCount = level.chunkGrid.x * level.chunkGrid.y;
        int pixelsPerChunk = level.chunkSize * level.chunkSize; // 16 nếu chunkSize=4
        int dataLen = level.chunksBits.Length;

        Debug.Log($"chunksBits={dataLen} | chunkCount={chunkCount} | pixelsPerChunk={pixelsPerChunk} | palette={level.palette.Count}");

        if (dataLen % chunkCount != 0)
        {
            Debug.LogError($"chunksBits length ({dataLen}) không chia hết cho chunkCount ({chunkCount}). " +
                           $"Regex chunks có thể đang bắt sai vùng.");
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        int bitsPerChunk = dataLen / chunkCount;
        Debug.Log($"bitsPerChunk = {bitsPerChunk}");

        // Màu dùng cho mask/2-bit
        Color32 c0 = level.palette.Count >= 1 ? level.palette[0] : new Color32(255, 255, 255, 255);
        Color32 c1 = level.palette.Count >= 2 ? level.palette[1] : c0;

        int cursor = 0;

        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            int chunkX = chunkIndex % level.chunkGrid.x;
            int chunkY = chunkIndex / level.chunkGrid.x;

            int chunkStart = cursor;

            if (bitsPerChunk == 8)
            {
                // 1 byte / chunk: coi như "chunk color id"
                int chunkValue = ReadValue(level.chunksBits, ref cursor, 8);
                Color32 fill;
                if (chunkValue == 0)
                {
                    fill = level.palette[0];
                }
                else
                {
                    //int palIndex = chunkValue - 1;
                    int palIndex = chunkValue;
                    /*if (level.palette.Count > 0)
                        palIndex = ((palIndex % level.palette.Count) + level.palette.Count) % level.palette.Count;*/
                    
                    fill = level.palette[palIndex];
                }

                // Fill nguyên chunk 4x4
                for (int i = 0; i < pixelsPerChunk; i++)
                {
                    int localX = i % level.chunkSize;
                    int localY = i / level.chunkSize;

                    int px = chunkX * level.chunkSize + localX;
                    int py = chunkY * level.chunkSize + localY;

                    // Nếu bị lật dọc, bật dòng này:
                    // py = (height - 1) - py;

                    pixels[py * width + px] = fill;
                }
            }
            else
            {
                Debug.LogError($"Chưa hỗ trợ bitsPerChunk={bitsPerChunk}. " +
                               $"Bạn gửi mình giá trị bitsPerChunk này để mình viết đúng decode.");
                break;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    /*static int ReadBits(string bits, ref int cursor, int count)
    {
        int v = 0;
        for (int i = 0; i < count; i++)
            v = (v << 1) | (bits[cursor++] == '1' ? 1 : 0);
        return v;
    }*/
    static int ReadValue(string data, ref int cursor, int count = 8)
    {
        int start = -1;
        int end = -1;

        // Duyệt qua cụm 8 ký tự để tìm phạm vi của số thực sự (bỏ qua 0 đầu và 0 cuối)
        for (int i = 0; i < count; i++)
        {
            int currentIndex = cursor + i;
            if (data[currentIndex] != '0')
            {
                if (start == -1) start = currentIndex; // Ghi lại vị trí số khác 0 đầu tiên
                end = currentIndex; // Cập nhật vị trí số khác 0 cuối cùng liên tục
            }
        }

        int value = 0;

        // Nếu không tìm thấy số nào khác 0 (toàn bộ là '0')
        if (start == -1)
        {
            value = 0;
        }
        else
        {
            // Chuyển đoạn từ start đến end thành số nguyên
            for (int i = start; i <= end; i++)
            {
                value = (value * 10) + (data[i] - '0');
            }
        }

        // Luôn luôn di chuyển con trỏ đi đủ 8 bước để sang cụm tiếp theo
        cursor += count;

        return value;
    }
    public void OnNextLevel()
    {
        playerLevel = (playerLevel % maxLevel) + 1;
        PlayerPrefs.SetInt("PlayerLevel", playerLevel);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    #endregion
}
