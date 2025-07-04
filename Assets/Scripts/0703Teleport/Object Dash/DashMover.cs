// DashMover.cs
using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class DashMover : MonoBehaviour
{
    [Header("觸發設定")]
    [Tooltip("按下此鍵才會觸發衝刺")]
    public KeyCode triggerKey = KeyCode.T;

    [Header("目標設定")]
    [Tooltip("如果指定了這個 Transform，就直接朝它衝刺；留空則根據 targetTag 自動尋找")]
    public Transform targetObject;
    [Tooltip("如果 targetObject 為空，會去找場景裡標籤為此值的物件")]
    public string targetTag = "Player";

    [Header("距離與高度")]
    [Tooltip("與目標保留的最小距離（≤此值時無動作）")]
    public float backOffDistance = 0.5f;
    [Tooltip("衝刺後在 Y 軸方向抬高，避免穿地面")]
    public float heightOffset = 0.5f;

    [Header("衝刺參數")]
    [Tooltip("整個衝刺過程持續時間（秒）")]
    public float dashDuration = 0.1f;
    [Tooltip("衝刺時是否暫時關閉碰撞")]
    public bool disableCollisions = true;

    private Rigidbody rb;
    private bool isDashing = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // 如果沒指定 targetObject，就靠標籤抓
        if (targetObject == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag(targetTag);
            if (go != null)
                targetObject = go.transform;
            else
                Debug.LogError($"[{name}] 找不到標籤為 '{targetTag}' 的物件。");
        }
    }

    void Update()
    {
        // 按到 triggerKey，而且目前不在衝刺中、又有目標時才執行
        if (Input.GetKeyDown(triggerKey) && !isDashing && targetObject != null)
        {
            // 只在距離大於 backOffDistance 時才衝刺
            float dist = Vector3.Distance(transform.position, targetObject.position);
            if (dist > backOffDistance)
            {
                Vector3 finalPos = ComputeDashTarget();
                StartDash(finalPos);
            }
        }
    }

    /// <summary>
    /// 計算最終要衝刺到的座標：目標往自己方向退 backOffDistance，再抬高 heightOffset
    /// </summary>
    private Vector3 ComputeDashTarget()
    {
        Vector3 toTarget = targetObject.position - transform.position;
        Vector3 dir = toTarget.normalized;
        Vector3 basePos = targetObject.position - dir * backOffDistance;
        return basePos + Vector3.up * heightOffset;
    }

    /// <summary>
    /// 公開方法：直接衝刺到指定點（可供外部呼叫）
    /// </summary>
    public void StartDash(Vector3 targetPos, Action onComplete = null)
    {
        if (isDashing) return;
        StartCoroutine(DashCoroutine(targetPos, onComplete));
    }

    private IEnumerator DashCoroutine(Vector3 targetPos, Action onComplete)
    {
        isDashing = true;

        // 暫時關閉碰撞
        bool origColl = rb.detectCollisions;
        if (disableCollisions) rb.detectCollisions = false;

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            float t = elapsed / dashDuration;
            Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);
            rb.MovePosition(newPos);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(targetPos);

        // 恢復碰撞設定
        if (disableCollisions) rb.detectCollisions = origColl;

        isDashing = false;
        onComplete?.Invoke();
    }
}
