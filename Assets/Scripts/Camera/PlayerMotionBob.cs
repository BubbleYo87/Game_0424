using UnityEngine;
using System;

/// <summary>
/// 走路/跑步晃動（Bob）— 掛在「Player」上，讀取 Player 的 Rigidbody 速度，
/// 再把晃動套用到指定的 bobTarget（通常是武器或相機下的一個 Pivot）
/// 可選：讀取 ProjectileGun.IsAiming，在 ADS 時縮小晃動；提供腳步事件。
/// </summary>
[DefaultExecutionOrder(70)] // 比一般移動/相機稍晚，避免撕裂
public class PlayerMotionBob : MonoBehaviour
{
    [Header("參考/輸入")]
    [Tooltip("玩家的剛體（不指定會自動抓本物件上的 Rigidbody）")]
    public Rigidbody playerRb;
    [Tooltip("晃動要套用到哪個物件（通常是 WeaponRoot 下的 BobPivot，或 Camera 下的某個 Pivot）")]
    public Transform bobTarget;

    [Tooltip("（可選）若有射擊腳本，可讀取 IsAiming 讓 ADS 時縮小晃動")]
    public ProjectileGun gun;                 // 自動往子物件找（如武器掛在玩家下面）
    [Tooltip("若找不到 gun，是否自動在子孫物件搜尋一次")]
    public bool autoFindGunInChildren = true;

    [Header("基準姿勢（啟動時自動記錄）")]
    public Vector3 baseLocalPos;
    public Vector3 baseLocalEuler;

    [Header("頻率（次/秒）")]
    public float idleFrequency = 0.9f;       // 待機呼吸
    public float walkFrequency = 1.8f;       // 行走
    public float runFrequency  = 2.6f;       // 跑步

    [Header("位移幅度（公尺）")]
    public Vector2 idleAmplitude = new Vector2(0.0015f, 0.0035f); // X 左右 / Y 上下
    public Vector2 walkAmplitude = new Vector2(0.015f, 0.020f);
    public Vector2 runAmplitude  = new Vector2(0.020f, 0.030f);

    [Header("旋轉幅度（Roll，度）")]
    public float idleRoll = 0.4f;
    public float walkRoll = 1.0f;
    public float runRoll  = 2.0f;

    [Header("速度門檻（m/s）")]
    [Tooltip("低於此速度視為靜止（呼吸）")]
    public float stopThreshold = 0.05f;
    [Tooltip("超過此水平速度視為跑步（幅度/頻率向跑步插值）")]
    public float runSpeed = 4.5f;

/*     [Header("ADS 衰減（瞄準時縮小晃動）")]
    [Range(0f, 1f)] public float adsMultiplier = 0.35f; */

    [Header("平滑")]
    [Tooltip("位置插值速度（越大越跟手）")]
    public float posSmooth = 12f;
    [Tooltip("旋轉插值速度")]
    public float rotSmooth = 12f;

    [Header("額外倍率（可由外部狀態調整，例如蹲下）")]
    [Tooltip("0~1：全域縮放本效果；可用 SetExtraMultiplier() 寫入")]
    [Range(0f, 2f)] public float extraMultiplier = 1f;

    // 事件：腳步（可接腳步聲/震動）
    public event Action OnFootstepLeft;
    public event Action OnFootstepRight;

    // 內部狀態
    private Vector3 targetLocalPos;
    private Quaternion targetLocalRot;
    private Vector3 velPos;             // SmoothDamp 速度
    private float phase = 0f;           // 位相
    private int lastStepSide = 0;       // -1 左 / +1 右 / 0 未定
    private Vector3 lastRootPos;        // 位置差估速（若沒剛體時候備用）
    private bool hasLastPos = false;

    void Awake()
    {
        if (!playerRb) playerRb = GetComponent<Rigidbody>();
        if (!bobTarget)
            Debug.LogWarning("[PlayerMotionBob] bobTarget 未指定：請拖一個 Pivot（例如武器或相機下的空物件）。");

        if (!gun && autoFindGunInChildren)
            gun = GetComponentInChildren<ProjectileGun>(true);

        if (bobTarget)
        {
            baseLocalPos = bobTarget.localPosition;
            baseLocalEuler = bobTarget.localEulerAngles;
            targetLocalPos = baseLocalPos;
            targetLocalRot = Quaternion.Euler(baseLocalEuler);
        }
    }

    void LateUpdate()
    {
        if (!bobTarget) return;

        // 1) 取得玩家「水平速度」
        Vector3 v = Vector3.zero;
        if (playerRb)
        {
            v = playerRb.velocity;
        }
        else
        {
            // 無剛體就用位置差估算
            if (!hasLastPos) { lastRootPos = transform.position; hasLastPos = true; }
            Vector3 delta = (transform.position - lastRootPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastRootPos = transform.position;
            v = delta;
        }
        float speed = new Vector3(v.x, 0f, v.z).magnitude;

        // 2) 判斷狀態：Idle / Walk / Run（用線性插值過度）
        bool moving = speed > stopThreshold;
        float tRun = Mathf.Clamp01((speed - stopThreshold) / Mathf.Max(0.0001f, runSpeed - stopThreshold));

        float freq = moving ? Mathf.Lerp(walkFrequency, runFrequency, tRun) : idleFrequency;
        Vector2 amp = moving ? Vector2.Lerp(walkAmplitude, runAmplitude, tRun) : idleAmplitude;
        float rollAmp = moving ? Mathf.Lerp(walkRoll, runRoll, tRun) : idleRoll;

        // 3) ADS 衰減 + 其他倍率
        /* bool aiming = gun ? gun.IsAiming : false;
        float mult = (aiming ? adsMultiplier : 1f) * Mathf.Max(0f, extraMultiplier);
        amp *= mult;
        rollAmp *= mult; */

        // 4) 位相推進（依頻率）
        phase += freq * Mathf.PI * 2f * Time.deltaTime;
        if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;

        // 5) 典型 Bob 曲線：x=sin(φ)，y=(1-cos(2φ))/2
        float x = Mathf.Sin(phase) * amp.x;                 // 左右
        float y = (1f - Mathf.Cos(phase * 2f)) * 0.5f * amp.y; // 上下（總是 >=0 的彈跳感）
        float roll = Mathf.Sin(phase) * rollAmp;            // 翻滾

        Vector3 posOffset = new Vector3(x, -y, 0f);         // 習慣往下為正，取負
        targetLocalPos = baseLocalPos + posOffset;
        targetLocalRot = Quaternion.Euler(baseLocalEuler + new Vector3(0f, 0f, roll));

        // 6) 平滑插值
        bobTarget.localPosition = Vector3.SmoothDamp(
            bobTarget.localPosition, targetLocalPos, ref velPos, 1f / Mathf.Max(0.0001f, posSmooth));
        bobTarget.localRotation = Quaternion.Slerp(
            bobTarget.localRotation, targetLocalRot, Time.unscaledDeltaTime * rotSmooth);

        // 7) 腳步事件（左右交替）：速度夠快才觸發，避免站立時抖動觸發
        if (moving && speed > (runSpeed * 0.25f))
        {
            float s = Mathf.Sin(phase);
            int side = s >= 0f ? +1 : -1;
            if (side != lastStepSide)
            {
                // φ 穿越 0 或 π → 換腳
                if (side > 0) OnFootstepRight?.Invoke();
                else OnFootstepLeft?.Invoke();
                lastStepSide = side;
            }
        }
        else
        {
            lastStepSide = 0;
        }
    }

    /// <summary>外部可設定額外倍率（例如蹲下 0.6、受傷 0.3）</summary>
    public void SetExtraMultiplier(float m) => extraMultiplier = m;

    /// <summary>切槍/重置時可呼叫，瞬間回到基準姿勢</summary>
    public void SnapToBasePose()
    {
        if (!bobTarget) return;
        bobTarget.localPosition = baseLocalPos;
        bobTarget.localRotation = Quaternion.Euler(baseLocalEuler);
        targetLocalPos = baseLocalPos;
        targetLocalRot = Quaternion.Euler(baseLocalEuler);
        velPos = Vector3.zero;
    }
}
