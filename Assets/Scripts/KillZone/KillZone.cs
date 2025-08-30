// KillZone.cs （改用 IDamageable 版）
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("進入 KillZone 時要造成的傷害值")]
    public float damage = 25f;

    private void Reset()
    {
        // 確保是 Trigger
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 只對 Player 觸發（如需對所有可受傷物件觸發，可移除這行判斷）
        if (!other.CompareTag("Player")) return;

        // 以 attachedRigidbody 為準取得「玩家根物件」
        GameObject root = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;

        // 嘗試在 本體 / 子物件 / 父物件 取得 IDamageable
        IDamageable dmg =
            root.GetComponent<IDamageable>() ??
            root.GetComponentInChildren<IDamageable>() ??
            root.GetComponentInParent<IDamageable>();

        if (dmg != null)
        {
            // 找到可受傷介面 → 直接扣血
            dmg.TakeDamage(damage);
        }
/*         else
        {
            // 找不到可受傷介面 → 視為玩家需要被重生
            RespawnManager.Instance?.Respawn(root);
        } */
    }
}
