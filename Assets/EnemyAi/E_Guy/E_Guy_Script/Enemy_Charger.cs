using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 追逐 →（怒氣滿 / 受擊達標）→ 衝刺 → 喘息 → 回到追逐
/// 將此腳本掛在敵人本體（需含 NavMeshAgent、//Animator）。
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class Enemy_Charger : MonoBehaviour, IEnemyChargerProvider
{
    // ========================= Inspector 區 =========================
    [Header("References / 參考物件")]
    [Tooltip("玩家 Transform")]
    public Transform player;
    [Tooltip("可選：提供地圖障礙物的 Layer，用於直線檢查（避免亂衝）")]
    public LayerMask obstacleMask;
    // --- 在 Enemy_Charger 類別外面宣告一個只讀介面（可放同檔頂部）---
    public interface IEnemyChargerProvider
    {
        float Speed { get; }             // 目前速度（給動畫 Blend）
        float RagePercent { get; }       // 怒氣 0~1（可綁 UI/特效）
        float Awareness01 { get; }       // 察覺 0~1（可綁表情/警戒動畫）
        Transform RootTransform { get; } // 根節點（看向/朝向用）
        string CurrentStateName { get; } // 目前狀態名稱（簡單字串）
    }

    
    [Header("移動參數")]
    [Tooltip("一般追逐時的移動速度")]
    public float chaseSpeed = 3.5f;
    [Tooltip("旋轉速度（NavMeshAgent.angularSpeed）")]
    public float angularSpeed = 360f;
    [Tooltip("停止距離（不會貼太近）")]
    public float stoppingDistance = 1.2f;

    [Header("視野 / 感知")]
    [Tooltip("可見半徑（視覺可感知的最大距離）")]
    public float visionRadius = 14f;

    [Tooltip("視野總角度（例：120 表示前方 ±60°）")]
    [Range(30f, 360f)] public float viewAngle = 120f;

    [Tooltip("周邊近距離半徑（即使在視野外，貼身也會感知）")]
    public float peripheralRadius = 1.8f;

    [Tooltip("用於視線遮擋（牆/障礙）的 Layer")]
    public LayerMask visionObstacleMask;

    [Header("察覺槽（0~1）")]
    [Tooltip("察覺上升速率/秒（完全在視野內且無遮擋時）")]
    public float awarenessGainPerSec = 0.7f;

    [Tooltip("在周邊近距離但不在視野內的上升速率/秒")]
    public float peripheralGainPerSec = 0.25f;

    [Tooltip("看不到目標時的衰減速率/秒")]
    public float awarenessDecayPerSec = 0.35f;

    [Tooltip("進入追逐門檻")]
    [Range(0f,1f)] public float awareThreshold = 0.6f;

    [Tooltip("掉出追逐門檻（遲滯，低於此值才算失去）")]
    [Range(0f,1f)] public float loseThreshold = 0.35f;

    [Header("搜尋狀態")]
    [Tooltip("進入搜尋後，最多搜尋幾秒")]
    public float searchDuration = 4f;

    [Tooltip("在搜尋點的左右掃視角度（相對當前前方）")]
    public float searchLookAngle = 60f;

    [Tooltip("在搜尋點的掃視速度（度/秒）")]
    public float searchLookSpeed = 240f;


    [Header("偵測 / 追逐條件")]
    [Tooltip("發現玩家的半徑；超出此範圍回到待機")]
    public float detectRadius = 15f;
    [Tooltip("若距離過遠，怒氣累積會加速")]
    public float farDistance = 12f;
    [Tooltip("若距離過近，怒氣累積會變慢")]
    public float nearDistance = 3f;

    [Header("怒氣系統（被動累積觸發）")]
    [Tooltip("怒氣上限，達到即觸發衝刺")]
    public float rageMax = 100f;
    [Tooltip("追逐狀態下每秒基礎累積")]
    public float rageGainPerSecond = 5f;
    [Tooltip("玩家距離過遠時，額外每秒累積")]
    public float rageExtraWhenFar = 10f;
    [Tooltip("玩家距離過近時，每秒累積變為此值（通常小於基礎累積）")]
    public float rageNearPerSecond = 2f;
    [Tooltip("觸發衝刺後是否清空怒氣")]
    public bool clearRageAfterDash = true;

    [Header("受擊觸發（主動暴走）")]
    [Tooltip("累積受到幾次攻擊就強制觸發衝刺")]
    public int hitsToEnrage = 3;
    [Tooltip("進入暴走衝刺時，是否同時清零受擊計數")]
    public bool resetHitCountOnEnrage = true;

    [Header("衝刺參數")]
    [Tooltip("衝刺速度（可暫時覆蓋 NavMeshAgent.speed）")]
    public float dashSpeed = 12f;
    [Tooltip("衝刺持續時間（秒）")]
    public float dashDuration = 1.0f;
    [Tooltip("與玩家的直線距離需 ≥ 此值才允許衝刺（避免近距離無意義衝刺）")]
    public float minDashDistance = 4f;
    [Tooltip("衝刺命中玩家時造成的傷害數值")]
    public float dashDamage = 30f;
    [Tooltip("衝刺期間的玩家圖層（用於簡易命中檢測）")]
    public LayerMask playerMask;
    [Tooltip("命中判定半徑（以敵人位置為中心圈出）")]
    public float hitRadius = 1.2f;
    [Tooltip("是否需要直線無障礙才能衝刺")]
    public bool requireClearLineForDash = true;

    [Header("直線衝刺（鎖定起手方向）")]
    [Tooltip("起手時鎖定玩家方向，衝刺全程沿該方向直線推進")]
    public bool dashLockDirectionOnStart = true;

    [Tooltip("衝刺最大距離（單位：公尺），若 > 0 則以距離為止；否則由 dashDuration 決定")]
    public float dashMaxDistance = 0f;

    [Tooltip("衝刺起手時是否朝鎖定方向瞬間面向")]
    public bool faceDashDirectionOnStart = true;

    [Tooltip("是否在前方有障礙物時，將衝刺終點縮短到撞點前")]
    public bool stopBeforeObstacle = true;

    [Tooltip("檢測障礙物的 Layer（建議與 Obstacle 一致）")]
    public LayerMask dashObstacleMask;

    [Tooltip("命中到障礙物或玩家時，是否立刻終止衝刺")]
    public bool endDashOnHitAnything = true;
    public bool hitPlayer = false;

    [Header("Dash 減速 / Ease-out")]
    public bool enableApproachSlowdown = true;     // 開關
    [Tooltip("距離終點這段距離內開始減速")]
    public float slowDownDistance = 2.0f;          // 例：2 公尺內開始放慢
    [Tooltip("到終點時的最低速度倍數（相對 dashSpeed）")]
    [Range(0f, 1f)] public float endSpeedMultiplier = 0.2f; // 0.2 = 只剩 20% 速度

    [Tooltip("（可選）用曲線控制 0→1 進度對應的速度倍數")]
    public bool useSlowdownCurve = false;
    public AnimationCurve slowdownCurve = AnimationCurve.EaseInOut(0, 1, 1, 0.2f);
    // 曲線說明：X=衝刺進度(0~1)，Y=速度倍數(0~1)。預設開頭快、末端慢。




    [Header("喘息（硬直）")]
    [Tooltip("衝刺命中玩家後的喘息時間（較短）")]
    public float breathOnHit = 1.0f;
    [Tooltip("衝刺落空或撞牆後的喘息時間（較長）")]
    public float breathOnMiss = 3.0f;

    [Header("冷卻時間 / 節奏控制")]
    [Tooltip("衝刺結束到下一次允許衝刺的最短間隔")]
    public float dashCooldown = 4.0f;

    [Header("動畫參數名稱（需與 Animator 對應）")]
    public string animParamSpeed = "Speed";
    public string animParamIsChasing = "IsChasing";
    public string animTriggerDash = "Dash";
    public string animTriggerBreathe = "Breathe";
    public string animTriggerHitReact = "Hit";

    // ========================= 內部狀態 =========================
    private NavMeshAgent agent;
    /* private //Animator //animator; */

    private enum State { Idle, Chase, Dash, Breathe, Faint , Search }
    private State state = State.Idle;

    private float rage;          // 當前怒氣
    private int hitCount;        // 受擊累積
    private float lastDashTime;  // 上次衝刺結束時間（用於冷卻）
    private bool isDashing;      // 保護旗標（避免重複進入）

    // 察覺與搜尋
    [SerializeField] private float awareness01;    // 0~1
    private Vector3 lastKnownPosition;
    private bool hasLineOfSight; // 供除錯觀察

    // 可選：事件（讓 UI 或音效訂閱）
    public System.Action<float, float> OnRageChanged; // (current, max)
    public System.Action OnDashStarted;
    public System.Action<bool> OnDashEnded;           // 命中？true: hit / false: miss

    // ★ 新增：Dash 動畫速度倍率（0~1）
    public System.Action<float> OnDashAnimSpeedChanged;

    // === IEnemyChargerProvider 介面實作 ===
    public float Speed => agent ? agent.velocity.magnitude : 0f;
    public float RagePercent => Mathf.Clamp01(rage / rageMax);
    public float Awareness01 => Mathf.Clamp01(awareness01);
    public Transform RootTransform => transform;
    public string CurrentStateName => state.ToString();

    // ========================= Unity 生命週期 =========================
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        //animator = GetComponent<//Animator>();

        agent.stoppingDistance = stoppingDistance;
        agent.speed = chaseSpeed;
        agent.angularSpeed = angularSpeed;
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

    }

    private void Start()
    {
        SetState(State.Idle);
    }

    private void Update()
    {
        // 先更新視覺/察覺
        TickSenses();

        //animator.SetFloat(animParamSpeed, agent.velocity.magnitude);

        switch (state)
        {
            case State.Idle:    TickIdle();   break;
            case State.Chase:   TickChase();  break;
            case State.Dash:                   break;
            case State.Breathe:                break;
            case State.Faint:                break;
            case State.Search:  TickSearch(); break; // 下面會新增 Search 狀態
        }
    }

    // === 動畫層可訂閱的事件（呈現用） ===
    public System.Action<string> OnStateChanged; // 參數會傳 "Idle/Chase/Dash/Breathe/Search"
    public System.Action OnHitReact;             // 受擊動畫
    // ========================= 狀態機本體 =========================
    private void SetState(State next)
    {
        // 離開時清理
        if (state == State.Search) ExitSearch();
        state = next;

        // 可做一些進入狀態時的統一處理
        switch (state)
        {
            case State.Idle:
                //animator.SetBool(animParamIsChasing, false);
                agent.isStopped = true;
                break;

            case State.Chase:
                //animator.SetBool(animParamIsChasing, true);
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                break;

            case State.Dash:
                // 由 StartCoroutine(DashRoutine()) 負責
                break;

            case State.Breathe:
                // 由 StartCoroutine(BreatheRoutine(x)) 負責
                break;
            case State.Faint:
            // 由 StartCoroutine(FaintRoutine(x)) 負責
            break;
            case State.Search:
                agent.isStopped = false;
                agent.speed = chaseSpeed * 0.8f; // 搜尋慢一點
                EnterSearch();           // <<< 加上這行
                break;
        }
        OnStateChanged?.Invoke(state.ToString()); // ← 通知動畫層
    }

    private void TickIdle()
    {
        if (awareness01 >= awareThreshold)
        {
            SetState(State.Chase);
            return;
        }

        if (!player) return;

        // 偵測玩家：進入追逐
        if (Vector3.Distance(transform.position, player.position) <= detectRadius)
        {
            SetState(State.Chase);
        }
    }

    private void TickChase()
    {
        if (!player) { SetState(State.Idle); return; }

        // 失去視線 → Search
        if (awareness01 < loseThreshold)
        {
            SetState(State.Search);
            return;
        }

        // 追逐路徑
        agent.SetDestination(player.position);

        // >>> 這行一定要有（你目前缺少）<<<
        float dist = Vector3.Distance(transform.position, player.position);

        // ---------- 怒氣累積 ----------
        float deltaRage = rageGainPerSecond * Time.deltaTime;
        if (dist >= farDistance)
            deltaRage += rageExtraWhenFar * Time.deltaTime;
        else if (dist <= nearDistance)
            deltaRage = rageNearPerSecond * Time.deltaTime;

        AddRage(deltaRage);

        // Debug（必要時註解）
        Debug.Log($"[Enemy Rage] {gameObject.name} : {rage}/{rageMax}");

        // ---------- 嘗試觸發衝刺 ----------
        bool rageReady = rage >= rageMax;
        bool enragedByHits = hitCount >= hitsToEnrage;
        bool cooldownReady = (Time.time - lastDashTime) >= dashCooldown;

        bool distanceOK = dist >= minDashDistance;
        bool pathClear = !requireClearLineForDash || HasClearLineToPlayer();

        if (!isDashing && cooldownReady && distanceOK && pathClear && (rageReady || enragedByHits))
        {
            if (clearRageAfterDash) rage = 0f;
            if (resetHitCountOnEnrage) hitCount = 0;
            StartCoroutine(DashRoutine());
        }
    }

    // =========================  =========================
    private void TickSenses()
    {
        if (!player)
        {
            awareness01 = Mathf.Max(0f, awareness01 - awarenessDecayPerSec * Time.deltaTime);
            return;
        }

        bool inFov  = InFOV(player, viewAngle, visionRadius);
        bool los    = inFov && HasLineOfSightTo(player);  // 只有在 FOV 內才檢查 LOS
        bool closePeripheral = !inFov && Vector3.Distance(transform.position, player.position) <= peripheralRadius;

        hasLineOfSight = los; // 給除錯看

        if (los)
        {
            // 正面可見：快速累積
            awareness01 = Mathf.Min(1f, awareness01 + awarenessGainPerSec * Time.deltaTime);
            lastKnownPosition = player.position;
        }
        else if (closePeripheral)
        {
            // 視野外但非常靠近：緩慢累積
            awareness01 = Mathf.Min(1f, awareness01 + peripheralGainPerSec * Time.deltaTime);
            lastKnownPosition = player.position; // 近距也算最後目擊
        }
        else
        {
            // 看不到：衰減
            awareness01 = Mathf.Max(0f, awareness01 - awarenessDecayPerSec * Time.deltaTime);
        }
    }

    private float searchTimer;
    private int searchPhase; // 0:前往; 1:掃視

    private void EnterSearch()
    {
        searchTimer = searchDuration;
        searchPhase = 0;
        if (lastKnownPosition != Vector3.zero)
            agent.SetDestination(lastKnownPosition);
    }
    private void ExitSearch() { /* 如需清理狀態在此 */ }

    private void TickSearch()
    {
        // 若在搜尋途中又重新看到玩家（awareness 回升），立刻回 Chase
        if (awareness01 >= awareThreshold)
        {
            SetState(State.Chase);
            return;
        }

        if (searchPhase == 0)
        {
            // 前往最後目擊點
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                searchPhase = 1; // 到點後進入掃視
            }
        }
        else
        {
            // 在原地掃視
            searchTimer -= Time.deltaTime;

            // 左右來回扭頭（簡單版）
            float t = Mathf.PingPong(Time.time * (searchLookSpeed / 60f), 1f) * 2f - 1f; // -1..1
            float yawOffset = t * searchLookAngle;
            Quaternion targetRot = Quaternion.Euler(0f, transform.eulerAngles.y + yawOffset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, searchLookSpeed * Time.deltaTime);

            if (searchTimer <= 0f)
            {
                // 搜尋失敗：回 Idle（或回巡邏）
                SetState(State.Idle);
            }
        }
    }

    // ========================= 衝刺 & 喘息 =========================
    private IEnumerator DashRoutine()
    {
        isDashing = true;
        SetState(State.Dash);

        // 觸發動畫（如需根動作，請另外調整控制邏輯）
        //animator.SetTrigger(animTriggerDash);

        // 暫停 AI 導航，由我們手動推進
        agent.isStopped = true;

        // 記錄並暫時覆蓋速度
        float originalSpeed = agent.speed;
        agent.speed = dashSpeed;

        // === 1) 起手「鎖定方向」 & 可選「瞬間面向」 ===
        // 方向鎖定：以「起手瞬間」的玩家相對方向為準
        Vector3 dashDir = (player ? (player.position - transform.position) : transform.forward);
        dashDir.y = 0f;
        if (dashDir.sqrMagnitude < 0.0001f) dashDir = transform.forward;  // 防守：重疊時取前方
        dashDir.Normalize();

        if (faceDashDirectionOnStart)
        {
            // 立即朝向鎖定方向（也可用 Slerp 平滑）
            transform.rotation = Quaternion.LookRotation(dashDir, Vector3.up);
        }

        // === 2) 計算終點（最大距離 or 由時間換算） ===
        float intendedDistance = (dashMaxDistance > 0f) ? dashMaxDistance : (dashSpeed * dashDuration);
        Vector3 dashEndPoint = transform.position + dashDir * intendedDistance;

        // 若需要「遇障縮短」
        if (stopBeforeObstacle)
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dashDir, out RaycastHit hit, intendedDistance, dashObstacleMask))
            {
                // 在碰撞點前留一點空隙，避免直接卡在牆上
                float safeOffset = 0.15f;
                dashEndPoint = hit.point - dashDir * safeOffset;
            }
        }

        // === 3) 衝刺主迴圈（沿鎖定方向直線前進，不再追著玩家轉向） ===
        float traveled = 0f;
        float maxTravel = Vector3.Distance(transform.position, dashEndPoint);
        hitPlayer = false;
        OnDashStarted?.Invoke();

        // 為了避免極端情況（如 NavMesh 邊界），額外做一次距離上限保護
        while (traveled < maxTravel)
        {
            // —— 距離式/曲線式減速 —— //
            float remaining = maxTravel - traveled;
            float currentSpeed = dashSpeed;

            if (enableApproachSlowdown && remaining > 0f)
            {
                // progress：0=起點，1=終點
                float progress = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.0001f, maxTravel));

                if (useSlowdownCurve && slowdownCurve != null)
                {
                    // 用曲線決定當前速度倍數（通常開頭接近1，末端接近 endSpeedMultiplier）
                    float mult = Mathf.Clamp01(slowdownCurve.Evaluate(progress));
                    // 為了確保「最慢不低於 endSpeedMultiplier」，再夾一下
                    mult = Mathf.Max(mult, endSpeedMultiplier);
                    currentSpeed = dashSpeed * mult;
                }
                else
                {
                    // 簡單距離式：在 slowDownDistance 內從 1 線性降到 endSpeedMultiplier
                    float t = Mathf.Clamp01(remaining / Mathf.Max(0.0001f, slowDownDistance)); // 近終點 t→0
                    float mult = Mathf.Lerp(endSpeedMultiplier, 1f, t);
                    currentSpeed = dashSpeed * mult;
                }
            }

            // ✅【新增】每幀都同步動畫速度倍率（不論是否啟用減速/是否用曲線）
            float speedMult = Mathf.Approximately(dashSpeed, 0f) ? 1f : Mathf.Clamp01(currentSpeed / dashSpeed);
            float animMult  = Mathf.Max(0.3f, speedMult); // 0.3 防卡幀的下限，可依手感調整
            OnDashAnimSpeedChanged?.Invoke(animMult);

            // 避免速度太小造成卡迴圈
            if (currentSpeed < 0.01f && remaining < 0.02f) break;

            // 計算當前步進距離
            float step = currentSpeed * Time.deltaTime;
            // 不要超過終點
            if (step > remaining) step = remaining;

            // 用 NavMeshAgent.Move 前進（不使用 SetDestination）
            Vector3 move = dashDir * step;
            agent.Move(move);
            traveled += step;

            // ✅ 命中玩家的檢測（維持你原本的 OverlapSphere 做法）
            Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, playerMask);
            if (hits != null && hits.Length > 0)
            {
                foreach (var h in hits)
                {
                    var dmg = h.GetComponentInParent<IDamageable>();
                    if (dmg != null) dmg.TakeDamage(dashDamage);
                }
                hitPlayer = true;

                if (endDashOnHitAnything)
                    break; // 命中玩家就結束衝刺
            }

            // ✅ 若啟用遇障終止（已經縮短終點通常不需要，但這裡給保險）
            if (endDashOnHitAnything && stopBeforeObstacle)
            {
                // 短射線前探，避免 Move 穿進薄牆
                if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dashDir, out RaycastHit hit2, hitRadius * 1.5f, dashObstacleMask))
                {
                    break; // 撞到障礙，提前結束
                }
            }

            yield return null;
        }

        // 還原速度
        agent.speed = originalSpeed;
        lastDashTime = Time.time;
        isDashing = false;

        // ★ 還原動畫速度倍率為1
        OnDashAnimSpeedChanged?.Invoke(1f);
        OnDashEnded?.Invoke(hitPlayer);

        // 進入喘息（命中/落空採用不同硬直）
        float breathTime = hitPlayer ? breathOnHit : breathOnMiss;
        StartCoroutine(BreatheRoutine(breathTime));
    }


    private IEnumerator BreatheRoutine(float duration)
    {
        if(hitPlayer)
        SetState(State.Faint);
        else
        SetState(State.Breathe);

        // 播放喘息動畫
        //animator.SetTrigger(animTriggerBreathe);

        // 完全停住
        agent.isStopped = true;

        yield return new WaitForSeconds(duration);

        // 回到追逐（若仍看得到玩家）
        if (player && Vector3.Distance(transform.position, player.position) <= detectRadius)
        {
            SetState(State.Chase);
        }
        else
        {
            SetState(State.Idle);
        }
    }

    // ========================= 公開 API（給其它腳本呼叫） =========================

    /// <summary>
    /// 被玩家攻擊時可呼叫此函數（例如在 Enemy 的受擊處理裡）
    /// </summary>
    public void OnDamaged(float damageAmount)
    {
        // 播受擊反應動畫（可選）
        //animator.SetTrigger(animTriggerHitReact);

        hitCount++;
        OnHitReact?.Invoke();
        // 也可以因受傷而小幅增加怒氣（可選）
        // AddRage(damageAmount * 0.2f);
    }

    /// <summary>
    /// 提供 UI 或其它系統讀取怒氣百分比。
    /// </summary>
    public float GetRagePercent()
    {
        return Mathf.Clamp01(rage / rageMax);
    }

    // ========================= 小工具 / 輔助 =========================
    private void AddRage(float amount)
    {
        rage = Mathf.Clamp(rage + amount, 0f, rageMax);
        OnRageChanged?.Invoke(rage, rageMax);
    }

    private bool HasClearLineToPlayer()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 target = player.position + Vector3.up * 1.0f;
        Vector3 dir = target - origin;

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dir.magnitude, obstacleMask))
        {
            // 擋住了（撞到牆/障礙）
            return false;
        }
        return true;
    }

    // 在 Scene 視圖內輔助觀察
    private void OnDrawGizmosSelected()
    {
        // 視覺半徑
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRadius);

        // 周邊半徑
        Gizmos.color = new Color(1f,0.5f,0f,0.8f);
        Gizmos.DrawWireSphere(transform.position, peripheralRadius);

        // 視野錐
        Vector3 fwd = Application.isPlaying ? transform.forward : Vector3.forward;
        float half = viewAngle * 0.5f;
        Quaternion left  = Quaternion.Euler(0f, -half, 0f);
        Quaternion right = Quaternion.Euler(0f,  half, 0f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + left  * fwd * visionRadius);
        Gizmos.DrawLine(transform.position, transform.position + right * fwd * visionRadius);

        // 最後目擊點
        Gizmos.color = hasLineOfSight ? Color.green : Color.red;
        Gizmos.DrawSphere(lastKnownPosition == Vector3.zero ? transform.position : lastKnownPosition, 0.15f);
    }
    /// <summary>
    /// 是否在視野錐內（僅角度與距離，不含遮擋）
    /// </summary>
    private bool InFOV(Transform target, float angleDeg, float radius)
    {
        Vector3 to = target.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist > radius) return false;

        Vector3 fwd = transform.forward;
        float ang = Vector3.Angle(fwd, to);
        return ang <= angleDeg * 0.5f;
    }

    /// <summary>
    /// 是否有清晰視線（Raycast 無被障礙擋）
    /// </summary>
    private bool HasLineOfSightTo(Transform target)
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 dest   = target.position + Vector3.up * 1.0f;
        Vector3 dir    = (dest - origin);
        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dir.magnitude, visionObstacleMask))
            return false;
        return true;
    }

}

