// DoubleTapDash.cs (Safe Sweep Version)
// - 逐步掃掠移動，避免穿牆
// - 不關閉碰撞（維持剛體連續碰撞模式）
// - 起跑太貼牆直接取消 Dash
// - FOV / Motion Blur 視覺效果保留
// - 四方向 Animator Trigger：Dash_Front / Dash_Back / Dash_Left / Dash_Right

using UnityEngine;
using UnityEngine.Rendering;                    // URP Post-processing
using UnityEngine.Rendering.Universal;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class DoubleTapDash : MonoBehaviour
{
    [Header("攝影機與 URP 後處理")]
    [Tooltip("用來判定方向並修改 FOV 的攝影機")]
    public Camera playerCamera;
    [Tooltip("掛載了 Motion Blur Override 的 Global Volume")]
    public Volume postProcessVolume;

    [Header("連擊判定")]
    [Tooltip("兩次按鍵間隔的最大時間（秒）")]
    public float doubleTapTime = 0.5f;

    [Header("Dash 設定")]
    [Tooltip("Dash 持續時間（秒）——用來分段位移與特效插值")]
    public float dashDuration = 0.2f;
    [Tooltip("最大 Dash 距離（世界單位）")]
    public float dashDistance = 5f;
    [Tooltip("障礙檢測預留距離（與牆面保持的安全邊界）")]
    public float collisionOffset = 0.12f;
    [Tooltip("啟動時若與前方牆面小於此距離，直接取消 Dash")]
    public float startClearance = 0.15f;
    [Tooltip("Dash 冷卻時間（秒）")]
    public float dashCooldown = 0.5f;

    [Header("高度偏移（可選）")]
    [Tooltip("Dash 結束後再以極短時間補上的 Y 偏移（避免用上抬偷越障礙）")]
    public float heightOffset = 0f;

    [Header("速度視覺效果")]
    [Tooltip("衝刺時 FOV 增量")]
    public float fovIncrease = 5f;
    [Tooltip("Dash 時 Motion Blur 強度 (0~1)")]
    public float blurIntensity = 1f;
    [Tooltip("Dash 結束後模糊還原時間")]
    public float blurRecoverTime = 0.1f;

    [Header("外部 Animator 參考")]
    [Tooltip("用來播放 Dash 動畫的 Animator")]
    public Animator targetAnimator;

    [Header("除錯/偵錯")]
    [Tooltip("在 Scene 模式下以 Gizmos 畫出 Sweep")]
    public bool debugGizmos = false;


    // 私有欄位
    public PlayerMovement pm;       // 外部玩家移動控制（若有需要可接 dashing 狀態）
    public Rigidbody rb;

    private bool isDashing = false;
    private KeyCode lastTapKey = KeyCode.None;
    private float lastTapTimeStamp;
    private float originalFOV;
    private float lastDashTime = -999f;

    // URP Motion Blur
    private MotionBlur urpMotionBlur;
    private float originalBlurIntensity;

    // Gizmos 暫存
    private Vector3 gizLastFrom, gizLastTo;
    private bool gizDraw = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // ★重要：連續碰撞，降低穿透

        if (playerCamera == null)
            playerCamera = Camera.main;
        if (playerCamera == null)
            Debug.LogError("DoubleTapDash：找不到 Camera，請在 Inspector 指定！");

        originalFOV = playerCamera != null ? playerCamera.fieldOfView : 60f;

        // 取得 URP Volume 裡的 MotionBlur Override
        if (postProcessVolume != null && postProcessVolume.profile != null &&
            postProcessVolume.profile.TryGet<MotionBlur>(out urpMotionBlur))
        {
            originalBlurIntensity = urpMotionBlur.intensity.value;
            urpMotionBlur.active = false;
        }
        else
        {
            Debug.LogWarning("DoubleTapDash：找不到 MotionBlur Override，請在 Volume Profile 裡加入並打勾它。");
        }

        if (targetAnimator == null)
            Debug.LogWarning("DoubleTapDash：目標 Animator (targetAnimator) 未指定，將無法觸發 Dash 動畫。");

        // 若外部沒拖 pm，可嘗試在同物件或父物件自動抓
        if (pm == null) pm = GetComponent<PlayerMovement>();
        if (pm == null) pm = GetComponentInParent<PlayerMovement>();
    }

    void Update()
    {
        if (isDashing) return;
        if (playerCamera == null || rb == null) return;

        // 冷卻檢查
        if (Time.time - lastDashTime < dashCooldown) return;

        TryCheckDoubleTap(KeyCode.W);
        TryCheckDoubleTap(KeyCode.S);
        TryCheckDoubleTap(KeyCode.A);
        TryCheckDoubleTap(KeyCode.D);
    }

    private void TryCheckDoubleTap(KeyCode key)
    {
        if (!Input.GetKeyDown(key)) return;

        float elapsed = Time.time - lastTapTimeStamp;
        if (lastTapKey == key && elapsed <= doubleTapTime)
        {
            // 計算攝影機的水平前/右向量
            Vector3 camF = playerCamera.transform.forward; camF.y = 0; camF.Normalize();
            Vector3 camR = playerCamera.transform.right;   camR.y = 0; camR.Normalize();
            Vector3 dir = key == KeyCode.W ? camF
                        : key == KeyCode.S ? -camF
                        : key == KeyCode.A ? -camR
                        : camR;

            // 先做「貼牆安全檢查」：起跑若太貼牆，直接取消 Dash
            if (TooCloseToWall(dir, startClearance))
            {
                lastTapKey = KeyCode.None;
                return;
            }

            // 前向障礙偵測：算出最大可 Dash 距離（以安全邊界 collisionOffset 作為緩衝）
            float actualDist = ComputeMaxDashDistance(dir, dashDistance, collisionOffset);
            if (actualDist <= 0f)
            {
                // 離牆過近或前方立即命中 → 取消
                lastTapKey = KeyCode.None;
                return;
            }

            // 觸發對應 Animator Trigger
            TriggerDashAnimation(key);

            // 計算起點與水平終點（垂直位移稍後再補）
            Vector3 startPos = rb.position;
            Vector3 targetPos = startPos + dir * actualDist;

            lastDashTime = Time.time;
            StartCoroutine(DashWithEffects_SafeSweep(startPos, targetPos, heightOffset));

            lastTapKey = KeyCode.None;
        }
        else
        {
            lastTapKey = key;
            lastTapTimeStamp = Time.time;
        }
    }

    /// <summary>
    /// 根據按鍵觸發對應的 Dash 動畫 Trigger
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

    /// <summary>
    /// 計算最大可 Dash 距離：用剛體 SweepTest 掃掠，
    /// 若命中則扣掉 collisionOffset 安全距，避免緊貼牆面。
    /// </summary>
    private float ComputeMaxDashDistance(Vector3 dir, float maxDist, float safeOffset)
    {
        if (rb.SweepTest(dir, out RaycastHit hit, maxDist + safeOffset))
        {
            float d = Mathf.Max(0f, hit.distance - safeOffset);
            return d;
        }
        return maxDist;
    }

    /// <summary>
    /// 起跑「貼牆過近」的檢查：若一小步（startClearance）內就命中，視為太近。
    /// </summary>
    private bool TooCloseToWall(Vector3 dir, float minClearance)
    {
        if (minClearance <= 0f) return false;
        return rb.SweepTest(dir, out _, minClearance);
    }

    /// <summary>
    /// 逐步掃掠的安全 Dash：
    /// - 將 dashDuration 均分成數個 FixedUpdate 小步
    /// - 每步先用 SweepTest 檢查，命中就停在牆前（扣安全距）
    /// - 完成水平段後，再用極短時間補上 heightOffset 的垂直位移
    /// - 全程不關閉碰撞
    /// </summary>
    private IEnumerator DashWithEffects_SafeSweep(Vector3 startPos, Vector3 flatTarget, float finalHeightOffset)
    {
        isDashing = true;
        if (pm != null) pm.dashing = true;

        // 開啟 Motion Blur
        if (urpMotionBlur != null) urpMotionBlur.active = true;

        // 僅做水平位移（以剛體當前 Y 高度為準）
        Vector3 flatStart = new Vector3(startPos.x, rb.position.y, startPos.z);
        Vector3 flatEnd   = new Vector3(flatTarget.x, rb.position.y, flatTarget.z);
        Vector3 total     = flatEnd - flatStart;

        int steps = Mathf.Max(1, Mathf.CeilToInt(dashDuration / Time.fixedDeltaTime));
        Vector3 stepDelta = total / steps;

        float elapsed = 0f;
        float safeOffset = Mathf.Max(0.03f, collisionOffset);

        for (int i = 0; i < steps; i++)
        {
            Vector3 dir = stepDelta.normalized;
            float stepLen = stepDelta.magnitude;

            // 每一小步之前做 SweepTest：若命中，停在牆前 safeOffset
            if (stepLen > 0f && rb.SweepTest(dir, out RaycastHit hit, stepLen + safeOffset))
            {
                float move = Mathf.Max(0f, hit.distance - safeOffset);
                if (move > 0f)
                {
                    Vector3 from = rb.position;
                    Vector3 to = from + dir * move;
                    rb.MovePosition(to);

                    if (debugGizmos) { gizLastFrom = from; gizLastTo = to; gizDraw = true; }
                }
                // 命中即中止水平段
                break;
            }
            else
            {
                Vector3 from = rb.position;
                Vector3 to = from + stepDelta;
                rb.MovePosition(to);

                if (debugGizmos) { gizLastFrom = from; gizLastTo = to; gizDraw = true; }
            }

            // 視覺效果插值（FOV / Blur）
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / dashDuration);
            if (playerCamera != null)
                playerCamera.fieldOfView = Mathf.Lerp(originalFOV, originalFOV + fovIncrease, t);
            if (urpMotionBlur != null)
                urpMotionBlur.intensity.value = Mathf.Lerp(originalBlurIntensity, blurIntensity, t);

            yield return new WaitForFixedUpdate();
        }

        // 補「極短時間的垂直位移」，避免上抬越障
        if (Mathf.Abs(finalHeightOffset) > 0.0001f)
        {
            float liftTime = 0.04f;   // 很短
            float t2 = 0f;
            Vector3 startLift = rb.position;
            Vector3 endLift   = startLift + Vector3.up * finalHeightOffset;

            while (t2 < liftTime)
            {
                float nt = t2 / liftTime;
                Vector3 target = Vector3.Lerp(startLift, endLift, nt);

                // 向上也做安全 Sweep（避免穿頂）
                Vector3 up = Vector3.up;
                Vector3 delta = target - rb.position;
                float upLen = delta.magnitude;

                if (upLen > 0f && rb.SweepTest(up, out RaycastHit upHit, upLen + safeOffset))
                {
                    float mv = Mathf.Max(0f, upHit.distance - safeOffset);
                    if (mv > 0f) rb.MovePosition(rb.position + up * mv);
                    break; // 頂到天花板就停止抬升
                }
                else
                {
                    rb.MovePosition(target);
                }

                t2 += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        // 視覺效果收尾：還原 FOV / Blur
        float recoverElapsed = 0f;
        while (recoverElapsed < blurRecoverTime)
        {
            float t = blurRecoverTime > 0f ? (recoverElapsed / blurRecoverTime) : 1f;
            if (playerCamera != null)
                playerCamera.fieldOfView = Mathf.Lerp(originalFOV + fovIncrease, originalFOV, t);
            if (urpMotionBlur != null)
                urpMotionBlur.intensity.value = Mathf.Lerp(blurIntensity, originalBlurIntensity, t);

            recoverElapsed += Time.deltaTime;
            yield return null;
        }

        if (playerCamera != null) playerCamera.fieldOfView = originalFOV;
        if (urpMotionBlur != null)
        {
            urpMotionBlur.intensity.value = originalBlurIntensity;
            urpMotionBlur.active = false;
        }

        isDashing = false;
        if (pm != null) pm.dashing = false;
    }

    // Scene 視覺化（選用）
    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos || !gizDraw) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(gizLastFrom, gizLastTo);
        Gizmos.DrawSphere(gizLastTo, 0.04f);
        gizDraw = false;
    }
}
