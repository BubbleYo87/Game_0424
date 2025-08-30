// DashOnLook_Rigidbody.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Rigidbody))]
public class DashOnLook_Rigidbody : MonoBehaviour
{
    [Header("射線設定")]
    public Camera playerCamera;
    public float maxDistance = 50f;
    public LayerMask teleportLayerMask = ~0;

    [Header("位移參數")]
    public float backOffDistance = 2f;
    public float heightOffset = 1f;
    public float dashDuration = 0.1f;

    [Header("指示圖示")]
    public Image indicatorImage;  // 要改色的 UI Image

    [Header("FOV 特效")]
    [Tooltip("最大 FOV 增量")] public float fovIncrease = 20f;
    [Tooltip("FOV 還原時間")] public float fovRecoverTime = 0.1f;

    [Header("URP Motion Blur")]
    [Tooltip("掛載了 Motion Blur Override 的 Global Volume")] public Volume postProcessVolume;
    [Tooltip("Dash 時 Motion Blur 強度 (0~1)")] public float blurIntensity = 1f;
    [Tooltip("Dash 結束後模糊還原時間")] public float blurRecoverTime = 0.1f;

    private Rigidbody rb;
    private bool isDashing = false;
    private bool canDash = false;
    private float originalFOV;
    private MotionBlur urpMotionBlur;
    private float originalBlurIntensity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (playerCamera == null) Debug.LogError("請在 Inspector 指定 playerCamera！");
        if (indicatorImage == null) Debug.LogError("請在 Inspector 指定 indicatorImage！");

        originalFOV = playerCamera != null ? playerCamera.fieldOfView : 60f;

        if (postProcessVolume != null 
            && postProcessVolume.profile.TryGet<MotionBlur>(out urpMotionBlur))
        {
            originalBlurIntensity = urpMotionBlur.intensity.value;
            urpMotionBlur.active = false;
        }
        else
        {
            Debug.LogWarning("找不到 Motion Blur Override，請在 Volume Profile 裡加入並勾選它。");
        }
    }

    private void Update()
    {
        UpdateDashAvailability();
        if (canDash && !isDashing && Input.GetMouseButtonDown(1))
            StartDashCoroutine();
    }

    private void UpdateDashAvailability()
    {
        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            float distance = Vector3.Distance(transform.position, hit.point);
            int hitLayer = hit.collider.gameObject.layer;
            bool isAllowedLayer = (teleportLayerMask.value & (1 << hitLayer)) != 0;
            canDash = isAllowedLayer && distance > backOffDistance;
        }
        else
        {
            canDash = false;
        }

        indicatorImage.color = canDash
            ? new Color32(0xFF, 0x00, 0x00, 0xFF)
            : new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    }

    private void StartDashCoroutine()
    {
        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            return;

        float distance = Vector3.Distance(transform.position, hit.point);
        int hitLayer = hit.collider.gameObject.layer;
        bool isAllowedLayer = (teleportLayerMask.value & (1 << hitLayer)) != 0;
        if (!isAllowedLayer || distance <= backOffDistance)
            return;

        Vector3 dir = (hit.point - playerCamera.transform.position).normalized;
        Vector3 basePos = hit.point - dir * backOffDistance;
        Vector3 targetPos = basePos + Vector3.up * heightOffset;

        StartCoroutine(DashCoroutine(transform.position, targetPos, distance));
    }

    private IEnumerator DashCoroutine(Vector3 startPos, Vector3 targetPos, float dashDistance)
    {
        isDashing = true;
        bool origColl = rb.detectCollisions;
        rb.detectCollisions = false;

        if (urpMotionBlur != null)
            urpMotionBlur.active = true;

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            float t = elapsed / dashDuration;
            rb.MovePosition(Vector3.Lerp(startPos, targetPos, t));
            float fovDelta = fovIncrease * (dashDistance / maxDistance);
            playerCamera.fieldOfView = Mathf.Lerp(originalFOV, originalFOV + fovDelta, t);
            if (urpMotionBlur != null)
                urpMotionBlur.intensity.value = Mathf.Lerp(originalBlurIntensity, blurIntensity, t);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(targetPos);
        float finalFovDelta = fovIncrease * (dashDistance / maxDistance);
        playerCamera.fieldOfView = originalFOV + finalFovDelta;
        if (urpMotionBlur != null)
            urpMotionBlur.intensity.value = blurIntensity;

        rb.detectCollisions = origColl;

        // 恢復 FOV & 模糊
        elapsed = 0f;
        while (elapsed < fovRecoverTime)
        {
            float t = elapsed / fovRecoverTime;
            playerCamera.fieldOfView = Mathf.Lerp(originalFOV + finalFovDelta, originalFOV, t);
            if (urpMotionBlur != null)
                urpMotionBlur.intensity.value = Mathf.Lerp(blurIntensity, originalBlurIntensity, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        playerCamera.fieldOfView = originalFOV;
        if (urpMotionBlur != null)
        {
            urpMotionBlur.intensity.value = originalBlurIntensity;
            urpMotionBlur.active = false;
        }

        isDashing = false;
    }
}
