// Assets/Scripts/Interfaces/IDamageable.cs
using UnityEngine;

public interface IDamageable
{
    /// <summary>
    /// 受到傷害時呼叫
    /// </summary>
    /// <param name="amount">傷害數值</param>
    /// <param name="attacker">攻擊者的 GameObject（可選）</param>
    void TakeDamage(float amount, GameObject attacker = null);
}
