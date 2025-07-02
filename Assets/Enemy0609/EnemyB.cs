using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class EnemyB : MonoBehaviour
{
    [Header("偵測參數")]
    [Tooltip("模型角色的視野範圍半徑")] public float detectionRadius = 10f;
    [Tooltip("視野的角度大小，扇形角度的一半用於內部計算")] public float viewAngle = 90f;
    [Tooltip("用於偵測的目標圖層遮罩，通常設定為玩家所在圖層")] public LayerMask playerMask;
    [Tooltip("要追蹤的玩家 Transform 引用")] public Transform player;
    [Tooltip("當玩家超出 chaseStopDistance 時，等待此秒數後返回起始位置")] public float returnDelay = 2f;
    [Tooltip("追擊玩家的最大距離；超過則不再追擊，轉為返回狀態")] public float chaseStopDistance = 15f;

    [Header("攻擊參數")]
    [Tooltip("當玩家與敵人距離小於此值時，立刻觸發攻擊（並判斷是否醉酒）")] public float attackDistance = 1.5f;
    [Tooltip("攻擊完成後進入醉酒狀態的機率（0 表示 0%，1 表示 100%）")][Range(0f, 1f)] public float drunkChance = 0.4f;

    [Header("動畫參數名稱")]
    [Tooltip("Blend Tree 控制移動速度的 float 參數名")] public string speedParam = "E_Guy_Speed";
    [Tooltip("觸發攻擊動畫的 Trigger 參數名")] public string attackTrigger = "E_Guy_attack";
    [Tooltip("控制是否進入醉酒動畫的 Bool 參數名")] public string drunkBool = "E_Guy_Drunk";
    [Tooltip("受擊動畫的 Trigger 參數名")] public string hitTrigger = "E_Guy_GetHit";
    [Tooltip("Blend Tree 速度參數平滑時間")] public float speedDampTime = 0.1f;

    [Header("血量")]
    public int maxHP = 3;             // 最大血量
    private int currentHP;            // 当前血量

    [Header("血条物件 (HP0~HP3)")]
    public GameObject[] hpBars;       // 长度应为 maxHP+1

    // 私有成員
    private NavMeshAgent agent;       // 尋路模組
    private Animator animator;    // 動畫控制
    private Vector3 homeCenter;  // 返回起點座標
    private float returnTimer; // 返回計時器
    private State lastState;   // 上一個狀態 (Debug)
    private State prevState;   // 受擊前的狀態，以便結束後回復

    public bool isDrunk;                        // 當前是否醉酒
    public PlayerAnimationController playerscript; // 玩家腳本，用於判定是否被攻擊

    // 狀態機
    private enum State { Idle, Chasing, Attacking, Drunk, Hit, Returning }
    private State currentState = State.Idle;

    void Awake()
    {
        // 如果 Inspector 裏沒設 player，就自動以 Tag 找
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                player = go.transform;
            else
                Debug.LogError("[EnemyB] 找不到 Tag 為 Player 的物件，請確認 Tag 設定！");
        }

        // 如果 Inspector 裏沒設 playerscript，就從 player 上拿
        if (playerscript == null && player != null)
        {
            playerscript = player.GetComponent<PlayerAnimationController>();
            if (playerscript == null)
                Debug.LogWarning("[EnemyB] Player 上沒有 PlayerAnimationController 腳本");
        }
    }
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        homeCenter = transform.position;
        lastState = currentState;

        // 初始化血量
        currentHP = maxHP;

        // 检查
        if (hpBars == null || hpBars.Length != maxHP + 1)
        {
            Debug.LogError("请在 Inspector 把 hpBars 长度设为 4，并依次拖入 HP0、HP1、HP2、HP3");
            enabled = false;
            return;
        }

        UpdateHPBars();
    }

    void Update()
    {
        // 更新、平滑速度參數
        float speedPct = Mathf.Clamp01(agent.velocity.magnitude / agent.speed);
        animator.SetFloat(speedParam, speedPct, speedDampTime, Time.deltaTime);

        // 狀態切換邏輯
        switch (currentState)
        {
            case State.Idle:
                DetectPlayer();
                break;
            case State.Chasing:
                HandleChasing();
                break;
            case State.Attacking:
            case State.Drunk:
            case State.Hit:
                // 攻擊、醉酒或受擊時皆停止移動
                agent.isStopped = true;
                break;
            case State.Returning:
                HandleReturning();
                break;
        }

        // Debug：狀態變化輸出
        if (currentState != lastState)
        {
            Debug.Log($"[EnemyB] State changed: {lastState} -> {currentState}");
            lastState = currentState;
        }
    }

    // 追擊邏輯
    private void HandleChasing()
    {
        agent.SetDestination(player.position);
        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= attackDistance)
        {
            // 進入攻擊前清空路徑
            agent.isStopped = true;
            agent.ResetPath();

            // 播放攻擊動畫並隨機醉酒
            animator.SetTrigger(attackTrigger);
            isDrunk = Random.value < drunkChance;
            animator.SetBool(drunkBool, isDrunk);

            // 切入相應狀態
            currentState = isDrunk ? State.Drunk : State.Attacking;
            return;
        }
        if (dist > chaseStopDistance)
        {
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
            returnTimer = 0f;
        }
    }

    // 返回邏輯
    private void HandleReturning()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
            currentState = State.Idle;
        DetectPlayer();
    }

    // 視野檢測
    private void DetectPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerMask);
        foreach (var hit in hits)
        {
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) < viewAngle * 0.5f)
            {
                currentState = State.Chasing;
                return;
            }
        }
    }

    private Vector3 GetRandomHomePosition()
    {
        float range = 2.5f;
        return homeCenter + new Vector3(
            Random.Range(-range, range), 0, Random.Range(-range, range)
        );
    }

    // Animation Event：攻擊結束
    public void OnAttackComplete()
    {
        animator.ResetTrigger(attackTrigger);
        if (!isDrunk)
        {
            currentState = State.Chasing;
            agent.isStopped = false;
        }
    }

    // Animation Event：醉酒結束
    public void OnDrunkComplete()
    {
        animator.SetBool(drunkBool, false);
        currentState = State.Chasing;
        agent.isStopped = false;
    }

    // Animation Event：受擊結束
    public void OnGetHitComplete()
    {
        animator.ResetTrigger(hitTrigger);
        // 回到受擊前狀態並恢復移動
        currentState = State.Idle;
        agent.isStopped = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 碰到玩家且玩家正在攻擊
        if (other.CompareTag("Player") || other.CompareTag("P_Bullet")/* && playerscript.isAttack */)
        {
            // 記錄受擊前狀態
            TakeDamage(1);
            prevState = currentState;
            currentState = State.Hit;
            agent.isStopped = true;
            animator.SetTrigger(hitTrigger);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 視野範圍輔助線
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.blue;
        Vector3 left = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + left * detectionRadius);
        Gizmos.DrawLine(transform.position, transform.position + right * detectionRadius);
    }

    private void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 200, 20), $"EnemyB State: {currentState}");
    }
    /// <summary>
    /// 扣血用这个，在 OnTriggerEnter 或其他地方调用
    /// </summary>
    public void TakeDamage(int dmg = 1)
    {
        currentHP = Mathf.Clamp(currentHP - dmg, 0, maxHP);
        UpdateHPBars();

        if (currentHP <= 0)
        {
            // 播死亡动画，或做别的
            animator.SetTrigger("E_Guy_Die");
        }
        else
        {
            // 播受击动画
            animator.SetTrigger(hitTrigger);
        }
    }

    /// <summary>
    /// 根据 currentHP 来显示／隐藏对应的那根血条
    /// </summary>
    private void UpdateHPBars()
    {
        for (int i = 0; i < hpBars.Length; i++)
        {
            hpBars[i].SetActive(i == currentHP);
        }
    }
}
