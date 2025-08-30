using UnityEngine;

/// <summary>
/// 實體子彈：負責飛行、命中、依距離衰減傷害，並呼叫 IDamageable/IDamageableWithHit
/// 掛在「子彈 Prefab」上（需有 Rigidbody + Collider）
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("數值（由槍在生成時覆寫）")]
    [Tooltip("基礎傷害（近距離時）")]
    public float baseDamage = 20f;

    [Tooltip("開始衰減距離（<=此距離不衰減）")]
    public float falloffStart = 30f;

    [Tooltip("衰減至最低傷害的距離（>=此距離使用 minDamage）")]
    public float falloffEnd = 80f;

    [Tooltip("最低傷害（遠距離下限，不高於 baseDamage）")]
    public float minDamage = 8f;

    [Tooltip("子彈最大生存時間（秒），避免飛太久沒撞到東西")]
    public float lifeTime = 5f;

    [Header("飛行/特效（可選）")]
    [Tooltip("命中時生成的特效（例如火花/血花）；會在命中點生成並在數秒後自毀")]
    public GameObject hitVFX;
    public float hitVFXLife = 2f;

    [Tooltip("射手，用於避免剛生成時打到自己（可選）")]
    public Transform owner;

    [Tooltip("剛生成後這段時間忽略與 owner 的碰撞（秒）")]
    public float ignoreOwnerTime = 0.05f;

    private Rigidbody rb;
    private Vector3 spawnPos;
    private float spawnTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        spawnPos = transform.position;
        spawnTime = Time.time;
        CancelInvoke();
        Invoke(nameof(SelfDestruct), lifeTime);
    }

    private void FixedUpdate()
    {
        // 簡單的「忽略自己」：在剛生成的短時間內，若與 owner 太近就略過處理（真正的忽略可用 Physics.IgnoreCollision）
        if (owner && Time.time - spawnTime < ignoreOwnerTime)
        {
            float distToOwner = Vector3.Distance(owner.position, transform.position);
            // 不做事即可；主要依層級/碰撞矩陣避免自撞
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        // 命中資訊
        Vector3 hitPoint = other.contacts.Length > 0 ? other.contacts[0].point : transform.position;
        Vector3 hitNormal = other.contacts.Length > 0 ? other.contacts[0].normal : -transform.forward;

        // 計算距離衰減
        float travelDist = Vector3.Distance(spawnPos, hitPoint);
        float finalDamage = ComputeDamageWithFalloff(travelDist);

        // 先嘗試有命中點的介面 → 再降級到簡單介面
        var col = other.collider;
        // 先找自己 → 再找父物件
        IDamageableWithHit adv = col.GetComponent<IDamageableWithHit>() ?? col.GetComponentInParent<IDamageableWithHit>();
        if (adv != null)
        {
            adv.TakeDamage(finalDamage, hitPoint, hitNormal);
        }
        else
        {
            IDamageable simp = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
            if (simp != null)
            {
                simp.TakeDamage(finalDamage);
            }
            Debug.Log($"[Projectile] {other.gameObject.name} 沒有實作 IDamageableWithHit 介面，無法受傷");
        }

        // 命中特效（可選）
        if (hitVFX)
        {
            var vfx = Instantiate(hitVFX, hitPoint, Quaternion.LookRotation(hitNormal));
            Destroy(vfx, hitVFXLife);
        }

        // 子彈消失
        Destroy(gameObject);
    }

    private float ComputeDamageWithFalloff(float distance)
    {
        float dmg0 = baseDamage;
        float dmgMin = Mathf.Min(baseDamage, minDamage);

        if (distance <= falloffStart) return dmg0;
        if (distance >= falloffEnd) return dmgMin;

        float t = Mathf.InverseLerp(falloffStart, falloffEnd, distance);
        return Mathf.Lerp(dmg0, dmgMin, t);
    }

    private void SelfDestruct()
    {
        Destroy(gameObject);
    }
}
