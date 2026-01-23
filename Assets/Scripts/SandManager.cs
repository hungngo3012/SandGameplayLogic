using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;

public class SandManager : MonoBehaviour
{
    [Header("Settings")]
    public int touchSize = 3;       // Bán kính vùng ảnh hưởng (cọ vẽ/tẩy)

    public int maxYDifferenceColorHeight = 2;
    
    [Header("References")]
    public GridManager gridManager; // Kéo GridManager vào đây

    private bool isHold;

    [Header("Simulation Speed")]
    public float timeStep = 0.2f;
    private float timer = 0f;
    void Update()
    {
        timer += Time.deltaTime;
        if (timer < timeStep)
            return;
        
        // 1. Xử lý đầu vào chuột
        if (Input.GetMouseButtonDown(0)) isHold = true;
        if (Input.GetMouseButtonUp(0)) isHold = false;
        
        if (isHold)
        {
            HandleInput();
        }
    }

    void HandleInput()
    {
        // 2. Chuyển tọa độ chuột sang tọa độ thế giới (World Space)
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0; // Đảm bảo luôn nằm trên mặt phẳng 2D của tranh

        // 3. Tính toán vị trí tương đối so với góc Dưới-Trái (gridStartPosition) của tranh
        Vector3 relativePos = mouseWorldPos - gridManager.gridStartPosition;

        // 4. Chuyển đổi từ đơn vị mét (World Unit) sang đơn vị ô lưới (Index)
        // Lưu ý: Không dùng Abs ở đây để tránh click bên ngoài bị nhảy vào trong
        int xIndex = Mathf.FloorToInt(relativePos.x / gridManager.spriteSize);
        int yIndex = Mathf.FloorToInt(relativePos.y / gridManager.spriteSize);

        // 5. Thực hiện hành động
        ApplyAction(xIndex, yIndex);
    }
    void ApplyAction(int centerX, int centerY)
    {
        int targetValue = 0;
        for (int i = centerX - touchSize; i <= centerX + touchSize; i++)
        {
            for (int j = centerY - touchSize; j <= centerY + touchSize; j++)
            {
                if (i >= 0 && i < gridManager.columns && j >= 0 && j < gridManager.rows)
                {
                    if (gridManager.grid[i, j] != targetValue)
                    {
                        gridManager.grid[i, j] = targetValue;
                        gridManager.colors[i, j] = gridManager.backgroundColor;
                        gridManager.OnDeleteSand();
                        gridManager.pressing = true;
                    }
                }
            }
        }
    }

    internal void ApplyAction(int centerX, int centerY, int color, int maxSandsCanCollect, Bucket bucket = null)
    {
        int countSandDeleted = 0;
        int targetValue = 0;
        int startI = centerX - touchSize;
        if (startI < 0)
            startI = 0;
        int endJ = centerY + touchSize * 2;
        if(endJ >= gridManager.rows)
            endJ = gridManager.rows - 1;
        
        int endI = centerX + touchSize;
        if(endI >= gridManager.columns)
            endI = gridManager.columns - 1;
        
        List<Vector2> checkList = new List<Vector2>();
        for (int i = startI; i <= endI; i++)
        {
            for (int j = centerY ; j <= endJ; j++)
            {
                if (!(i >= 0 && i < gridManager.columns && j >= 0 && j < gridManager.rows))
                    Debug.Log("Debug: " + i + "-" + j + " / " + gridManager.columns + " - " + gridManager.rows + " / " + endI + "-" + endJ);
                if (gridManager.grid[i, j] != color)
                {
                    if(j > maxSandsCanCollect)
                        break;
                    continue;
                }
                if (i >= 0 && i < gridManager.columns && j >= 0 && j < gridManager.rows)
                {
                    if (gridManager.grid[i, j] != targetValue)
                    {
                        /*if(bucket != null && (i + j) % 10 == 0)
                            SpawnFlyParticle(gridManager.GetPointPosition(i, j), gridManager.colors[i, j], bucket.transform).Forget();//tmp*/
                        gridManager.grid[i, j] = targetValue;
                        gridManager.colors[i, j] = gridManager.backgroundColor;
                        /*if(j + 1 >= gridManager.rows || (gridManager.colors[i, j + 1].Equals(gridManager.backgroundColor)))
                            gridManager.colors[i, j] = gridManager.backgroundColor;
                        else
                            checkList.Add(new Vector2(i, j));*/
                        gridManager.OnDeleteSand();
                        gridManager.pressing = true;
                        countSandDeleted++;
                        if (countSandDeleted == maxSandsCanCollect)
                        {
                            if (bucket != null) bucket.collected = countSandDeleted;
                            return;
                        }
                    }
                }
            }
        }
        
        WaitToClearSand(checkList).Forget();
        if (bucket != null) bucket.collected = countSandDeleted;
    }
    public SandPoint sandParticlePrefab;
    async UniTask SpawnFlyParticle(Vector3 startPos, Color color, Transform target)//tmp
    {
        if (sandParticlePrefab == null) return;

        // Tạo hạt cát
        SandPoint p = Instantiate(sandParticlePrefab, startPos, Quaternion.identity);
        p.transform.SetParent(target);
        p.SetColor(color);
    
        // Đổi màu hạt cát cho giống màu vừa xóa (nếu bạn có bảng màu)
        // p.GetComponent<SpriteRenderer>().color = ...; 

        float duration = 0.5f; // Thời gian bay (giây)
        float elapsed = 0f;

        await p.transform.DOLocalMove(Vector3.zero, duration).AsyncWaitForCompletion();

        Destroy(p);
    }
    async UniTask WaitToClearSand(List<Vector2> checkList)
    {
        foreach (var val in checkList)
        {
            await UniTask.Yield();
            if(gridManager.grid[(int)val.x, (int)val.y] == 0)
                gridManager.colors[(int)val.x, (int)val.y] = gridManager.backgroundColor;
        }
    }

    [ContextMenu("Clear All Sands")]
    void ClearAllSands()
    {
        for (int i = 0; i < gridManager.columns; i++)
        {
            for (int j = 0; j < gridManager.rows; j++)
            {
                gridManager.grid[i, j] = 0;
                gridManager.colors[i, j] = gridManager.backgroundColor;
            }
        }
        gridManager.UpdateSandTexture();
    }
}