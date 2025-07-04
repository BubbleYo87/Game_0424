using UnityEngine;

public class SkyboxOnlyCycle : MonoBehaviour
{
    [Header("循環設定")]
    [Tooltip("一個完整旋轉週期所需時間（秒），例如 120 = 2 分鐘旋轉一圈")]
    public float cycleDuration = 120f;
    [Range(0f, 1f)]
    [Tooltip("初始時間 0~1（0 和 1 都是起點）")]
    public float startTime = 0f;

    [Header("天空盒材質")]
    [Tooltip("拖入 DayInTheClouds 這個材質")]
    public Material skyboxMaterial;

    private float timeOfDay;

    void Start()
    {
        // 初始化時間
        timeOfDay = Mathf.Clamp01(startTime);

        // 指定場景使用的天空盒材質
        if (skyboxMaterial != null)
            RenderSettings.skybox = skyboxMaterial;
        else
            Debug.LogWarning("請在 Inspector 指定 skyboxMaterial（DayInTheClouds）");
    }

    void Update()
    {
        // 推進時間：1秒增加 1/cycleDuration
        timeOfDay += Time.deltaTime / cycleDuration;
        if (timeOfDay > 1f) timeOfDay -= 1f;

        // 計算 Skybox 旋轉角度（0~360）
        float rotation = timeOfDay * 360f;

        // 只旋轉天空盒貼圖，不動 Directional Light
        // Panoramic / 6-Sided Shader 支援 _Rotation 屬性
        if (RenderSettings.skybox.HasProperty("_Rotation"))
        {
            RenderSettings.skybox.SetFloat("_Rotation", rotation);
        }
        else
        {
            // 如果你的自訂 Shader 名稱不同，改成對應屬性
            Debug.LogWarning("skybox 材質不支援 _Rotation 屬性");
        }
    }
}
