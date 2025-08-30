// Assets/Scripts/Interfaces/IDamageable.cs
using UnityEngine;

/// <summary>
/// 進階受傷介面：包含命中點與法線（用於血花/擊退/爆頭判定）
/// 建議：敵人、可破壞物有需要就實作這個
/// </summary>
public interface IDamageableWithHit
{
    float TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal);
}
