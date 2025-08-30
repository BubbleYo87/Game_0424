using UnityEngine;

public class EnemyHitboxDriver : MonoBehaviour
{
    [Tooltip("在攻擊期間啟用的 Hitbox（Trigger Collider）")]
    public Collider[] hitboxes;

    public void Hitbox_On()
    {
        foreach (var c in hitboxes) if (c) c.enabled = true;
    }

    public void Hitbox_Off()
    {
        foreach (var c in hitboxes) if (c) c.enabled = false;
    }
}
