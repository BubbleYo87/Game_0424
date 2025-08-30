using UnityEngine;

/// <summary>
/// 讓物件在空中自動旋轉並上下漂浮
/// </summary>
public class FloatingRotator : MonoBehaviour
{
    [Header("旋轉參數")]
    [Tooltip("旋轉速度（度/秒）")]
    public float rotationSpeed = 60f;

    [Header("漂浮參數")]
    [Tooltip("漂浮高度振幅")]
    public float floatAmplitude = 0.25f;
    [Tooltip("漂浮速度（頻率）")]
    public float floatFrequency = 1f;

    // 起始高度
    private Vector3 startPos;

    private void Start()
    {
        // 記住一開始的位置
        startPos = transform.position;
    }

    private void Update()
    {
        // 1. 旋轉（以 Y 軸為主）
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // 2. 上下漂浮（用 sin 波）
        float newY = startPos.y + Mathf.Sin(Time.time * floatFrequency * Mathf.PI * 2f) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}
