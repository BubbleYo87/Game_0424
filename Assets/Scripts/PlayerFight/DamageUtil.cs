// Assets/Scripts/Combat/DamageUtil.cs
using UnityEngine;

public static class DamageUtil
{
    /// 玩家攻擊：盡量帶命中資訊；若對方沒實作進階，就降級為純數值
    public static float ApplyDamageWithHit(GameObject target, float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (target.TryGetComponent<IDamageableWithHit>(out var adv))
            return adv.TakeDamage(amount, hitPoint, hitNormal);

        if (target.TryGetComponent<IDamageable>(out var simple))
        {
            simple.TakeDamage(amount);
            return amount;
        }
        return 0f;
    }
}
