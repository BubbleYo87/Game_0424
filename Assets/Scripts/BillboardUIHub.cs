using UnityEngine;

[ExecuteAlways]  // 在编辑器也能预览  
public class BillboardUIHub : MonoBehaviour
{
    [Tooltip("不填则自动用 Camera.main")]
    public Camera mainCamera;  

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCamera == null) return;

        // 遍历所有子物件，让它们朝向摄像机
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform bar = transform.GetChild(i);
            
            // 方式一：直接 LookAt 摄像机
            bar.LookAt(mainCamera.transform);

            // 如果你的模型面朝的是它的正 Z 轴之外的方向，需再旋转 180 度：
//          bar.Rotate(0, 180f, 0, Space.Self);

            // 方式二：只沿 Y 轴旋转（水平“锁高”）
//          Vector3 dir = bar.position - mainCamera.transform.position;
//          dir.y = 0;
//          if (dir.sqrMagnitude > 0.001f)
//              bar.rotation = Quaternion.LookRotation(dir);
        }
    }
}
