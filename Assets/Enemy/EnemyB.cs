using UnityEngine;
using UnityEngine.AI;

public class EnemyB : MonoBehaviour
{
    public float detectionRadius = 10f;            // 敵人視野半徑
    public float viewAngle = 90f;                  // 扇形視野角度
    public LayerMask playerMask;                   // 玩家圖層
    public Transform player;                       // 玩家 Transform (需外部指定)
    public float chaseStopDistance = 15f;          // 超出此距離停止追擊
    public float returnDelay = 2f;                 // 失去目標後等待幾秒才返回棲息區

    private Vector3 homeCenter;                    // 棲息區中心
    private NavMeshAgent agent;                    // 導航組件
    private enum State { Idle, Chasing, Returning } // 狀態機
    private State currentState = State.Idle;       // 目前狀態
    private float returnTimer;                     // 回家倒數計時器
    public Animator animator;           // 指定Animator（在Inspector拖進來或GetComponent）
    public float comboTriggerDistance = 2f;   // 距離多少就觸發Combo動畫


    void Start()
    {
        homeCenter = transform.position;           
        agent = GetComponent<NavMeshAgent>();      

        // 自動抓自己身上的 Animator
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Idle:
                DetectPlayer();                    // 閒置狀態持續偵測玩家
                break;
            case State.Chasing:
                agent.SetDestination(player.position); // 持續追蹤玩家

                // 新增：檢查與玩家距離
                float distToPlayer = Vector3.Distance(transform.position, player.position);
                if (distToPlayer <= comboTriggerDistance)
                {
                    animator.SetTrigger("combo"); // 播放combo動畫
                }

                // 原本的邏輯
                if (distToPlayer > chaseStopDistance)
                {
                    returnTimer += Time.deltaTime;
                    if (returnTimer > returnDelay)
                    {
                        currentState = State.Returning;
                        agent.SetDestination(GetRandomHomePosition());
                    }
                }
                else
                {
                    returnTimer = 0; // 追擊中重置倒數
                }
                break;
            case State.Returning:
                // 回到棲息區隨機點後回到閒置
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                    currentState = State.Idle;
                DetectPlayer();                    // 回家途中也可偵測到玩家
                break;
        }
    }

    void DetectPlayer()
    {
        // 扇形範圍偵測玩家
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerMask);
        foreach (Collider hit in hits)
        {
            Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2f)
            {
                currentState = State.Chasing;     // 發現玩家開始追擊
                Debug.Log("EB");
                break;
            }
        }
    }

    Vector3 GetRandomHomePosition()
    {
        // 在棲息區 5x5 隨機選一個位置
        Vector3 randomOffset = new Vector3(Random.Range(-2.5f, 2.5f), 0, Random.Range(-2.5f, 2.5f));
        return homeCenter + randomOffset;
    }
}
