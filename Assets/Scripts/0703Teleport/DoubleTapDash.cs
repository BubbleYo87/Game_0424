// DoubleTapDash.cs
using UnityEngine;
using UnityEngine.Rendering;                    // URP Post-processing
using UnityEngine.Rendering.Universal;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class DoubleTapDash : MonoBehaviour
{
    [Header("攝影機與 URP 後處理")]
    [Tooltip("用來判定方向並修改 FOV 的攝影機")] public Camera playerCamera;
    [Tooltip("掛載了 Motion Blur Override 的 Global Volume")] public Volume postProcessVolume;

    [Header("連擊判定")]
    [Tooltip("兩次按鍵間隔的最大時間（秒）")] public float doubleTapTime = 0.3f;

    [Header("Dash 設定")]
    [Tooltip("Dash 持續時間（秒）")] public float dashDuration = 0.1f;
    [Tooltip("最大 Dash 距離（世界單位）")] public float dashDistance = 2f;
    [Tooltip("障礙檢測預留距離")] public float collisionOffset = 0.1f;
    [Tooltip("Dash 時暫時關閉碰撞")] public bool disableCollisions = true;
    [Tooltip("Dash 冷卻時間（秒）")] public float dashCooldown = 1f;

    [Header("高度偏移")]
    [Tooltip("Dash 結束時抬高的 Y 偏移")] public float heightOffset = 0.5f;

    [Header("速度視覺效果")]
    [Tooltip("衝刺時 FOV 增量")] public float fovIncrease = 20f;
    [Tooltip("Dash 時 Motion Blur 強度 (0~1)")] public float blurIntensity = 1f;
    [Tooltip("Dash 結束後模糊還原時間")] public float blurRecoverTime = 0.1f;

    [Header("外部 Animator 參考")]
    [Tooltip("用來播放 Dash 動畫的 Animator")] public Animator targetAnimator;

    // 私有欄位
    private Rigidbody rb;
    private bool isDashing = false;
    private KeyCode lastTapKey = KeyCode.None;
    private float lastTapTimeStamp;
    private float originalFOV;
    private float lastDashTime = -999f;

    // URP Motion Blur
    private MotionBlur urpMotionBlur;
    private float originalBlurIntensity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (playerCamera == null)
            playerCamera = Camera.main;
        if (playerCamera == null)
            Debug.LogError("DoubleTapDash：找不到 Camera，請在 Inspector 指定！");

        originalFOV = playerCamera.fieldOfView;

        // 取得 URP Volume 裡的 MotionBlur Override
        if (postProcessVolume != null && postProcessVolume.profile.TryGet<MotionBlur>(out urpMotionBlur))
        {
            originalBlurIntensity = urpMotionBlur.intensity.value;
            urpMotionBlur.active = false;
        }
        else
        {
            Debug.LogWarning("DoubleTapDash：找不到 MotionBlur Override，請在 Volume Profile 裡加入並打勾它。");
        }

        // 檢查是否已經把目標 Animator 指定進來
        if (targetAnimator == null)
            Debug.LogWarning("DoubleTapDash：目標 Animator (targetAnimator) 未指定，將無法觸發 Dash 動畫。");
    }

    void Update()
    {
        if (isDashing) return;

        // 冷卻檢查
        if (Time.time - lastDashTime < dashCooldown) return;

        TryCheckDoubleTap(KeyCode.W);
        TryCheckDoubleTap(KeyCode.S);
        TryCheckDoubleTap(KeyCode.A);
        TryCheckDoubleTap(KeyCode.D);
    }

    private void TryCheckDoubleTap(KeyCode key)
    {
        if (!Input.GetKeyDown(key)) 
            return;

        float elapsed = Time.time - lastTapTimeStamp;
        if (lastTapKey == key && elapsed <= doubleTapTime)
        {
            // 計算水平面方向
            Vector3 camF = playerCamera.transform.forward; camF.y = 0; camF.Normalize();
            Vector3 camR = playerCamera.transform.right;   camR.y = 0; camR.Normalize();
            Vector3 dir = key == KeyCode.W ? camF
                        : key == KeyCode.S ? -camF
                        : key == KeyCode.A ? -camR
                        : camR;

            // 前向障礙偵測，算出實際可 Dash 距離
            float actualDist = dashDistance;
            if (rb.SweepTest(dir, out RaycastHit hit, dashDistance + collisionOffset))
                actualDist = Mathf.Max(0f, hit.distance - collisionOffset);

            // 如果實際距離 <= 0，表示在預留距離內就被擋住 → Dash 失敗
            if (actualDist <= 0f)
            {
                // 清除 lastTapKey，等待下一次連擊
                lastTapKey = KeyCode.None;
                // 不觸發任何動畫，也不進行 Dash
                return;
            }

            // 到這裡才是真正能成功 Dash，先觸發對應的 Animator Trigger
            TriggerDashAnimation(key);

            // 準備執行 Dash：計算起點與終點
            Vector3 startPos  = transform.position;
            Vector3 targetPos = startPos + dir * actualDist + Vector3.up * heightOffset;

            // 記錄冷卻時間戳
            lastDashTime = Time.time;
            // 執行 Dash 並帶特效
            StartCoroutine(DashWithEffects(startPos, targetPos));

            // 清除 lastTapKey，避免重複觸發
            lastTapKey = KeyCode.None;
        }
        else
        {
            // 如果還沒達到 double-tap 條件，更新 lastTapKey/Time
            lastTapKey = key;
            lastTapTimeStamp = Time.time;
        }
    }


    /// <summary>
    /// 根據按鍵觸發對應的 Dash 動畫 Trigger，
    /// 例如 W → "Dash_Front"、S → "Dash_Back"、A → "Dash_Left"、D → "Dash_Right"
    /// </summary>
    private void TriggerDashAnimation(KeyCode key)
    {
        if (targetAnimator == null) return;

        string triggerName = key switch
        {
            KeyCode.W => "Dash_Front",
            KeyCode.S => "Dash_Back",
            KeyCode.A => "Dash_Left",
            KeyCode.D => "Dash_Right",
            _ => null
        };

        if (!string.IsNullOrEmpty(triggerName))
            targetAnimator.SetTrigger(triggerName);
    }

    private IEnumerator DashWithEffects(Vector3 startPos, Vector3 targetPos)
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
                urpMotionBlur.intensity.value = Mathf.Lerp(originalBlurIntensity, blurIntensity, t);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(targetPos);
        playerCamera.fieldOfView = originalFOV + fovIncrease;
        if (urpMotionBlur != null) urpMotionBlur.intensity.value = blurIntensity;

        if (disableCollisions) rb.detectCollisions = origColl;

        // 還原 FOV & Motion Blur
        elapsed = 0f;
        while (elapsed < blurRecoverTime)
        {
            float t = elapsed / blurRecoverTime;
            playerCamera.fieldOfView = Mathf.Lerp(originalFOV + fovIncrease, originalFOV, t);
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
