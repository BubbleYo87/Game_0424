using UnityEngine;

public class BoomTriggerCaller : MonoBehaviour
{
    [Tooltip("指向場上那個要呼叫 Boom() 的 Enemy_BoomB")]
    public Enemy_BoomB targetEnemy;  

    // 若你用的是 Trigger Collider，Collider.IsTrigger = true
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (targetEnemy != null)
                targetEnemy.Boom();
            else
                Debug.LogWarning("BTC未指定 targetEnemy，無法呼叫 Boom()");
        }
    }
}
