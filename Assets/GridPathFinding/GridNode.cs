using UnityEngine;

public class GridNode
{
    public Vector2Int gridPos; // Tọa độ (x, y) trên mảng
    public Vector3 worldPos;   // Tọa độ thực tế trong không gian 3D/2D
    public bool isWalkable;    // Ô này có đang bị xô khác chiếm không?
    public GridNode parent;    // Dùng để truy vết lại đường đi

    public GridNode(Vector2Int gPos, Vector3 wPos)
    {
        gridPos = gPos;
        worldPos = wPos;
        isWalkable = true;
    }
}