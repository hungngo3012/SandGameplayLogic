using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Random = UnityEngine.Random;

public class GridManager : MonoBehaviour
{
    [SerializeField] internal List<Bucket> buckets = new List<Bucket>(); //tmp
    public SpriteRenderer displayRenderer;

    internal int columns = 128;
    internal int rows = 128;

    [HideInInspector] public int[,] grid;
    [HideInInspector] public Color[,] colors;
    public Dictionary<Color32, ColorCount> colorConfigs = new Dictionary<Color32, ColorCount>(); //tmp
    private Color[] flatColorArray;
    internal Texture2D sandTexture;

    // --- HAI BIẾN BẠN ĐANG THIẾU ---
    public float spriteSize = 0.1f; // Kích thước hiển thị của 1 pixel trong World Space
    public Vector3 gridStartPosition; // Tọa độ góc dưới bên trái của tranh
    // ------------------------------

    public Color backgroundColor = Color.clear;

    void Start()
    {
        // Tự động gán vị trí bắt đầu là vị trí của GameObject này
        gridStartPosition = transform.position;
        //updated = new bool[columns, rows];

        sandTexture = new Texture2D(columns, rows);
        sandTexture.filterMode = FilterMode.Point;
        sandTexture.wrapMode = TextureWrapMode.Clamp;

        // Tạo Sprite và gán cho Renderer
        // 100 ở đây là Pixels Per Unit, spriteSize sẽ tương ứng là 1/100 = 0.01f nếu scale là 1
        displayRenderer.sprite = Sprite.Create(sandTexture, new Rect(0, 0, columns, rows), new Vector2(0, 0), 100);

        //InitializeGrid();
    }

    internal void InitializeGrid()
    {
        Texture2D tex = displayRenderer.sprite.texture;
        Rect r = displayRenderer.sprite.textureRect;
        int texX0 = Mathf.RoundToInt(r.x);
        int texY0 = Mathf.RoundToInt(r.y);
        int texW = Mathf.RoundToInt(r.width);
        int texH = Mathf.RoundToInt(r.height);

        columns = texW;
        rows = texH;

        grid = new int[columns, rows];
        colors = new Color[columns, rows];
        flatColorArray = new Color[columns * rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                float u = (float)x / (columns - 1);
                float v = (float)y / (rows - 1);
                Color pixelColor = tex.GetPixel(texX0 + x, texY0 + y);

                if (pixelColor.a > 0.1f)
                {
                    colors[x, y] = pixelColor;

                    Color32 key = pixelColor;
                    if (!colorConfigs.ContainsKey(key))
                        colorConfigs.Add(key, new ColorCount(colorConfigs.Count + 1, 1));
                    else
                        colorConfigs[key].AddAmount(1);

                    grid[x, y] = colorConfigs[key].colorId;
                }
                else
                {
                    grid[x, y] = 0;
                    colors[x, y] = backgroundColor;
                }
            }
        }

        InitBuckets();
    }

    async UniTask InitBuckets()
    {
        List<int> colorsCapacity = CalculateColorCapacity();

        Dictionary<int, List<Bucket>> bucketsPerColor = new Dictionary<int, List<Bucket>>();
        foreach (var bucket in buckets)
        {
            if (!bucketsPerColor.TryGetValue(bucket.color, out var list))
            {
                list = new List<Bucket>();
                bucketsPerColor.Add(bucket.color, list);
            }

            list.Add(bucket);
        }

        foreach (var kv in bucketsPerColor)
        {
            int colorId = kv.Key;
            List<Bucket> list = kv.Value;

            int colorIndex = colorId - 1; // QUAN TRỌNG: id 1..N -> index 0..N-1
            if (colorIndex < 0 || colorIndex >= colorsCapacity.Count)
            {
                Debug.LogWarning($"ColorId {colorId} out of capacity range.");
                continue;
            }

            int total = colorsCapacity[colorIndex];
            int count = list.Count;
            int baseCap = (int)((float)total / (float)count);
            int remain = total;

            for (int i = 0; i < count; i++)
            {
                int cap = (i == count - 1) ? remain : baseCap;
                remain -= cap;
                list[i].Init(cap);
                await UniTask.Delay(1000);
            }
        }
    }

    List<int> CalculateColorCapacity()
    {
        List<int> results = new List<int>();
        foreach (var val in colorConfigs)
        {
            results.Add(val.Value.amount);
        }

        return results;
    }

    // HÀM QUAN TRỌNG: Để SandManager gọi khi xóa
    public void UpdatePixelTexture(int x, int y, Color color)
    {
        sandTexture.SetPixel(x, y, color);
    }

    internal void OnDeleteSand()
    {
        UpdateSandLogic();
    }

    internal int numBucketsCollectingSand = 0;
    private void FixedUpdate()
    {
        if (numBucketsCollectingSand <= 0)
            return;

        UpdateSandLogic();
    }

    //bool needUpdateTexture = false;
    private uint _sandSeed = 12345;

    void UpdateSandLogic()
    {
        _sandSeed += (uint)Time.frameCount;
        bool leftToRight = true;
        //Tính toán vị trí mới
        for (int y = 1; y < rows; y++)
        {
            //bool leftToRight = Random.value > 0.5f;
            //bool leftToRight = (y  % 2 == 0) ? true : false;

            // 1. Dùng Xorshift để lấy một số ngẫu nhiên cho hàng này
            _sandSeed ^= _sandSeed << 13;
            _sandSeed ^= _sandSeed >> 17;
            _sandSeed ^= _sandSeed << 5;
            // 2. Quyết định hướng quét X (Trái -> Phải hoặc ngược lại)
            // Dùng bit cuối cùng của seed để quyết định
            leftToRight = (_sandSeed & 1) == 1;

            for (int i = 0; i < columns; i++)
            {
                int x = leftToRight ? i : (columns - 1 - i);

                if (grid[x, y] != 0)
                {
                    int nextX = -1;
                    int nextY = y - 1;

                    if (grid[x, nextY] == 0) nextX = x;
                    else if (x > 0 && x < columns - 1 && grid[x - 1, nextY] == 0 && grid[x + 1, nextY] == 0)
                        nextX = x + (Random.value > 0.5f ? 1 : -1);
                    else if (x < columns - 1 && grid[x + 1, nextY] == 0) nextX = x + 1;
                    else if (x > 0 && grid[x - 1, nextY] == 0) nextX = x - 1;

                    if (nextX != -1)
                    {
                        Color currentColor = colors[x, y];
                        grid[x, y] = 0;
                        grid[nextX, nextY] = colorConfigs[currentColor].colorId;

                        colors[x, y] = backgroundColor;
                        colors[nextX, nextY] = currentColor;
                    }
                }
            }
        }

        //update texture
        //needUpdateTexture = true;
        /*if (!handlingSandTexture)
            HandleSandTexture().Forget();*/
        UpdateSandTexture();
    }

    async UniTask WaitAndCheckClear(int x, int y)
    {
        await UniTask.DelayFrame(3);
        if (!colors[x, y].Equals(backgroundColor))
        {
            colors[x, y] = backgroundColor;
        }
    }

    internal bool pressing = false;
    private bool handlingSandTexture = false;

    [ContextMenu("HandleSandTexture")]
    internal async UniTask HandleSandTexture()
    {
        handlingSandTexture = true;
        while (numBucketsCollectingSand > 0)
        {
            UpdateSandTexture();
            await UniTask.Delay(33);
        }

        UpdateSandTexture();
        handlingSandTexture = false;
    }

    internal void UpdateSandTexture()
    {
        // Chuyển dữ liệu từ mảng 2D sang mảng phẳng 1D
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                flatColorArray[y * columns + x] = colors[x, y];
            }
        }

        // Cập nhật một lần duy nhất
        sandTexture.SetPixels(flatColorArray);
        sandTexture.Apply();

        //CheckClearSand();
    }

    public void OnClickActiveSprite()
    {
        bool active = !displayRenderer.gameObject.activeSelf;
        displayRenderer.gameObject.SetActive(active);
    }

    [ContextMenu("CheckClearSand")]
    void CheckClearSand()
    {
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (grid[x, y] == 0 && !colors[x, y].Equals(backgroundColor))
                    colors[x, y] = backgroundColor;
            }
        }
    }

    public class ColorCount
    {
        public int colorId;
        public int amount;

        public ColorCount(int id, int num)
        {
            colorId = id;
            amount = num;
        }

        public void AddAmount(int amount)
        {
            this.amount += amount;
        }
    }

    internal Vector3 GetPointPosition(int x, int y)
    {
        float worldX = gridStartPosition.x + (x * spriteSize) + (spriteSize * 0.5f);
        float worldY = gridStartPosition.y + (y * spriteSize) + (spriteSize * 0.5f);

        return new Vector3(worldX, worldY, gridStartPosition.z);
    }
}