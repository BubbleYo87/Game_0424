using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class Guy_2_Script : MonoBehaviour
{
    #region 偵測參數
    [Header("偵測參數")]
    [Tooltip("角色偵測玩家的半徑範圍")]
    public float detectionRadius = 30f;
    [Tooltip("角色視野扇形的總角度")]
    public float viewAngle = 230f;
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
    public float attackDistance = 20f;
    [Tooltip("攻擊完成後進入醉酒狀態的機率（0 表示 0%，1 表示 100%）")]
    [Range(0f, 1f)]
    public float drunkChance = 0f;

    [Header("投擲物件")]
    [Tooltip("要丟出的石頭 Prefab")]
    public GameObject stonePrefab;
    [Tooltip("石頭生成位子 (空物件)、以及朝向")]
    public Transform stoneSpawnPoint;
    [Tooltip("石頭初速度")]
    public float throwSpeed = 10f;
    #endregion
    #region 私有生成石頭管理
    private GameObject currentStone;   // 手上那顆
    private Coroutine flyingRoutine;   // 飛行協程
    #endregion
    // 在 Guy_2_Script 類別最上方的參數區新增：
    [Header("Plan 指示器參數")]
    [Tooltip("要顯示的 Plan 預製件 (必須包含可調透明度的材質)")]
    public GameObject planPrefab;
    [Tooltip("Plan 的寬度 (X 軸)")]
    public float planWidth = 0.3f;
    [Tooltip("閃爍頻率 (Hz)")]
    public float flickerFrequency = 2f;
    [Tooltip("閃爍最低透明度")]
    public float flickerMinAlpha = 0.2f;
    [Tooltip("閃爍最高透明度")]
    public float flickerMaxAlpha = 0.8f;
    [Tooltip("淡出 Plan 持續時間 (秒)")]
    public float fadeOutDuration = 0.5f;

    // 私有欄位
    private GameObject planInstance;      // 當前的 Plan 實例
    private Material  planMaterial;      // 用來控制透明度的材質
    private Coroutine flickerCoroutine;  // 閃爍協程引用
    private Coroutine  updatePlanCoroutine; // 更新 Plan 狀態的協程引用
    private bool       isPlanFixed = false;   // 是否已經固定 Plan 位置

    #region 動畫參數
    [Header("動畫參數名稱")]
    [Tooltip("移動速度 Blend Tree 參數名")]
    public string speedParam = "E_Guy_Speed";
    [Tooltip("觸發攻擊動畫的 Trigger 名稱")]
    public string attackTrigger = "E_Guy_Attack";
    [Tooltip("控制是否進入醉酒動畫的 Bool 名稱")]
    public string drunkBool = "E_Guy_Drunk";
    [Tooltip("受擊動畫的 Trigger 名稱")]
    public string hitTrigger = "E_Guy_GetHit";
    [Tooltip("Blend Tree 速度參數平滑時間")]
    public float speedDampTime = 0.1f;
    [Tooltip("角色轉向速度（每秒度數）")]
    public float rotationSpeed = 5f; // 每秒旋轉 360 度
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
                Lookat(player.position);   // 持續轉向玩家
                break;
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

    #region 攻擊與追擊邏輯
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
            
            currentState = State.Attacking;
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
    #endregion
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

    // AnimationEvent：生成石頭，並禁用它的 Collider
    public void GenerateStone()
    {
        if (stonePrefab == null || stoneSpawnPoint == null) return;
        if (currentStone != null) Destroy(currentStone);

        currentStone = Instantiate(
            stonePrefab,
            stoneSpawnPoint.position,
            stoneSpawnPoint.rotation,
            stoneSpawnPoint  // 先綁到手上
        );
        // 禁用 Collider，避免卡到手或 NPC 本體
        var col = currentStone.GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    // AnimationEvent：丟石頭，啟用 Collider 並啟動飛行協程
    public void ThrowStone()
    {
        if (currentStone == null) return;

        // 從手上鬆綁 & 啟用碰撞
        currentStone.transform.SetParent(null);
        var col = currentStone.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        // 計算延伸到玩家前方 50m 的終點
        Vector3 baseDir = (player.position - stoneSpawnPoint.position).normalized;
        Vector3 endPoint = player.position + baseDir * 50f;

        // 啟動協程：往 endPoint 直線飛行
        flyingRoutine = StartCoroutine(MoveStoneStraight(currentStone, endPoint, throwSpeed, 5f));

        // 切回追擊
        currentState = State.Chasing;
        agent.isStopped = false;
    }
    // 協程：直線飛到指定終點再銷毀
    private IEnumerator MoveStoneStraight(GameObject stone, Vector3 endPos, float speed, float maxTime)
    {
        float t = 0f;
        while (t < maxTime && stone != null)
        {
            // 每幀直線靠近終點
            stone.transform.position = Vector3.MoveTowards(
                stone.transform.position,
                endPos,
                speed * Time.deltaTime
            );
            // 如果已經到達終點，或者非常接近，就跳出
            if (Vector3.Distance(stone.transform.position, endPos) < 0.1f)
                break;

            t += Time.deltaTime;
            yield return null;
        }
        if (stone != null) Destroy(stone);
    }

    
    /// <summary>
    /// 讓 NPC 面向指定世界座標
    /// </summary>
    private void Lookat(Vector3 worldPos)
    {
        Vector3 dir = (player.position - transform.position).normalized;
        Quaternion targetRot = Quaternion.LookRotation(dir);
        // 用 Slerp 做平滑轉向
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
    }

    // -----------------------------
    // Step1：顯示並開始閃爍 Plan
    // -----------------------------
    public void ShowPlanIndicator()
    {
        if (planPrefab == null || player == null)
        {
            Debug.LogWarning("[Plan] prefab or player 未設定，跳過顯示");
            return;
        }

        // 1. 刪除舊的
        if (planInstance != null)
        {
            Destroy(planInstance);
            planInstance = null;
        }

        // 2. 第一次實例化，位置暫且放在 NPC
        planInstance = Instantiate(planPrefab, transform.position, Quaternion.identity);
        planInstance.transform.localScale = Vector3.one;

        // 3. 取材質，切換成透明模式
        var rend = planInstance.GetComponent<Renderer>();
        planMaterial = rend.material;  
        planMaterial.SetFloat("_Mode", 2);
        planMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        planMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        planMaterial.EnableKeyword("_ALPHABLEND_ON");
        planMaterial.renderQueue = 3000;

        // 4. 開始閃爍（假設你已有這個 Coroutine）
        if (flickerCoroutine != null) StopCoroutine(flickerCoroutine);
        flickerCoroutine = StartCoroutine(FlickerPlan());

        // 5. 啟動持續更新 Coroutine
        isPlanFixed = false;
        if (updatePlanCoroutine != null) StopCoroutine(updatePlanCoroutine);
        updatePlanCoroutine = StartCoroutine(UpdatePlan());
    }

    // -----------------------------
    // Step2：不斷更新 Plan 的位置、方向、長度
    // -----------------------------
    private IEnumerator UpdatePlan()
    {
        int ignorePlanLayer = LayerMask.GetMask("Ignore Raycast");
        int groundMask     = ~ignorePlanLayer;

        const float extension = 15f;  // 往玩家後方延伸 50m

        while (!isPlanFixed && planInstance != null)
        {
            // a. 水平向量
            Vector3 raw = player.position - transform.position;
            Vector3 flatDir = new Vector3(raw.x, 0f, raw.z).normalized;

            // 原本的 NPC→玩家距離
            float baseLength = raw.magnitude;
            // 加上延伸距離
            float fullLength = baseLength + extension;

            // b. 重新計算中心點：往 flatDir 方向走一半的 fullLength
            Vector3 center = transform.position + flatDir * (fullLength * 0.5f);

            // c. 貼地
            RaycastHit hit;
            if (Physics.Raycast(center + Vector3.up * 5f, Vector3.down, out hit, 10f, groundMask))
                center.y = hit.point.y + 0.01f;
            else
                center.y = transform.position.y;

            // d. 更新 Transform
            planInstance.transform.position = center;
            if (flatDir.sqrMagnitude > 0.001f)
                planInstance.transform.rotation = Quaternion.LookRotation(flatDir);
            // Z 軸縮放用 fullLength/10f，X 軸（寬度）維持 planWidth
            planInstance.transform.localScale = new Vector3(
                planWidth,
                1f,
                fullLength / 10f
            );

            yield return null;
        }
    }



    // -----------------------------
    // Step3：呼叫這個來「固定」平面，並執行 ThrowStone
    // -----------------------------
    public void FreezePlanIndicatorAndThrow()
    {
        if (planInstance == null) return;

        // 3. 呼叫丟石頭邏輯
        ThrowStone();

        // 1. 停掉持續更新
        isPlanFixed = true;
        if (updatePlanCoroutine != null)
        {
            StopCoroutine(updatePlanCoroutine);
            updatePlanCoroutine = null;
        }

        // 2. （選擇）也可以停掉閃爍，讓 Plan 保持當前透明度
        if (flickerCoroutine != null)
        {
            StopCoroutine(flickerCoroutine);
            flickerCoroutine = null;
        }

        
    }

    // -----------------------------
    // 閃爍協程：透明度在 min↔max 之間來回
    // -----------------------------
    private IEnumerator FlickerPlan()
    {
        Color c = planMaterial.color;
        while (true)
        {
            // Sin 波動產生 0~1 之間的值，再對應到透明度範圍
            float t = (Mathf.Sin(Time.time * flickerFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            c.a = Mathf.Lerp(flickerMinAlpha, flickerMaxAlpha, t);
            planMaterial.color = c;
            yield return null;
        }
    }

    // -----------------------------
    // 公開：淡出並移除 Plan
    // -----------------------------
    public void HidePlanIndicator()
    {
        StartCoroutine(FadeOutPlanIndicator());
    }

    // -----------------------------
    // 淡出協程：從當前透明度平滑降到 0，然後銷毀
    // -----------------------------
    private IEnumerator FadeOutPlanIndicator()
    {
        // 停止閃爍
        if (flickerCoroutine != null)
        {
            StopCoroutine(flickerCoroutine);
            flickerCoroutine = null;
        }
        if (planMaterial == null) yield break;

        Color c = planMaterial.color;
        float startAlpha = c.a;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
            planMaterial.color = c;
            yield return null;
        }

        // 銷毀 GameObject
        if (planInstance != null)
        {
            Destroy(planInstance);
            planInstance = null;
        }
    }
}