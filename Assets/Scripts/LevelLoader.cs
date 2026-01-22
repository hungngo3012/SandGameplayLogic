using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    [Header("Input")]
    public string relativeLevelPath = "Levels/Level 1.asset";
    
    [Header("Output")]
    public SpriteRenderer targetRenderer;
    public Color32 background = new Color32(0, 0, 0, 0);
    public int pixelsPerUnit = 1;

    ParsedLevelData currentLevel;
    
    public GridManager gridManager;

    void Start()
    {
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
        string resourcePath = relativeLevelPath; 

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
        var chunksMatch = Regex.Match(yaml, @"chunks:\s*([\s\S]*?)\s*chunkSize:", RegexOptions.Singleline);
        if (chunksMatch.Success)
        {
            level.chunksBits = CleanBinaryString(chunksMatch.Groups[1].Value);
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
                (byte)Mathf.RoundToInt(float.Parse(m.Groups[1].Value) * 255f),
                (byte)Mathf.RoundToInt(float.Parse(m.Groups[2].Value) * 255f),
                (byte)Mathf.RoundToInt(float.Parse(m.Groups[3].Value) * 255f),
                (byte)Mathf.RoundToInt(float.Parse(m.Groups[4].Value) * 255f)
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

        Debug.Log($"Pink parsed = {level.palette[0]}");
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

            // CASE A: 16 bits / chunk (1 bit / pixel cho 4x4)
            /*if (bitsPerChunk == pixelsPerChunk)
            {
                for (int i = 0; i < pixelsPerChunk; i++)
                {
                    int localX = i % level.chunkSize;
                    int localY = i / level.chunkSize;

                    int px = chunkX * level.chunkSize + localX;
                    int py = chunkY * level.chunkSize + localY;

                    // Nếu bị lật dọc, bật dòng này:
                    // py = (height - 1) - py;

                    char b = level.chunksBits[cursor++];
                    if (b == '1') pixels[py * width + px] = c0;
                }
            }
            // CASE B: 32 bits / chunk (16 data + 16 padding)
            else if (bitsPerChunk == 32 && pixelsPerChunk == 16)
            {
                // đọc 16 bit đầu là pixel
                for (int i = 0; i < pixelsPerChunk; i++)
                {
                    int localX = i % level.chunkSize;
                    int localY = i / level.chunkSize;

                    int px = chunkX * level.chunkSize + localX;
                    int py = chunkY * level.chunkSize + localY;

                    // Nếu bị lật dọc, bật dòng này:
                    // py = (height - 1) - py;

                    char b = level.chunksBits[cursor++];
                    if (b == '1') pixels[py * width + px] = c0;
                }

                // skip padding 16 bit
                cursor = chunkStart + 32;
            }
            // CASE C: 64 bits / chunk
            // - thử 2bit/pixel (16*2=32) + padding 32
            // - nếu game bạn thực ra là 4bit/pixel (16*4=64) thì cần sửa decode (bảo mình)
            else if (bitsPerChunk == 64 && pixelsPerChunk == 16)
            {
                for (int i = 0; i < pixelsPerChunk; i++)
                {
                    int localX = i % level.chunkSize;
                    int localY = i / level.chunkSize;

                    int px = chunkX * level.chunkSize + localX;
                    int py = chunkY * level.chunkSize + localY;

                    // Nếu bị lật dọc, bật dòng này:
                    // py = (height - 1) - py;

                    int v = ReadBits(level.chunksBits, ref cursor, 2); // 0..3
                    if (v == 1) pixels[py * width + px] = c0;
                    else if (v == 2) pixels[py * width + px] = c1;
                }

                // skip padding 32 bit
                cursor = chunkStart + 64;
            }
            else */if (bitsPerChunk == 8)
            {
                // 1 byte / chunk: coi như "chunk color id"
                int chunkValue = ReadBits(level.chunksBits, ref cursor, 8); // 0..255
                Color32 fill;
                if (chunkValue == 0)
                {
                    fill = level.palette[0];
                }
                else
                {
                    int palIndex = chunkValue - 1;

                    if (level.palette.Count > 0)
                        palIndex = ((palIndex % level.palette.Count) + level.palette.Count) % level.palette.Count;

                    fill = level.palette.Count > 0 ? level.palette[palIndex] : new Color32(255, 255, 255, 255);
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

    static int ReadBits(string bits, ref int cursor, int count)
    {
        int v = 0;
        for (int i = 0; i < count; i++)
            v = (v << 1) | (bits[cursor++] == '1' ? 1 : 0);
        return v;
    }

    #endregion
}
