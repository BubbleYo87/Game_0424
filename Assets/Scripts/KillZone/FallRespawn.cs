// FallRespawn.cs  掛在玩家上
using UnityEngine;

public class FallRespawn : MonoBehaviour
{
    public float fallY = -20f;

    void Update()
    {
        if (transform.position.y < fallY)
        {
            var hp = GetComponent<PlayerHealth>();
            if (hp != null) hp.Die();           // 統一走死亡→重生流程
            else RespawnManager.Instance?.Respawn(gameObject);
        }
    }
}
