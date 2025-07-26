using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StoneCollisionDestroy : MonoBehaviour
{
    // 如果你用的是 Trigger（勾選 Collider.isTrigger）
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }
}
