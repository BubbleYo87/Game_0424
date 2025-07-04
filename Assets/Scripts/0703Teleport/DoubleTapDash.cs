// DoubleTapDash.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class DoubleTapDash : MonoBehaviour
{
    [Header("攝影機參考")]
    [Tooltip("用來判斷移動方向的攝影機")]
    public Camera playerCamera;

    [Header("連擊判定")]
    [Tooltip("兩次按鍵間隔的最大時間（秒）")]
    public float doubleTapTime = 0.3f;
    private KeyCode lastTapKey = KeyCode.None;
    private float lastTapTimeStamp;

    [Header("Dash 設定")]
    [Tooltip("Dash 持續時間（秒）")]
    public float dashDuration = 0.1f;
    [Tooltip("最大 Dash 距離（世界單位）")]
    public float dashDistance = 2f;
    [Tooltip("Dash 時是否暫時關閉碰撞")]
    public bool disableCollisions = true;
    [Tooltip("當檢測到碰撞時，從障礙前退多遠（避免貼到牆上）")]
    public float collisionOffset = 0.1f;

    [Header("高度偏移")]
    [Tooltip("Dash 最終位置在 Y 軸上抬高，避免穿地面")]
    public float heightOffset = 0.5f;

    private Rigidbody rb;
    private bool isDashing;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (playerCamera == null)
            Debug.LogError("請在 Inspector 指定 playerCamera！");
    }

    void Update()
    {
        if (isDashing) return;

        TryCheckDoubleTap(KeyCode.W);
        TryCheckDoubleTap(KeyCode.S);
        TryCheckDoubleTap(KeyCode.A);
        TryCheckDoubleTap(KeyCode.D);
    }

    private void TryCheckDoubleTap(KeyCode key)
    {
        if (!Input.GetKeyDown(key)) return;

        float elapsed = Time.time - lastTapTimeStamp;
        if (lastTapKey == key && elapsed <= doubleTapTime)
        {
            // 1. 計算攝影機在水平面上的 forward/right
            Vector3 camF = playerCamera.transform.forward; camF.y = 0; camF.Normalize();
            Vector3 camR = playerCamera.transform.right;   camR.y = 0; camR.Normalize();
            Vector3 dir = Vector3.zero;
            switch (key)
            {
                case KeyCode.W: dir = camF; break;
                case KeyCode.S: dir = -camF; break;
                case KeyCode.A: dir = -camR; break;
                case KeyCode.D: dir = camR; break;
            }

            // 2. 前向碰撞偵測 (SweepTest)
            float actualDistance = dashDistance;
            if (rb.SweepTest(dir, out RaycastHit hitInfo, dashDistance + collisionOffset))
            {
                // 避開 collisionOffset，確保不穿透
                actualDistance = hitInfo.distance - collisionOffset;
            }

            // 3. 如太短或負值，就不 Dash
            if (actualDistance <= 0f)
            {
                Debug.Log("前方障礙太近，無法執行 Dash");
                lastTapKey = KeyCode.None;
                return;
            }

            // 4. (Debug) 顯示方向與距離
            Debug.DrawRay(transform.position, dir * actualDistance, Color.green, 1f);
            Debug.Log($"DoubleTap {key} ⇒ dir={dir} dist={actualDistance:F2}");

            // 5. 計算目標位置 (水平 + 抬高)
            Vector3 startPos  = transform.position;
            Vector3 targetPos = startPos + dir * actualDistance + Vector3.up * heightOffset;

            // 6. 啟動 Dash
            StartCoroutine(DashCoroutine(startPos, targetPos));

            lastTapKey = KeyCode.None;
        }
        else
        {
            lastTapKey = key;
            lastTapTimeStamp = Time.time;
        }
    }

    private IEnumerator DashCoroutine(Vector3 startPos, Vector3 targetPos)
    {
        isDashing = true;
        bool origColl = rb.detectCollisions;
        if (disableCollisions) rb.detectCollisions = false;

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            float t = elapsed / dashDuration;
            rb.MovePosition(Vector3.Lerp(startPos, targetPos, t));
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(targetPos);
        if (disableCollisions) rb.detectCollisions = origColl;
        isDashing = false;
    }
}


