using UnityEngine;

public class EnemyA : MonoBehaviour
{
    public float detectionRadius = 10f;          // 敵人視野半徑
    public float viewAngle = 90f;                // 視野扇形角度
    public Transform firePoint;                  // 子彈發射點
    public GameObject bulletPrefab;              // 子彈預製物
    public float fireRate = 1f;                  // 每秒射擊次數
    public LayerMask playerMask;                 // 玩家所在圖層

    private float lastFireTime;                  // 上一次射擊時間

    void Update()
    {
        // 用球形碰撞檢測範圍內是否有玩家
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerMask);
        foreach (Collider hit in hits)
        {
            // 計算敵人正前方到玩家的方向向量
            Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
            // 判斷玩家是否在扇形視野內
            if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2f)
            {
                Debug.Log("EA");
                // 檢查射擊間隔，控制射速
                if (Time.time - lastFireTime > 1f / fireRate)
                {
                    Shoot(hit.transform.position); // 朝玩家射擊
                    lastFireTime = Time.time;      // 記錄射擊時間
                }
            }
        }
    }

    void Shoot(Vector3 targetPosition)
    {
        // 產生子彈並設定方向
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        bullet.transform.forward = (targetPosition - firePoint.position).normalized;
        // 子彈的移動與傷害由 Bullet 腳本處理
    }
}
