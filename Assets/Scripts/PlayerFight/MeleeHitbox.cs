// Assets/Scripts/Combat/MeleeHitbox.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class MeleeHitbox : MonoBehaviour
{
    [Header("設定")]
    public LayerMask targetLayers = ~0;     // 只打這些 Layer
    public Transform ownerRoot;             // 發動者（避免打到自己，可不填）
    public float debugGizmoSeconds = 0.2f;  // 命中點Debug顯示時間

    private bool canHit = false;
    private float damage = 10f;
    private readonly HashSet<GameObject> hitThisSwing = new();

    private Collider col;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        EnableHit(false);
    }

    public void Configure(float dmg, bool enable, Transform owner)
    {
        damage = dmg;
        EnableHit(enable);
        ownerRoot = owner;
        if (enable) hitThisSwing.Clear();
    }

    public void EnableHit(bool enable)
    {
        canHit = enable;
        // 你也可以選擇啟/關 Collider，本範例只用旗標控制邏輯
        // col.enabled = enable;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canHit) return;

        // 1) Layer 過濾
        if (((1 << other.gameObject.layer) & targetLayers) == 0) return;

        // 2) 忽略自己（若有填 ownerRoot）
        if (ownerRoot && other.transform.IsChildOf(ownerRoot)) return;

        // 3) 避免同一揮對同一物件多次結算
        var targetGO = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        if (hitThisSwing.Contains(targetGO)) return;

        // 4) 命中點/法線（以 Hitbox 與對方相對關係估算）
        Vector3 point  = other.ClosestPoint(transform.position);
        Vector3 normal = (other.transform.position - transform.position).normalized;

        // 5) 套用傷害（玩家攻擊 → 優先帶命中點）
        DamageUtil.ApplyDamageWithHit(targetGO, damage, point, normal);

#if UNITY_EDITOR
        // Debug 視覺化（Scene 視窗）
        Debug.DrawRay(point, normal, Color.red, debugGizmoSeconds);
#endif

        hitThisSwing.Add(targetGO);
    }
}
