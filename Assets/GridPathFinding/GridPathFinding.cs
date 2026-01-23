using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class GridPathfinding : MonoBehaviour
{
    public int width = 10;
    public int height = 15;
    public float cellSize = 1f;
    public LayerMask bucketLayer; // Layer để check xem xô khác có đang đứng đó không

    [Header("Manual Obstacles")]
    // Danh sách tọa độ các ô bạn muốn làm vật cản (nhập tay trên Inspector)
    public List<Vector2Int> staticObstacleCoords = new List<Vector2Int>();
    
    private GridNode[,] grid;
    
    [SerializeField] GameObject obstaclePrefab;

    void Awake()
    {
        InitializeGrid();
    }
    internal void SetGridCanMove(int x, int y, bool canMove)
    {
        grid[x, y].isWalkable = canMove;
    }

    // Khởi tạo bàn chơi dựa trên kích thước ô
    void InitializeGrid()
    {
        grid = new GridNode[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 worldPos = new Vector3(x * cellSize, 0, y * cellSize); // Giả sử mặt phẳng nằm ngang
                grid[x, y] = new GridNode(new Vector2Int(x, y), worldPos);
                
                if (staticObstacleCoords.Contains(new Vector2Int(x, y)))
                {
                    // 1. Khóa luôn ô này, không cho đi qua
                    grid[x, y].isWalkable = false;

                    // 2. Tạo Cube vật cản tại đây
                    if (obstaclePrefab != null)
                    {
                        GameObject obj = Instantiate(obstaclePrefab, worldPos, Quaternion.identity, transform);
                        obj.transform.localScale = Vector3.one * cellSize;
                        // Gán tên cho dễ quản lý
                        obj.name = $"Obstacle_{x}_{y}";
                    }
                }
            }
        }
    }

    // Cập nhật trạng thái các ô (ô nào có xô đứng thì không cho đi qua)
    public void UpdateNodesWalkability()
    {
        foreach (var node in grid)
        {
            // Kiểm tra tại vị trí ô có Collider của xô khác không
            Collider[] colliders = Physics.OverlapSphere(node.worldPos, cellSize * 0.4f, bucketLayer);
            
            // Nếu có vật cản và vật đó không phải là chính mình
            node.isWalkable = (colliders.Length == 0);
        }
    }

    // HÀM CHÍNH: Kiểm tra xem có thể đi đến đích hay không
    public bool CanReachTarget(Vector2Int startCoord, Vector2Int targetCoord, out List<Vector3> worldPath)
    {
        worldPath = new List<Vector3>();
        //UpdateNodesWalkability(); // Cập nhật vật cản trước khi tìm

        if (!IsInsideGrid(targetCoord) || !grid[targetCoord.x, targetCoord.y].isWalkable)
            return false;

        Queue<GridNode> openSet = new Queue<GridNode>();
        HashSet<GridNode> closedSet = new HashSet<GridNode>();

        GridNode startNode = grid[startCoord.x, startCoord.y];
        GridNode targetNode = grid[targetCoord.x, targetCoord.y];

        openSet.Enqueue(startNode);
        
        while (openSet.Count > 0)
        {
            GridNode currentNode = openSet.Dequeue();
            if (currentNode == targetNode)
            {
                worldPath = RetracePath(startNode, targetNode);
                return true;
            }

            foreach (GridNode neighbor in GetNeighbors(currentNode))
            {
                if (!neighbor.isWalkable || closedSet.Contains(neighbor)) continue;

                if (!openSet.Contains(neighbor))
                {
                    neighbor.parent = currentNode;
                    openSet.Enqueue(neighbor);
                }
            }
            closedSet.Add(currentNode);
        }

        return false;
    }

    // Lấy các ô lân cận (Lên, Xuống, Trái, Phải)
    List<GridNode> GetNeighbors(GridNode node)
    {
        List<GridNode> neighbors = new List<GridNode>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in dirs)
        {
            Vector2Int checkPos = node.gridPos + dir;
            if (IsInsideGrid(checkPos))
                neighbors.Add(grid[checkPos.x, checkPos.y]);
        }
        return neighbors;
    }

    bool IsInsideGrid(Vector2Int pos) => pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;

    List<Vector3> RetracePath(GridNode startNode, GridNode endNode)
    {
        List<Vector3> path = new List<Vector3>();
        GridNode curr = endNode;
        while (curr != startNode)
        {
            path.Add(curr.worldPos);
            curr = curr.parent;
        }
        path.Reverse();
        return path;
    }
    void OnDrawGizmos()
    {
        if (grid == null) return;
        foreach (var node in grid)
        {
            Gizmos.color = node.isWalkable ? Color.white : Color.red;
            Gizmos.DrawWireCube(node.worldPos, Vector3.one * (cellSize * 0.9f));
        }
    }
    public Vector3 GetWorldPosFromGrid(Vector2Int gridPos)
    {
        // Kiểm tra để tránh lỗi IndexOutOfRangeException nếu tọa độ nằm ngoài mảng
        if (IsInsideGrid(gridPos))
        {
            return grid[gridPos.x, gridPos.y].worldPos;
        }
    
        Debug.LogError($"Tọa độ Grid {gridPos} nằm ngoài phạm vi bàn chơi!");
        return Vector3.zero;
    }
}