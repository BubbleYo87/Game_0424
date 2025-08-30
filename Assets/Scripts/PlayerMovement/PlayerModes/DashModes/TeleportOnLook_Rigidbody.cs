// TeleportOnLook_Rigidbody.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TeleportOnLook_Rigidbody : MonoBehaviour
{
    [Header("射線設定")]
    [Tooltip("用來發射射線檢測點的攝影機")]
    public Camera playerCamera;

    [Header("瞬移參數")]
    [Tooltip("瞬移至目標的最高距離")]
    public float maxDistance = 50f;
    [Tooltip("瞬移後抬高，避免卡地面")]
    public float heightOffset = 1f;
    [Tooltip("可以瞬移的圖層遮罩")]
    public LayerMask teleportLayerMask = ~0;
    [Tooltip("在命中點往回推多遠，保留與目標的距離")]
    public float backOffDistance = 2f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            Debug.LogError("TeleportOnLook_Rigidbody 需要掛在有 Rigidbody 的物件上！");
        if (playerCamera == null)
            Debug.LogError("請在 Inspector 指定 playerCamera！");
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryTeleport();
    }

    private void TryTeleport()
    {
        // 從攝影機往前發出射線
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, teleportLayerMask))
        {
            // **新增檢查：如果與命中點距離小於 backOffDistance，就不做任何動作**
            float distanceToHit = Vector3.Distance(transform.position, hit.point);
            if (distanceToHit <= backOffDistance)
                return;

            // 計算從命中點往回推的基礎位置
            Vector3 dir = (hit.point - playerCamera.transform.position).normalized;
            Vector3 basePos = hit.point - dir * backOffDistance;
            // 再加上抬高量
            Vector3 targetPos = basePos + Vector3.up * heightOffset;

            // 暫時關閉碰撞，避免瞬移瞬間卡住
            bool originalDetect = rb.detectCollisions;
            rb.detectCollisions = false;

            // 設定 Rigidbody 與 Transform 位置
            rb.position = targetPos;
            transform.position = targetPos;

            // 恢復碰撞設定
            rb.detectCollisions = originalDetect;
        }
    }
}
