// ObjectDashController.cs
using UnityEngine;

[RequireComponent(typeof(DashMover))]
public class ObjectDashController : MonoBehaviour
{
    [Header("觸發按鍵")]
    [Tooltip("按下這個鍵才會啟動衝刺")]
    public KeyCode triggerKey = KeyCode.T;

    [Header("目標設定")]
    [Tooltip("衝刺到哪個物件")]
    private Transform targetObject;
    [Tooltip("與目標保留的最小距離（<=0 表示貼到目標）")]
    public float backOffDistance = 0.5f;
    [Tooltip("衝刺後抬高，避免穿地面")]
    public float heightOffset = 0.5f;

    private DashMover dashMover;

    void Awake()
    {
        dashMover = GetComponent<DashMover>();
        // 自動尋找場景中「Player」標籤的物件，並取得它的 Transform
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            targetObject = playerGO.transform;
        }
        else
        {
            Debug.LogError($"[{name}] 找不到標籤為 'Player' 的物件，請確認場景中有標記 Player 標籤。");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(triggerKey) && targetObject != null)
        {
            Vector3 finalPos = ComputeDashTarget();
            dashMover.StartDash(finalPos);
        }
    }

    // 計算最終要衝刺到的座標
    private Vector3 ComputeDashTarget()
    {
        Vector3 toTarget = targetObject.position - transform.position;
        float dist = toTarget.magnitude;

        // 如果已經在保留距離內，就回傳目前位置（不動作）
        if (dist <= backOffDistance)
            return transform.position;

        // 方向向量
        Vector3 dir = toTarget.normalized;
        // 往回推 backOffDistance
        Vector3 basePos = targetObject.position - dir * backOffDistance;
        // 再加上高度偏移
        return basePos + Vector3.up * heightOffset;
    }
}
