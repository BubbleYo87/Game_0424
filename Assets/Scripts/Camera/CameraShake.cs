// CameraShake.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    private Vector3 originalPos;

    private void Awake()
    {
        originalPos = transform.localPosition;
        // 5. 訂閱帶參數的全域事件
        GameEvents.OnCameraShake += StartShake;
    }

    private void OnDestroy()
    {
        // 6. 取消訂閱
        GameEvents.OnCameraShake -= StartShake;
    }

    // 接收 duration, magnitude，並啟動協程
    private void StartShake(float duration, float magnitude)
    {
        StartCoroutine(ShakeCoroutine(duration, magnitude));
    }

    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            transform.localPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = originalPos;
    }
}
