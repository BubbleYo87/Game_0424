using UnityEngine;

public class BoomB_AttakcToBoom : MonoBehaviour
{
    [Tooltip("指向場上那個要呼叫 Boom() 的 Enemy_BoomB")]
    public Enemy_BoomB targetEnemy;
    // Start is called before the first frame update
    void AttackToBoom()
    {
        if (targetEnemy != null)
            targetEnemy.Boom();
        else
            Debug.LogWarning("ATB未指定 targetEnemy，無法呼叫 Boom()");
    }
}
