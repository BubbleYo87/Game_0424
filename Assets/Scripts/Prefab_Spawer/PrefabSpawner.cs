using UnityEngine;
#if UNITY_AI_NAVIGATION || UNITY_2019_1_OR_NEWER
using UnityEngine.AI; // 若使用 NavMesh，需在 Project 有導入 AI Navigation
#endif

public class PrefabSpawner : MonoBehaviour
{
    [Header("要生成的 Prefab")]
    public GameObject prefab;

    [Header("生成中心（通常是一個空物件）")]
    public Transform spawnPoint;

    [Header("隨機半徑（XZ 平面）")]
    [Min(0f)] public float radius = 10f;

    [Header("生成鍵")]
    public KeyCode keyToPress = KeyCode.Space;

    [Header("隨機旋轉")]
    public bool randomYawOnly = true;          // 只隨機 Y 軸（常見於角色）
    public bool randomFullRotation = false;     // 勾選則使用任意 3D 旋轉（與上方互斥）
    
    [Header("地面對齊（可選）")]
    public bool alignToGround = true;           // 用 Raycast 落地
    public LayerMask groundMask = ~0;           // 地面圖層
    public float raycastHeight = 10f;           // 從目標上方多高往下打

#if UNITY_AI_NAVIGATION || UNITY_2019_1_OR_NEWER
    [Header("NavMesh（可選）")]
    public bool snapToNavMesh = false;          // 對齊到可行走 NavMesh
    public float navMeshSampleMaxDistance = 3f; // 探測半徑（越大越能找到可行走點）
    public int navMeshAreaMask = NavMesh.AllAreas;
#endif

    [Header("嘗試次數（避免障礙/無地面）")]
    [Tooltip("若某次隨機點無法落地/無NavMesh，會重試直到超過此次數")]
    [Min(1)] public int maxAttempts = 10;

    private void Update()
    {
        if (Input.GetKeyDown(keyToPress))
        {
            TrySpawnOnce();
        }
    }

    private void TrySpawnOnce()
    {
        if (!prefab)
        {
            Debug.LogWarning("[PrefabSpawner] Prefab 尚未設定！");
            return;
        }

        Vector3 center = spawnPoint ? spawnPoint.position : Vector3.zero;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 1) 在 XZ 平面上隨機一個圓內點
            Vector2 circle = Random.insideUnitCircle * radius;
            Vector3 candidate = new Vector3(center.x + circle.x, center.y, center.z + circle.y);

            // 2) 地面對齊（由上往下 Raycast）
            if (alignToGround)
            {
                Vector3 rayOrigin = candidate + Vector3.up * raycastHeight;
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    candidate = hit.point;
                }
                else
                {
                    // 這次失敗，換下一次嘗試
                    continue;
                }
            }

#if UNITY_AI_NAVIGATION || UNITY_2019_1_OR_NEWER
            // 3) NavMesh 對齊（可選）
            if (snapToNavMesh)
            {
                if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, navMeshSampleMaxDistance, navMeshAreaMask))
                {
                    candidate = navHit.position;
                }
                else
                {
                    // 找不到可行走區，換下一次嘗試
                    continue;
                }
            }
#endif

            // 4) 計算隨機旋轉
            Quaternion rot = Quaternion.identity;
            if (randomFullRotation)
            {
                rot = Random.rotation; // 任意 3D 旋轉
            }
            else if (randomYawOnly)
            {
                rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 只隨機 Y 軸
            }
            else
            {
                // 保持 spawnPoint 的旋轉（或世界對齊）
                rot = spawnPoint ? spawnPoint.rotation : Quaternion.identity;
            }

            // 5) 生成
            Instantiate(prefab, candidate, rot);
            return; // 成功後結束
        }

        Debug.LogWarning($"[PrefabSpawner] 在半徑 {radius} 內嘗試 {maxAttempts} 次仍無法找到合適位置（可能沒有地面或 NavMesh）。");
    }

    // Scene 視窗可視化半徑
    private void OnDrawGizmosSelected()
    {
        Vector3 c = spawnPoint ? spawnPoint.position : transform.position;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
        Gizmos.DrawSphere(c, radius);
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
        Gizmos.DrawWireSphere(c, radius);
    }
}
