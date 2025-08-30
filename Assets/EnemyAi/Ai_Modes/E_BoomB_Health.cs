// Assets/Scripts/Modes/EnemyHealth.cs
using UnityEngine;

public class E_BoomB_Health : MonoBehaviour, IDamageableWithHit
{
    public float maxHP = 80f;
    public float hp;
    public Enemy_BoomB eb;

    void Awake() => hp = maxHP;

    // 玩家武器：優先呼叫這個（有命中點/法線）
    public float TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        hp -= amount;
        Debug.Log($"{gameObject.name} 被擊中 {amount}，剩餘 {hp}");
        // TODO: 在 hitPoint 產生血花/彈孔；依 hitNormal 做擊退/硬直
        if (hp <= 0f) Die();
        return amount;
    }

    private void Die()
    {
        // TODO: 掉落/特效/回收
        Destroy(gameObject, 5f);
        eb.Boom();
    }
}
