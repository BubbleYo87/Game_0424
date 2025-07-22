using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class EnemyB : MonoBehaviour
{
    #region 偵測參數
    [Header("偵測參數")]
    [Tooltip("角色偵測玩家的半徑範圍")]
    public float detectionRadius = 10f;
    [Tooltip("角色視野扇形的總角度")]
    public float viewAngle = 90f;
    [Tooltip("要偵測目標的 LayerMask，通常設為玩家所在圖層")]
    public LayerMask playerMask;
    [Tooltip("玩家 Transform 引用，如果未指派則自動透過 Tag 搜尋")]
    public Transform player;
    [Tooltip("玩家距離超過 chaseStopDistance 時，經過 returnDelay 後觸發返回")]
    public float returnDelay = 2f;
    [Tooltip("追擊停止的最大距離；超過後停止追擊並開始計時")]
    public float chaseStopDistance = 15f;
    #endregion

    #region 攻擊參數
    [Header("攻擊參數")]
    [Tooltip("玩家與敵人距離小於此值時觸發攻擊")]
    public float attackDistance = 1.5f;
    [Tooltip("攻擊完成後進入醉酒狀態的機率（0 表示 0%，1 表示 100%）")]
    [Range(0f, 1f)]
    public float drunkChance = 0.4f;
    #endregion

    #region 動畫參數
    [Header("動畫參數名稱")]
    [Tooltip("移動速度 Blend Tree 參數名")]
    public string speedParam = "E_Guy_Speed";
    [Tooltip("觸發攻擊動畫的 Trigger 名稱")]
    public string attackTrigger = "E_Guy_attack";
    [Tooltip("控制是否進入醉酒動畫的 Bool 名稱")]
    public string drunkBool = "E_Guy_Drunk";
    [Tooltip("受擊動畫的 Trigger 名稱")]
    public string hitTrigger = "E_Guy_GetHit";
    [Tooltip("Blend Tree 速度參數平滑時間")]
    public float speedDampTime = 0.1f;
    #endregion

    #region Idle 隨機視野旋轉參數
    [Header("Idle 隨機視野旋轉參數")]
    [Tooltip("Idle 狀態下，每次掃視的最短間隔（秒）")]
    public float idleLookIntervalMin = 2f;
    [Tooltip("Idle 狀態下，每次掃視的最長間隔（秒）")]
    public float idleLookIntervalMax = 5f;
    [Tooltip("從當前朝向轉到新朝向所需時間（秒）")]
    public float idleRotateDuration = 0.5f;
    [Tooltip("轉向後停留新朝向的時間（秒）")]
    public float idleHoldTime = 2f;
    [Tooltip("掃視時，左右最大偏航角度（度），相對於當前朝向")]
    public float maxIdleYawAngle = 45f;
    #endregion

    #region 血量與血條顯示
    [Header("血量")]
    public int maxHP = 3;      // 最大血量
    private int currentHP;     // 當前血量

    [Header("血條物件 (HP0~HP3)")]
    public GameObject[] hpBars;  // 血條顯示物件陣列，長度應為 maxHP+1
    #endregion

    #region 私有成員
    private NavMeshAgent agent;    // 尋路模組
    private Animator animator;     // 動畫控制器
    private Vector3 homeCenter;    // 回家（起始）位置
    private float returnTimer;     // 返回計時器
    private State lastState;       // 上一個狀態（用於 Debug）
    private State prevState;       // 受擊前的狀態，用於受擊結束後恢復

    public bool isDrunk;           // 當前是否處於醉酒狀態
    public PlayerAnimationController playerscript; // 玩家動畫控制腳本，用於判定是否被攻擊

    // Idle 掃視旋轉相關
    private float idleLookTimer;   // 倒數計時器
    private bool isIdleRotating = false;  // 是否正在旋轉
    private Quaternion idleStartRot;      // 掃視起始朝向
    private Quaternion idleTargetRot;     // 掃視目標朝向
    #endregion

    #region 狀態機定義
    private enum State { Idle, Chasing, Attacking, Drunk, Hit, Returning }
    private State currentState = State.Idle;  // 初始為 Idle 狀態
    #endregion

    #region Unity 事件函式
    // Awake 用於提前初始化引用
    void Awake()
    {
        // 如果 Inspector 未指定 player，則透過 Tag 查找
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                player = go.transform;
            else
                Debug.LogError("[EnemyB] 找不到 Tag 為 Player 的物件，請確認 Tag 設定！");
        }

        // 如未指定 playerscript，嘗試從 player 上取得
        if (playerscript == null && player != null)
        {
            playerscript = player.GetComponent<PlayerAnimationController>();
            // 若無此元件，可依需求顯示警告
        }
    }

    // Start 用於取得元件 & 初始設定
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        homeCenter = transform.position;  // 設定回家位置
        lastState = currentState;

        // 初始化血量
        currentHP = maxHP;

        // 確認 hpBars 陣列長度正確
        if (hpBars == null || hpBars.Length != maxHP + 1)
        {
            Debug.LogError("請在 Inspector 設定 hpBars 陣列長度為 " + (maxHP + 1));
            enabled = false;
            return;
        }

        // 初始化 Idle 掃視倒數與血條顯示
        ResetIdleLookTimer();
        UpdateHPBars();
    }

    // 每幀更新：狀態處理與動畫參數平滑
    void Update()
    {
        // 更新並平滑移動速度參數
        float speedPct = Mathf.Clamp01(agent.velocity.magnitude / agent.speed);
        animator.SetFloat(speedParam, speedPct, speedDampTime, Time.deltaTime);

        // 狀態切換邏輯
        switch (currentState)
        {
            case State.Idle:
                DetectPlayer();  // 在 Idle 狀態檢測玩家
                break;
            case State.Chasing:
                HandleChasing(); // 處理追擊邏輯
                break;
            case State.Attacking:
            case State.Drunk:
            case State.Hit:
                // 攻擊、醉酒或受擊時皆停止移動
                agent.isStopped = true;
                break;
            case State.Returning:
                HandleReturning(); // 處理返回邏輯
                break;
        }

        // Idle 狀態下觸發隨機掃視
        if (currentState == State.Idle && !isIdleRotating)
        {
            idleLookTimer -= Time.deltaTime;
            if (idleLookTimer <= 0f)
            {
                // 計算隨機偏航後的目標朝向
                idleStartRot = transform.rotation;
                float yawOffset = Random.Range(-maxIdleYawAngle, maxIdleYawAngle);
                idleTargetRot = Quaternion.Euler(0f, transform.eulerAngles.y + yawOffset, 0f);
                StartCoroutine(DoIdleRotate());
            }
        }

        // Debug: 輸出狀態變化
        if (currentState != lastState)
        {
            Debug.Log($"[EnemyB] State changed: {lastState} -> {currentState}");
            lastState = currentState;
        }
    }

    // 當碰撞器進入觸發 (如玩家或子彈)
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("P_Bullet"))
        {
            // 扣血、進入受擊狀態
            TakeDamage(1);
            prevState = currentState;
            currentState = State.Hit;
            agent.isStopped = true;
            animator.SetTrigger(hitTrigger);
        }
    }

    // 顯示視野範圍與扇形
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.blue;
        Vector3 left = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + left * detectionRadius);
        Gizmos.DrawLine(transform.position, transform.position + right * detectionRadius);
    }

    // 在畫面上顯示當前狀態 (僅開發階段使用)
    private void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 200, 20), $"EnemyB State: {currentState}");
    }
    #endregion

    #region 狀態處理方法
    // 檢測玩家是否進入視野範圍
    private void DetectPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerMask);
        foreach (var hit in hits)
        {
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) < viewAngle * 0.5f)
            {
                // 玩家在視野內，開始追擊並撥放咆哮
                currentState = State.Chasing;
                animator.SetTrigger("E_Guy_Roar");
                return;
            }
        }
    }

    // 追擊邏輯：移動到玩家位置，根據距離決定攻擊或返回
    private void HandleChasing()
    {
        agent.SetDestination(player.position);
        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= attackDistance)
        {
            // 距離小於攻擊距離，停止並攻擊
            agent.isStopped = true;
            agent.ResetPath();
            animator.SetTrigger(attackTrigger);

            // 隨機決定是否進入醉酒狀態
            isDrunk = Random.value < drunkChance;
            animator.SetBool(drunkBool, isDrunk);
            currentState = isDrunk ? State.Drunk : State.Attacking;
            return;
        }

        if (dist > chaseStopDistance)
        {
            // 玩家跑遠，開始計時返回
            returnTimer += Time.deltaTime;
            if (returnTimer >= returnDelay)
            {
                currentState = State.Returning;
                agent.isStopped = false;
                agent.SetDestination(GetRandomHomePosition());
            }
        }
        else
        {
            // 玩家未跑太遠，重置返回計時器
            returnTimer = 0f;
        }
    }

    // 返回邏輯：移動回起始點，抵達後回 Idle
    private void HandleReturning()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
            currentState = State.Idle;
        DetectPlayer();
    }

    // 隨機取得一個回家範圍內的位置
    private Vector3 GetRandomHomePosition()
    {
        float range = 2.5f;
        return homeCenter + new Vector3(
            Random.Range(-range, range),
            0,
            Random.Range(-range, range)
        );
    }
    #endregion

    #region 動畫事件回調
    // 攻擊動畫結束
    public void OnAttackComplete()
    {
        animator.ResetTrigger(attackTrigger);
        if (!isDrunk)
        {
            currentState = State.Chasing;
            agent.isStopped = false;
        }
    }

    // 醉酒動畫結束
    public void OnDrunkComplete()
    {
        animator.SetBool(drunkBool, false);
        currentState = State.Chasing;
        agent.isStopped = false;
    }

    // 受擊動畫結束
    public void OnGetHitComplete()
    {
        animator.ResetTrigger(hitTrigger);
        currentState = prevState == State.Hit ? State.Idle : prevState;
        agent.isStopped = false;
    }
    #endregion

    #region 受擊與血量管理
    /// <summary>
    /// 扣血函式，外部可呼叫
    /// </summary>
    public void TakeDamage(int dmg = 1)
    {
        currentHP = Mathf.Clamp(currentHP - dmg, 0, maxHP);
        UpdateHPBars();

        if (currentHP <= 0)
        {
            // 播放死亡動畫並延遲銷毀
            animator.SetTrigger("E_Guy_Die");
            agent.isStopped = true;
            Destroy(gameObject, 3f);
        }
        else
        {
            // 播受擊動畫
            animator.SetTrigger(hitTrigger);
        }
    }

    /// <summary>
    /// 根據 currentHP 顯示對應血條
    /// </summary>
    private void UpdateHPBars()
    {
        for (int i = 0; i < hpBars.Length; i++)
        {
            hpBars[i].SetActive(i == currentHP);
        }
    }
    #endregion

    #region Idle 掃視協程
    private IEnumerator DoIdleRotate()
    {
        isIdleRotating = true;
        float t = 0f;

        // 平滑旋轉到目標朝向
        while (t < idleRotateDuration)
        {
            transform.rotation = Quaternion.Slerp(idleStartRot, idleTargetRot, t / idleRotateDuration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.rotation = idleTargetRot;

        // 停留一段時間
        yield return new WaitForSeconds(idleHoldTime);

        // 平滑轉回原始朝向
        t = 0f;
        while (t < idleRotateDuration)
        {
            transform.rotation = Quaternion.Slerp(idleTargetRot, idleStartRot, t / idleRotateDuration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.rotation = idleStartRot;

        // 重置下一次掃視計時器
        ResetIdleLookTimer();
        isIdleRotating = false;
    }

    // 重設隨機掃視倒數
    private void ResetIdleLookTimer()
    {
        idleLookTimer = Random.Range(idleLookIntervalMin, idleLookIntervalMax);
    }
    #endregion

    #region 外部控制方法
    /// <summary>
    /// 停止敵人移動
    /// </summary>
    public void Npc_StopWalk()
    {
        agent.isStopped = true;
    }

    /// <summary>
    /// 允許敵人移動
    /// </summary>
    public void Npc_CanWalk()
    {
        agent.isStopped = false;
    }
    #endregion
}
