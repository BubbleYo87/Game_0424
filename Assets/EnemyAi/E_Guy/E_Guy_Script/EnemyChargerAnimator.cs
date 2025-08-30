using UnityEngine;

/// <summary>
/// 專責「讀取 Enemy_Charger 的狀態/事件 → 餵給 Animator」
/// 放在 Model 子物件（有 Animator 的那個物件）
/// </summary>
[RequireComponent(typeof(Animator))]
public class EnemyChargerAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root 上的 Enemy_Charger（或任何實作 IEnemyChargerProvider 的腳本）")]
    public MonoBehaviour providerSource; // 指到 Enemy_Charger
    private IEnemyChargerProvider provider; // 只讀介面
    private Animator animator;

    [Header("Animator Parameters（需與控制器一致）")]
    public string pSpeed = "Speed";            // float
    public string pIsChasing = "IsChasing";    // bool
    public string pIsSearching = "IsSearching";// bool（可選）
    public string pDash = "Dash";              // bool
    public string tBreathe = "Breathe";        // trigger
    public string tFaint = "Faint";        // trigger
    public string tHit = "Hit";                // trigger
    public string pRage = "Rage";              // float 0~1（可選）
    public string pAware = "Aware";            // float 0~1（可選）

    [Header("Dash 動畫速度控制")]
    [Tooltip("勾選：直接改 Animator.speed；取消：改用參數 (pDashSpeedMult)")]
    public bool controlByGlobalAnimatorSpeed = true;

    [Tooltip("參數模式：Animator 的 float 參數名（請在 Dash clip 的 SpeedMultiplier 綁它）")]
    public string pDashSpeedMult = "DashSpeedMult";

    [Range(0.05f, 1f)] public float minAnimSpeed = 0.3f;

    private float _animBaseSpeed = 1f;


    private Enemy_Charger charger; // 為了訂閱事件（非必要操作其行為）

    private void Awake()
    {
        animator = GetComponent<Animator>();
        charger = providerSource as Enemy_Charger;
        provider = providerSource as IEnemyChargerProvider;

        if (provider == null)
            Debug.LogError("[EnemyChargerAnimator] providerSource 沒有實作 IEnemyChargerProvider，請指到 Enemy_Charger");

        if (animator) _animBaseSpeed = animator.speed; 
    }

    private void OnEnable()
    {
        if (charger != null)
        {
            charger.OnStateChanged += HandleStateChanged;
            charger.OnRageChanged += HandleRageChanged;
            charger.OnDashStarted += HandleDashStarted;
            charger.OnDashEnded   += HandleDashEnded;
            charger.OnHitReact    += HandleHitReact;

            // ★ 新增：接收動畫倍率
            charger.OnDashAnimSpeedChanged += HandleDashAnimSpeedChanged;
        }
    }

    private void OnDisable()
    {
        if (charger != null)
        {
            charger.OnStateChanged -= HandleStateChanged;
            charger.OnRageChanged  -= HandleRageChanged;
            charger.OnDashStarted  -= HandleDashStarted;
            charger.OnDashEnded    -= HandleDashEnded;
            charger.OnHitReact     -= HandleHitReact;

            // ★ 新增：退訂
            charger.OnDashAnimSpeedChanged -= HandleDashAnimSpeedChanged;
        }
    }

    private void Update()
    {
        if (provider == null) return;

        // 連續量：速度 / 怒氣 / 察覺（可選）
/*         animator.SetFloat(pSpeed, provider.Speed);
        if (!string.IsNullOrEmpty(pRage))  animator.SetFloat(pRage,  provider.RagePercent);
        if (!string.IsNullOrEmpty(pAware)) animator.SetFloat(pAware, provider.Awareness01); */
    }

    // === 事件對應 ===
    private void HandleStateChanged(string state)
    {
        // 只開必要的布林位（其餘關閉）
        bool chasing  = state == "Chase" || state == "Dash" || state == "Breathe" || state == "Faint"; // 追逐期維持戰鬥移動 Blend
        bool searching = state == "Search";

        animator.SetBool(pIsChasing, chasing);
        if (!string.IsNullOrEmpty(pIsSearching))
            animator.SetBool(pIsSearching, searching);

        if (state == "Breathe" && !string.IsNullOrEmpty(tBreathe))
            animator.SetTrigger(tBreathe);
        if (state == "Faint" && !string.IsNullOrEmpty(tFaint))
        animator.SetTrigger(tFaint);
    }

    private void HandleRageChanged(float current, float max)
    {
        // 已在 Update() 餵 pRage，這裡可做門檻特效或表情
        // 例如：當怒氣 > 0.8 時開啟臉部表情 Layer（這裡略）
    }

    private void HandleDashStarted()
    {
        if (!string.IsNullOrEmpty(pDash))
            animator.SetBool(pDash, true);
    }

    private void HandleDashEnded(bool hitPlayer)
    {
        animator.SetBool(pDash, false);
        // 可根據是否命中切不同收招（若 Animator 需要）
        // 例如：animator.SetBool("DashHit", hitPlayer);
    }

    private void HandleHitReact()
    {
        if (!string.IsNullOrEmpty(tHit))
            animator.SetTrigger(tHit);
    }

    private void HandleDashAnimSpeedChanged(float mult)
    {
        if (!animator) return;

        float animMult = Mathf.Clamp(mult, minAnimSpeed, 1f);

        if (controlByGlobalAnimatorSpeed)
        {
            // 全域：直接改 Animator.speed
            animator.speed = _animBaseSpeed * animMult;
        }
        else
        {
            // 參數：請在 Animator 內將 Dash clip 的 SpeedMultiplier 綁到 pDashSpeedMult
            if (!string.IsNullOrEmpty(pDashSpeedMult))
                animator.SetFloat(pDashSpeedMult, animMult);
        }
    }

}
