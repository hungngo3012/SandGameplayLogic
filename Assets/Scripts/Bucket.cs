using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class Bucket : MonoBehaviour
{
    [Header("References")]
    public SandManager SandManager;
    public GridManager GridManager;
    public Renderer Renderer;

    public int color = 1;
    internal int capacity = 128;
    public int collectedSands = 0;

    private void Start()
    {
        
    }

    internal void Init(int capacity)
    {
        this.capacity = capacity;
        InitColor();
        Move();
        CheckCollectedSands().Forget();
    }
    void InitColor()
    {
        if (!colorInitalized)
        {
            Renderer.material.color = GridManager.colorConfigs.FirstOrDefault(x => x.Value.colorId == color).Key;
            colorInitalized = true;
        }
    }
    
    [ContextMenu("CollectSand")]
    public void CollectSand()
    {
        int centerX = GetCenterXFromBucket();
        if(centerX < 0 || centerX >= GridManager.columns - 1)
            return;
        int collected = SandManager.ApplyAction(centerX, 0, color, capacity - collectedSands, transform);
        collectedSands += collected;
    }
    int GetCenterXFromBucket()
    {
        // 1. Lấy vị trí world của Bucket
        float worldX = transform.position.x;

        // 2. Quy về tọa độ tương đối so với góc dưới-trái của grid
        float relativeX = worldX - GridManager.gridStartPosition.x - 1.0f;

        // 3. Đổi sang index ô lưới
        int centerX = Mathf.FloorToInt(relativeX / GridManager.spriteSize);

        // 4. Clamp để tránh out of range
        /*centerX = Mathf.Clamp(centerX, 0, GridManager.columns - 1);*/

        return centerX;
    }

    [ContextMenu("MoveTest")]
    void Move() //test
    {
        transform.DOMoveX(15.0f, 3.0f).SetSpeedBased().SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
    }

    bool colorInitalized = false;
    async UniTask CheckCollectedSands()
    {
        while (collectedSands < capacity)
        {
            CollectSand();
            await UniTask.Yield();
        }

        await transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack, 3.5f).AsyncWaitForCompletion();
        GridManager.buckets.Remove(this);
        Destroy(gameObject);
    }
}
