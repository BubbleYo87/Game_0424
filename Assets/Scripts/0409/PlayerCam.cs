using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using DG.Tweening; // 裡面有補間動畫功能（你暫時註解掉了）

public class PlayerCam : MonoBehaviour
{
    // 滑鼠靈敏度設定
    public float sensX;
    public float sensY;
    public float multiplier; //靈敏度

    // 相機與角色方向的參考
    public Transform orientation; // 控制角色朝向
    public Transform camHolder;   // 控制相機角度

    // 內部變數：記錄目前的旋轉角度
    float xRotation;
    float yRotation;

    [Header("Fov")]
    // 是否使用流暢的視角變化
    public bool useFluentFov;
    public PlayerMovementTutorial pm; // 角色移動腳本參考（目前這段代碼沒用到）
    public Rigidbody rb;              // 角色的剛體，用於取得速度
    public Camera cam;                // 相機本體

    // 視角變化的參數範圍
    public float minMovementSpeed; // 移動速度下限
    public float maxMovementSpeed; // 移動速度上限
    public float minFov;           // 視野下限
    public float maxFov;           // 視野上限

    private void Start()
    {
        // 鎖定滑鼠游標，並隱藏
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        multiplier = 0.01f;
    }

    private void Update()
    {
        // 讀取滑鼠輸入
        float mouseX = Input.GetAxisRaw("Mouse X") * sensX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensY;

        // 計算左右旋轉（水平）
        yRotation += mouseX * multiplier;

        // 計算上下旋轉（垂直）
        xRotation -= mouseY * multiplier;

        // 限制上下旋轉角度，避免相機翻轉
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // 應用旋轉到相機和角色方向
        camHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);

        // 如果啟用了流暢視角變化，執行 FOV 處理
        if (useFluentFov) HandleFov();
    }

    // 根據角色速度調整視野（FOV），讓速度感更強
    private void HandleFov()
    {
        // 計算速度差值與視野差值
        float moveSpeedDif = maxMovementSpeed - minMovementSpeed;
        float fovDif = maxFov - minFov;

        // 計算角色的水平速度（忽略 Y 軸）
        float rbFlatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;

        // 目前速度超過最小速度多少
        float currMoveSpeedOvershoot = rbFlatVel - minMovementSpeed;

        // 計算速度進度百分比（用於插值）
        float currMoveSpeedProgress = currMoveSpeedOvershoot / moveSpeedDif;

        // 根據進度百分比計算目標 FOV
        float fov = (currMoveSpeedProgress * fovDif) + minFov;

        // 當前相機的 FOV
        float currFov = cam.fieldOfView;

        // 緩慢插值到目標 FOV，讓變化更平滑
        float lerpedFov = Mathf.Lerp(fov, currFov, Time.deltaTime * 200);

        // 設定相機的 FOV
        cam.fieldOfView = lerpedFov;
    }

    /* 這裡是暫時被註解掉的功能，可以搭配 DOTween 插件使用，做出更流暢的效果
    // 用於快速變化視野效果
    public void DoFov(float endValue)
    {
        GetComponent<Camera>().DOFieldOfView(endValue, 0.25f);
    }

    // 用於相機傾斜效果（例如角色側移時視角傾斜）
    public void DoTilt(float zTilt)
    {
        transform.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f);
    }
    */
    public void DoFov(float endValue)
    {
        GetComponent<Camera>().DOFieldOfView(endValue, 0.25f);
    }
}
