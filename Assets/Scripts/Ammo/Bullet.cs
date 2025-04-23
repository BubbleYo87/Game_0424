using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 30f;               // 子彈初速
    public int maxBounces = 2;              // 最大反彈次數
    public float lifeTime = 5f;             // 最長存在時間（保險）

    private int bounceCount = 0;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 初速：沿著 forward 推出去
        rb.velocity = transform.forward * speed;
        
        // ✅ 忽略與玩家的碰撞
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Collider bulletCol = GetComponent<Collider>();
            Collider[] playerCols = player.GetComponentsInChildren<Collider>();

            foreach (var pc in playerCols)
            {
                Physics.IgnoreCollision(bulletCol, pc);
            }
        }

        // 最長存在時間（自毀保險）
        Destroy(gameObject, lifeTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        // ✅ 檢查是否還能反彈
        if (bounceCount < maxBounces)
        {
            // 計算反射方向
            Vector3 incomingVelocity = rb.velocity;
            Vector3 normal = collision.contacts[0].normal;
            Vector3 reflectedVelocity = Vector3.Reflect(incomingVelocity, normal);

            rb.velocity = reflectedVelocity;

            bounceCount++;
        }
        else
        {
            // 超過反彈次數，自毀
            Destroy(gameObject);
        }
    }
}
