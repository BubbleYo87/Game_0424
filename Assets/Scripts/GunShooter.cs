using UnityEngine;

public class GunShooter : MonoBehaviour
{
    public GameObject bulletPrefab;     // 子彈預製體
    public Transform firePoint;         // 槍口位置
    public float bulletSpeed = 30f;

    public Camera cam;                  // 📷 攝影機（請拖進來）

    public void Shoot()
    {
        if (cam == null)
        {
            Debug.LogWarning("請在 GunShooter 指定 Camera！");
            return;
        }

        // 1️⃣ 從畫面中央射出一條射線（crosshair 射線）
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); // 螢幕中央
        Vector3 targetDirection;

        // 2️⃣ 嘗試射線偵測，看是否打中東西
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            targetDirection = (hit.point - firePoint.position).normalized;
        }
        else
        {
            // 沒打中 → 射向前方 100 單位處
            targetDirection = (ray.GetPoint(100f) - firePoint.position).normalized;
        }

        // 3️⃣ 生成子彈，朝正確方向飛
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(targetDirection));
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = targetDirection * bulletSpeed;
        }

        // 可加上：拖尾、閃光、音效
    }
}
