using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    [SerializeField][Tooltip("時間")] private float duration = 0.2f;
    [SerializeField][Tooltip("規模")] private float magnitude = 0.3f;
    private Vector3 originalPos;

    private void Awake()
    {
        originalPos = transform.localPosition;
        // 5. 訂閱全域事件
        GameEvents.OnCameraShake += StartShake;
    }

    private void OnDestroy()
    {
        // 6. 取消訂閱
        GameEvents.OnCameraShake -= StartShake;
    }

    private void StartShake()
    {
        StartCoroutine(ShakeCoroutine());
    }

    private IEnumerator ShakeCoroutine()
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
