using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class Mover : MonoBehaviour
{
    [Header("Settings")]
    private float moveSpeed = 6.0f;
    public GridPathfinding gridSystem; // Gán class GridPathfinding vào đây

    [Header("State")]
    public bool isMoving = false;
    private Vector2Int currentGridPos;

    void Start()
    {
        // Khởi tạo vị trí ô lưới ban đầu dựa trên vị trí thế giới
        currentGridPos = WorldToGridPos(transform.position);
        gridSystem.SetGridCanMove(currentGridPos.x, currentGridPos.y, false);
        // Snap vị trí về tâm ô để tránh sai lệch
        transform.position = gridSystem.GetWorldPosFromGrid(currentGridPos);
    }

    [SerializeField] private Vector2Int target;
    [ContextMenu("Test Move Player")]
    public void Test()
    {
        CommandMoveTo(target);
    }
    /// <summary>
    /// Lệnh cho xô di chuyển đến một vị trí đích
    /// </summary>
    public void CommandMoveTo(Vector2Int targetGridPos)
    {
        if (isMoving) return;

        // 1. Kiểm tra xem có đường đi không và lấy danh sách các điểm (Path)
        if (gridSystem.CanReachTarget(currentGridPos, targetGridPos, out List<Vector3> worldPath))
        {
            gridSystem.SetGridCanMove(currentGridPos.x, currentGridPos.y, true);//set vị trí cũ là can move
            StartCoroutine(MoveSequence(worldPath, targetGridPos));
        }
        else
        {
            Debug.LogWarning("Đường đi bị chặn bởi xô khác hoặc vật cản!");
            // Bạn có thể thêm hiệu ứng rung xô (shake) để báo hiệu không đi được
            transform.DOShakePosition(0.5f, 0.2f);
        }
    }

    private System.Collections.IEnumerator MoveSequence(List<Vector3> path, Vector2Int finalGridPos)
    {
        isMoving = true;

        // 2. Di chuyển mượt mà qua các ô bằng DOTween
        // Sử dụng SetOptions(false) để không đóng kín đường
        Tween moveTween = transform.DOPath(path.ToArray(), moveSpeed)
            .SetSpeedBased()
            .SetEase(Ease.Linear);

        yield return moveTween.WaitForCompletion();

        // 3. Cập nhật vị trí ô lưới sau khi đến nơi
        currentGridPos = finalGridPos;
        isMoving = false;
        gridSystem.SetGridCanMove(currentGridPos.x, currentGridPos.y, false);//set vị trí mới là can't move
        
        Debug.Log("Xô đã đến đích tại ô: " + currentGridPos);
    }

    // Hàm hỗ trợ quy đổi vị trí thế giới sang tọa độ Grid
    private Vector2Int WorldToGridPos(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x / gridSystem.cellSize);
        int y = Mathf.RoundToInt(worldPos.z / gridSystem.cellSize);
        return new Vector2Int(x, y);
    }
}