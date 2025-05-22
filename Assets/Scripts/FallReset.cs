using UnityEngine;
using UnityEngine.SceneManagement;

public class FallReset : MonoBehaviour
{
    [Tooltip("玩家 Y 座標低於此值時重新載入場景")]
    public float fallThreshold = -50f;

    void Update()
    {
        if (transform.position.y < fallThreshold)
        {
            // 重新載入目前場景
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
