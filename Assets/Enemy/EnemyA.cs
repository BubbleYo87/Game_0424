using UnityEngine;

public class EnemyA : MonoBehaviour
{
    public float detectionRadius = 10f;
    public float viewAngle = 120f;
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float minFireRate = 0.5f;  // 最快射速（單位：每秒幾發）
    public float maxFireRate = 2.5f;  // 最慢射速
    public float fireRate = 1f;       // 當前射速
    private float lastFireTime;
    public LayerMask playerMask;
    public float turnSpeed = 5f;

    [Header("射線預警")]
    public LineRenderer lineRendererPrefab;
    public float laserDuration = 0.1f;
    public Color laserColor = Color.red;

    [Header("被擊中次數")]
    public int hitCountToDie = 2; // 幾發子彈死
    public int hitCount = 0;     // 當前被擊中次數


    void Update()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerMask);
        foreach (Collider hit in hits)
        {
            Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
            Vector3 flatDir = new Vector3(dirToTarget.x, 0, dirToTarget.z);

            if (Vector3.Angle(transform.forward, flatDir) < viewAngle / 2f)
            {
                if (flatDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(flatDir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }

                if (Time.time - lastFireTime > 1f / fireRate)
                {
                    Shoot(hit.transform.position);
                    lastFireTime = Time.time;
                    fireRate = Random.Range(minFireRate, maxFireRate); // 🔥 這裡隨機新射速
                }
            }
        }
    }

    void Shoot(Vector3 targetPosition)
    {
        StartCoroutine(ShootWithLaser(targetPosition));
    }

    private System.Collections.IEnumerator ShootWithLaser(Vector3 targetPosition)
    {
        ShowLaser(targetPosition);

        yield return new WaitForSeconds(laserDuration);

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        bullet.transform.forward = (targetPosition - firePoint.position).normalized;
    }

    void ShowLaser(Vector3 targetPosition)
    {
        LineRenderer lr = Instantiate(lineRendererPrefab, firePoint.position, Quaternion.identity);

        lr.SetPosition(0, firePoint.position);
        lr.SetPosition(1, targetPosition);

        lr.startColor = lr.endColor = laserColor;
        lr.startWidth = lr.endWidth = 0.05f;

        Destroy(lr.gameObject, laserDuration);
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("P_Bullet"))
        {
            hitCount++;

            if (hitCount >= hitCountToDie)
            {
                Destroy(gameObject);  // 敵人被打2次自爆
            }
        }
    }
}
