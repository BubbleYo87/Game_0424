// MouseForwardDash.cs
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class MouseForwardDash : MonoBehaviour
{
    [Header("Dash 來源設定")]
    [Tooltip("持有 comboStep 的腳本")] 
    public PlayerAnimationController comboController;  // ← 指定你的 ComboController

    [Header("攝影機與 URP 後處理")]
    public Camera playerCamera;
    public Volume postProcessVolume;

    [Header("Dash 基本設定")]
    public float dashDuration = 0.1f;
    public float dashDistance = 2f;
    public float collisionOffset = 0.1f;
    public bool disableCollisions = true;
    public float dashCooldown = 1f;

    [Header("高度偏移")]
    public float heightOffset = 0.5f;

    [Header("速度視覺效果")]
    public float fovIncrease = 20f;
    public float blurIntensity = 1f;
    public float blurRecoverTime = 0.1f;

    Rigidbody rb;
    bool isDashing = false;
    float originalFOV;
    float lastDashTime = -999f;
    MotionBlur urpMotionBlur;
    float originalBlurIntensity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // 自動指派主攝影機
        if (playerCamera == null)
            playerCamera = Camera.main;
        if (playerCamera == null)
            Debug.LogError("找不到 Camera，請指定！");

        originalFOV = playerCamera.fieldOfView;

        if (postProcessVolume != null &&
            postProcessVolume.profile.TryGet<MotionBlur>(out urpMotionBlur))
        {
            originalBlurIntensity = urpMotionBlur.intensity.value;
            urpMotionBlur.active = false;
        }
        else
            Debug.LogWarning("找不到 MotionBlur Override！");

        // 檢查 comboController
        if (comboController == null)
            Debug.LogWarning("未指定 ComboController，會使用預設 comboStep=1");
    }

    void Update()
    {
        if (isDashing) return;
        if (Time.time - lastDashTime < dashCooldown) return;

        if (Input.GetMouseButtonDown(0) && !comboController.isAttack)
            {
                StartForwardDash();
            }
    }

    void StartForwardDash()
    {
        // 1. 從 comboController 讀取 comboStep
        int comboStep = 1;
        if (comboController != null)
            comboStep = comboController.comboStep;

        // 2. 用 switch 決定距離倍數
        float stepDistance;
        float stepDuration;
        switch (comboController.comboStep)
        {
            case 1:
                stepDistance = dashDistance;         // 最短
                break;
            case 3:
                stepDistance = dashDistance; // 中等
                break;
            case 2:
                stepDistance = dashDistance * 4f;   // 最長
                stepDuration = dashDuration / 2f; // 2倍快
                break;
            default:
                stepDistance = dashDistance;         // 其他都回到最短
                break;
        }

        // 3. 計算水平面前方方向
        Vector3 dir = playerCamera.transform.forward;
        dir.y = 0f;
        dir.Normalize();

        // 4. 障礙檢測
        float actualDist = stepDistance;
        if (rb.SweepTest(dir, out RaycastHit hit, stepDistance + collisionOffset))
            actualDist = Mathf.Max(0f, hit.distance - collisionOffset);
        if (actualDist <= 0f) return;

        // 5. 啟動 Dash Coroutine
        Vector3 startPos  = transform.position;
        Vector3 targetPos = startPos + dir * actualDist + Vector3.up * heightOffset;
        lastDashTime = Time.time;
        StartCoroutine(DashWithEffects(startPos, targetPos));
    }

    IEnumerator DashWithEffects(Vector3 startPos, Vector3 targetPos)
    {
        isDashing = true;
        bool origColl = rb.detectCollisions;
        if (disableCollisions) rb.detectCollisions = false;
        if (urpMotionBlur != null) urpMotionBlur.active = true;

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            float t = elapsed / dashDuration;
            rb.MovePosition(Vector3.Lerp(startPos, targetPos, t));
            playerCamera.fieldOfView = Mathf.Lerp(originalFOV, originalFOV + fovIncrease, t);
            if (urpMotionBlur != null)
                urpMotionBlur.intensity.value =
                    Mathf.Lerp(originalBlurIntensity, blurIntensity, t);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(targetPos);
        if (disableCollisions) rb.detectCollisions = origColl;

        // 還原 FOV & 模糊
        elapsed = 0f;
        while (elapsed < blurRecoverTime)
        {
            float t = elapsed / blurRecoverTime;
            playerCamera.fieldOfView =
                Mathf.Lerp(originalFOV + fovIncrease, originalFOV, t);
            if (urpMotionBlur != null)
                urpMotionBlur.intensity.value =
                    Mathf.Lerp(blurIntensity, originalBlurIntensity, t);

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
