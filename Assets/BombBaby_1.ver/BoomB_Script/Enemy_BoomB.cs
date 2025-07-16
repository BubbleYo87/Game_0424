using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.VFX;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]  // 確保物件上有 NavMeshAgent 元件
public class Enemy_BoomB : MonoBehaviour
{
    [Header("偵測參數")]
    [Tooltip("偵測玩家的半徑範圍")] 
    public float detectionRadius = 10f;            // 偵測玩家的距離範圍
    [Tooltip("視野角度 (度)，扇形角度的一整圈")] 
    public float viewAngle = 90f;                 // 視野扇形的總角度
    [Tooltip("玩家所在的 LayerMask，用於 OverlapSphere")] 
    public LayerMask playerMask;                  // 用來篩選玩家的 Layer
    [Tooltip("玩家 Transform 引用")] 
    public Transform player;                      // 玩家位置引用
    [Tooltip("超出此距離後開始倒計時返家")] 
    public float chaseStopDistance = 15f;         // 追擊中，若與玩家距離超過此值，開始返家計時
    [Tooltip("超出 chaseStopDistance 後，等待此秒數才真正返家")]
    public float returnDelay = 2f;                // 超出追擊距離後，延遲多久才進入 Returning

    [Header("Idle 掃視參數")]
    [Tooltip("Idle 狀態下，最短掃視間隔")] 
    public float idleLookIntervalMin = 2f;        // 隨機掃視的最小間隔時間
    [Tooltip("Idle 狀態下，最長掃視間隔")] 
    public float idleLookIntervalMax = 5f;        // 隨機掃視的最大間隔時間
    [Tooltip("掃視旋轉所需時間")] 
    public float idleRotateDuration = 0.5f;       // 掃視時旋轉用的插值時間
    [Tooltip("掃視後停留時間")] 
    public float idleHoldTime = 2f;               // 掃視到目標後停留多長時間
    [Tooltip("最大左右偏航角度")] 
    public float maxIdleYawAngle = 45f;           // 最大左右旋轉角度範圍

    [Header("血量系統")]
    [Tooltip("最大血量")] 
    public int maxHP = 3;                         // 敵人總血量
    [Tooltip("血條物件陣列，長度請設定為 maxHP+1")]
    public GameObject[] hpBars;                   // 各血量對應的 UI 切換
    private int currentHP;                        // 當前血量

    [Header("攻擊參數")]
    [Tooltip("觸發攻擊的最小距離")]
    public float attackDistance = 5f;         // 距離玩家小於此值就攻擊
    [Tooltip("攻擊時 NavMeshAgent 的速度")]
    public float attackSpeed = 10f;           // 攻擊時的衝刺速度
    /// <summary>
    /// Boom 時計算出的傷害值（1~100 浮點數）
    /// </summary>
    [HideInInspector]
    public float LastDamageValue;
    [Header("Boom 範圍設定")]
    [Tooltip("爆炸判定用的 Collider 所在子物件名稱（請跟 Inspector 裡的 GameObject 名稱吻合）")]
    public string boomColliderObjectName = "Boom_Attack";  // 預設子物件名
    private Collider boomCollider;  // 存放自動找到的 Collider
    [Header("爆炸特效 (ParticleSystem)")]
    [Tooltip("把預先放在子物件或 Prefab 上的 ParticleSystem 拖到這裡")]
    public ParticleSystem boomVFX;
    [Header("玩家狀態參考")]
    [Tooltip("請在 Inspector 把玩家那個含 TakeDamage bool 的腳本拖進來")]
    public PlayerMovementGrappling playerMovementGrappling;        // 假設你的玩家腳本 

    [Header("BoomAttack 物件參考")]
    [Tooltip("場景中代表爆炸判定的物件")]
    public Transform boomAttackObject;       // 拖入名稱為 BoomAttack 的物件

    [Header("UI 顯示設定")]
    [Tooltip("拖要顯示距離的 Image，必須有 CanvasRenderer")]
    public Image boomUIImage;                // 拖入要顯示的那張 Image

    // —— 私有欄位 —— 
    private NavMeshAgent agent;                   // NavMeshAgent 參考
    private Vector3 homeCenter;                   // 初始家中位置，用於返家時隨機範圍
    private float returnTimer;                    // 追擊超時計時器

    private float idleLookTimer;                  // Idle 狀態下，下一次掃視倒數
    private bool isIdleRotating;                  // 是否正在執行掃視 Coroutine
    private Quaternion idleStartRot;              // 掃視起始旋轉
    private Quaternion idleTargetRot;             // 掃視目標旋轉
    
    private enum State { Idle, Chasing, Returning, Hit }  // 敵人狀態列舉
    private State currentState = State.Idle;      // 目前狀態，預設 Idle
    private State lastState = State.Idle;         // 上一次狀態，用於 Debug 切換
    private bool hasBoomed = false;           // 是否已經觸發過一次攻擊
    // ----- 新增私有欄位 -----
    private Animator childAnimator;  // 子物件的 Animator
    // -------------------------

    // 在 Awake 階段確認 player 參考是否設定
    void Awake()
    {
        // … 前面找 player 的程式 …
        //── 確保 player 一定要被賦值 ──
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                player = go.transform;
            else
                Debug.LogError("[Enemy_BoomB] 無法找到 Tag 為 'Player' 的物件，請確認已經設定 Tag");
        }
        // 如果 Inspector 沒設，就自動抓
        if (playerMovementGrappling == null && player != null)
            playerMovementGrappling = player.GetComponent<PlayerMovementGrappling>();

        // BoomAttack 子物件
        if (boomAttackObject == null)
        {
            // 以前：var tf = …
            Transform boomAtkTf = transform.root.Find("Boom_Attack");
            if (boomAtkTf != null)
                boomAttackObject = boomAtkTf;
        }

        // UI Image 物件
        if (boomUIImage == null)
        {
            // 以前：var go = …
            GameObject uiGo = GameObject.Find("BoomB_AtkEff_S");
            if (uiGo != null)
                boomUIImage = uiGo.GetComponent<Image>();
        }

        // Collider 子物件（同理改名）
        Transform colliderTf = transform.Find(boomColliderObjectName);
        if (colliderTf != null)
        {
            boomCollider = colliderTf.GetComponent<Collider>();
            // …
        }
        else
        {
            Debug.LogError($"[Enemy_BoomB] 找不到子物件「{boomColliderObjectName}」");
        }
    }


    // 初始化 NavMeshAgent、血量與 Idle 計時器
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        homeCenter = transform.position;      // 紀錄初始位置
        currentHP = maxHP;                    // 設定滿血

        // 檢查 hpBars 是否正確設定
        if (hpBars == null || hpBars.Length != maxHP + 1)
        {
            Debug.LogError("[Enemy_BoomB] 請設定 hpBars 長度為 maxHP+1");
            enabled = false;                 // 停用此腳本
            return;
        }

        // 找到子物件上的 Animator（假設你的模型放在這個物件底下）
        childAnimator = GetComponentInChildren<Animator>();
        if (childAnimator == null)
            Debug.LogError("[Enemy_BoomB] 找不到子物件的 Animator，請確認模型層級");

        UpdateHPBars();                      // 更新血條顯示
        ResetIdleLookTimer();                // 設定下一次 Idle 掃視時間
    }

    // 每幀根據狀態執行對應邏輯
    void Update()
    {
        switch (currentState)
        {
            case State.Idle:
                DetectPlayer();               // 偵測玩家進入視野
                HandleIdleLook();             // 處理 Idle 隨機掃視
                break;
            case State.Chasing:
                HandleChasing();              // 追擊玩家
                break;
            case State.Returning:
                HandleReturning();            // 返家邏輯
                break;
            case State.Hit:
                agent.isStopped = true;       // 被打斷時停止移動
                break;
        }

        // 若狀態有變化，印出 Debug 訊息
        if (currentState != lastState)
        {
            // Debug.Log($"[Enemy_BoomB] {lastState} -> {currentState}");
            lastState = currentState;
        }

        // 每幀同步速度參數
        SyncSpeedWithAnimator();
        float dist = Vector3.Distance(transform.position, player.position);

        // —— 新增：距離檢查攻擊觸發 —— 
        if (!hasBoomed && player != null && currentState == State.Chasing)
        {
            if (dist < attackDistance)
            {
                // 1. 觸發攻擊動畫
                childAnimator.SetTrigger("BoomB_Attack");
                agent.isStopped = true;
                // 2. 調整速度並快速向玩家靠近
                /* agent.speed = attackSpeed;
                agent.isStopped = false;
                agent.SetDestination(player.position); */

                hasBoomed = true;  // 只執行一次
            }
        }
    }

    /// <summary>
    /// 處理 Idle 狀態下的隨機掃視
    /// </summary>
    private void HandleIdleLook()
    {
        if (isIdleRotating) return;          // 若正在掃視中，跳過

        idleLookTimer -= Time.deltaTime;     // 倒數計時
        if (idleLookTimer <= 0f)
        {
            // 設定掃視的起始與目標旋轉
            idleStartRot = transform.rotation;
            float yaw = transform.eulerAngles.y + Random.Range(-maxIdleYawAngle, maxIdleYawAngle);
            idleTargetRot = Quaternion.Euler(0, yaw, 0);
            StartCoroutine(DoIdleRotate());  // 開始掃視 Coroutine
        }
    }

    /// <summary>
    /// Coroutine：做一次左右掃視，停留，再回到原方向
    /// </summary>
    private IEnumerator DoIdleRotate()
    {
        isIdleRotating = true;
        float t = 0f;

        // 由起始旋轉轉到目標旋轉
        while (t < idleRotateDuration)
        {
            transform.rotation = Quaternion.Slerp(idleStartRot, idleTargetRot, t / idleRotateDuration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.rotation = idleTargetRot;

        // 在目標方向停留
        yield return new WaitForSeconds(idleHoldTime);

        // 再從目標方向轉回起始方向
        t = 0f;
        while (t < idleRotateDuration)
        {
            transform.rotation = Quaternion.Slerp(idleTargetRot, idleStartRot, t / idleRotateDuration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.rotation = idleStartRot;

        ResetIdleLookTimer();                // 重置下一次掃視計時
        isIdleRotating = false;
    }

    /// <summary>
    /// 重置 Idle 掃視倒數計時器
    /// </summary>
    private void ResetIdleLookTimer()
    {
        idleLookTimer = Random.Range(idleLookIntervalMin, idleLookIntervalMax);
    }

    /// <summary>
    /// 偵測玩家是否進入視野範圍與視野角內
    /// </summary>
    private void DetectPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerMask);
        foreach (var hit in hits)
        {
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            // 判斷夾角是否在視野範圍內
            if (Vector3.Angle(transform.forward, dir) < viewAngle * 0.5f)
            {
                currentState = State.Chasing;
                agent.isStopped = false;    // 開始移動追擊
                return;
            }
        }
    }

    /// <summary>
    /// 追擊玩家邏輯：設定目標，並監控距離決定是否返家
    /// </summary>
    private void HandleChasing()
    {
        agent.SetDestination(player.position);
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > chaseStopDistance)
        {
            returnTimer += Time.deltaTime;
            if (returnTimer >= returnDelay)
            {
                currentState = State.Returning;
                agent.isStopped = false;
                agent.SetDestination(GetRandomHomePosition());  // 返家時設定隨機目標
            }
        }
        else
        {
            returnTimer = 0f;  // 玩家靠近，重置返家計時
        }
    }

    /// <summary>
    /// 返家邏輯：走到家後回到 Idle，並持續偵測玩家
    /// </summary>
    private void HandleReturning()
    {
        // 到家附近時回 Idle
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
            currentState = State.Idle;

        // 在返家途中仍可偵測玩家，若發現則立刻追擊
        if (currentState == State.Returning)
            DetectPlayer();
    }

    /// <summary>
    /// 取得家中隨機一點，用於返家路線
    /// </summary>
    private Vector3 GetRandomHomePosition()
    {
        float range = 2.5f;
        return homeCenter + new Vector3(
            Random.Range(-range, range),
            0,
            Random.Range(-range, range)
        );
    }

    /// <summary>
    /// 受到傷害時呼叫，更新血量，切換血條，死亡或進入 Hit 狀態
    /// </summary>
    public void TakeDamage(int dmg = 1)
    {
        Boom();
        currentHP = Mathf.Clamp(currentHP - dmg, 0, maxHP);
        UpdateHPBars();

        if (currentHP <= 0)
        {
            Destroy(gameObject, 3f);        // 血量歸零後 3 秒後銷毀物件
        }
        else
        {
            currentState = State.Hit;        // 進入被擊打中斷狀態
            StartCoroutine(RecoverFromHit());
        }
    }

    /// <summary>
    /// Coroutine：Hit 狀態持續 0.5 秒後恢復到 Idle
    /// </summary>
    private IEnumerator RecoverFromHit()
    {
        yield return new WaitForSeconds(0.5f);
        currentState = State.Idle;
        agent.isStopped = false;            // 解除停止，恢復移動
    }

    // 若玩家用 Trigger 方式接觸到敵人，則造成傷害
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("P_Bullet"))
        {
            TakeDamage(1);
        }
    }
    /// <summary>
    /// 更新血條陣列顯示，只有 index == currentHP 的那個物件顯示
    /// </summary>
    private void UpdateHPBars()
    {
        for (int i = 0; i < hpBars.Length; i++)
            hpBars[i].SetActive(i == currentHP);
    }

    // 在 Scene 檢視中，繪製偵測範圍與視野線
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.blue;
        Vector3 leftDir  = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0,  viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + leftDir  * detectionRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * detectionRadius);
    }
    
    /// <summary>
    /// 當與玩家距離小於 2.5 時，同步 NavMeshAgent 的速度到 Animator 參數 BoomB_Speed
    /// （並可選擇再同步回 agent.speed）
    /// </summary>
    private void SyncSpeedWithAnimator()
    {
        if (childAnimator == null) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        // 1. 取得當前移動速度（m/s）
        float currentMoveSpeed = agent.velocity.magnitude;
        
        // 2. 同步到 Animator 參數
        childAnimator.SetFloat("BoomB_Speed", currentMoveSpeed);
        
        // 3. （可選）也把這個參數值同步回 agent.speed，
        //    如果你想讓 Animator 控制真正的行走速度，就打開下一行：
        // agent.speed = childAnimator.GetFloat("BoomB_Speed");
    }
    [Header("Boom 功能")]
    [Tooltip("Boom 時要打開的單一物件")]
    public GameObject BoomBaby1;  // 在 Inspector 指定要打開的那個物件
    [Tooltip("Boom 時要關閉的單一物件")]
    public GameObject BoomBaby0;    // 指定要關閉的物件


    /// <summary>
    /// 觸發 Boom：開啟 objectToEnable，關閉 objectToDisable
    /// </summary>
    public void Boom()
    {
        agent.isStopped = true;

        // —— 在啟動 Collider 或特效之前就先算好傷害值 —— 
        float dist = Vector3.Distance(boomAttackObject.position, player.position);
        float normalized = Mathf.Clamp01(1f - dist / detectionRadius);
        LastDamageValue = Mathf.Lerp(1f, 100f, normalized);
        Debug.Log($"[BoomUI] value = {LastDamageValue}");

        // —— 原本的 BoomBaby1/BoomBaby0 邏輯 —— 
        if (BoomBaby1 != null) BoomBaby1.SetActive(true);
        if (BoomBaby0 != null) { BoomBaby0.SetActive(false); Destroy(BoomBaby0); }

        // —— 啟動爆炸範圍 Collider —— 
        if (boomCollider != null)
            StartCoroutine(EnableAndDisableBoomCollider());

        // —— 啟動粒子特效 —— 
        if (boomVFX != null)
        {
            // 確保從頭播放
            boomVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            boomVFX.Play();
        }
        // —— 新增：UI 提示 —— 
        StartCoroutine(HandleBoomUIFeedback());
    }
    private IEnumerator EnableAndDisableBoomCollider()
    {
        boomCollider.enabled = true;            // 瞬間開啟
        yield return new WaitForSeconds(0.3f);  // 等 0.3 秒
        boomCollider.enabled = false;           // 再關閉
    }
        /// <summary>
    /// Boom() 之後：  
    /// 1. 檢查 playerMovementGrappling.TakeDamage  
    /// 2. 算出 boomAttackObject 與 player 的距離映射到 1~100  
    /// 3. Debug.Log 距離值  
    /// 4. 設定 boomUIImage 的透明度並顯示  
    /// 5. 等候（100→5秒，1→1秒），再關閉 Image  
    /// </summary>
    private IEnumerator HandleBoomUIFeedback()
    {
        // 等玩家受傷
        yield return new WaitUntil(() => playerMovementGrappling != null && playerMovementGrappling.hasTakenDamage);
        
        // 計算距離、value
        float dist = Vector3.Distance(boomAttackObject.position, player.position);
        float normalized = Mathf.Clamp01(1f - dist / detectionRadius);
        float value = Mathf.Lerp(1f, 100f, normalized);
        /* Debug.Log($"[BoomUI] value = {value}");
 */
        // 顯示並設定 alpha
        boomUIImage.gameObject.SetActive(true);
        Color c = boomUIImage.color;
        c.a = value / 100f;
        boomUIImage.color = c;

        // 線性映射到 [1,5] 區間
        float duration = Mathf.Lerp(1f, 5f, value / 100f);
        Debug.Log($"[BoomUI] 等待 {duration:F2} 秒");

        // 用 unscaledDeltaTime 手動倒數
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        // 倒數結束，關閉 UI
        Debug.Log("[BoomUI] Manual 倒數結束，Hide Image");
        boomUIImage.gameObject.SetActive(false);
    }
}
