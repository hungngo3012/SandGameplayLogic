using UnityEngine;
using TMPro;
using UnityEngine.UI; // Thư viện để điều khiển TextMeshPro

public class FPSCounter : MonoBehaviour
{
    public Text fpsText; // Kéo thả Object Text vào đây
    public float updateInterval = 0.5f; // Thời gian làm mới con số (giây)
    
    private float accum = 0; 
    private int frames = 0; 
    private float timeleft; 

    void Start()
    {
        Application.targetFrameRate = 60;
        timeleft = updateInterval; 
    }

    void Update()
    {
        timeleft -= Time.unscaledDeltaTime;
        accum += Time.unscaledDeltaTime;
        ++frames;

        // Cập nhật text sau mỗi khoảng interval để tránh số nhảy quá nhanh gây hoa mắt
        if (timeleft <= 0.0)
        {
            float fps = frames / accum;
            fpsText.text = string.Format("FPS: {0:F0}", fps);

            timeleft = updateInterval;
            accum = 0.0f;
            frames = 0;
        }
    }
}