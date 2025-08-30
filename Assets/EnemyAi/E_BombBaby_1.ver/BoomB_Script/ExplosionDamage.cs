using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(Collider))]
public class ExplosionDamage : MonoBehaviour
{
    
    [Header("設定")]
    [Tooltip("進入 ExplosionDamage 時要造成的傷害值")]
    private float damage;
    [Tooltip("只對這些 Layer 生效（建議把 Player 放在這裡）")]
    public LayerMask targetLayers = ~0;
    // Start is called before the first frame update
    private void Reset()
    {
        // 確保是 Trigger
        GetComponent<Collider>().isTrigger = true;
    }

    /// <summary>在引爆前由外部（Enemy_BoomB）設定這次的傷害值。</summary>
    public void SetDamage(float value)
    {
        damage = value;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Layer 過濾（比只用 Tag 更穩）
        if (((1 << other.gameObject.layer) & targetLayers) == 0) return;

        // 以 attachedRigidbody 為準取得「玩家根物件」
        GameObject root = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;

        // 取得 IDamageable（本體/子/父 皆可）
        IDamageable dmg =
            root.GetComponent<IDamageable>() ??
            root.GetComponentInChildren<IDamageable>() ??
            root.GetComponentInParent<IDamageable>();

        if (dmg != null)
        {
            dmg.TakeDamage(damage);
        }
    }
}
