using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 控制玩家衝刺（Dashing）行為的腳本
/// </summary>
public class Dashing : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;         // 角色面向方向（通常是角色身體的前方）
    public Transform playerCam;           // 玩家攝影機
    private Rigidbody rb;                 // 角色剛體
    private PlayerMovementGrappling pm;   // 玩家移動控制腳本

    [Header("Dashing")]
    public float dashForce;               // 衝刺前方推力
    public float dashUpwardForce;         // 衝刺向上推力
    public float maxDashYSpeed;           // 衝刺時Y軸最大速度
    public float dashDuration;            // 衝刺持續時間

    [Header("CameraEffects")]
    public PlayerCam cam;                 // 攝影機控制腳本
    public float dashFov;                 // 衝刺時的視野範圍（FOV）

    [Header("Settings")]
    public bool useCameraForward = true;  // 是否以攝影機面向為衝刺方向
    public bool allowAllDirections = true;// 是否允許全方向衝刺（WASD 控制）
    public bool disableGravity = false;   // 衝刺時是否關閉重力
    public bool resetVel = true;          // 衝刺前是否重置速度

    [Header("Cooldown")]
    public float dashCd;                  // 衝刺冷卻時間
    private float dashCdTimer;            // 冷卻計時器

    [Header("Input")]
    public KeyCode dashKey = KeyCode.E;   // 衝刺按鍵

    // 初始化
    private void Start()
    {
        rb = GetComponent<Rigidbody>();                   // 取得剛體
        pm = GetComponent<PlayerMovementGrappling>();     // 取得玩家移動控制腳本
    }

    // 每幀更新
    private void Update()
    {
        // 按下衝刺鍵時觸發 Dash()
        if (Input.GetKeyDown(dashKey))
            Dash();

        // 冷卻計時
        if (dashCdTimer > 0)
            dashCdTimer -= Time.deltaTime;
    }

    /// <summary>
    /// 衝刺主邏輯
    /// </summary>
    private void Dash()
    {
        // 冷卻未結束則跳出
        if (dashCdTimer > 0) return;
        else dashCdTimer = dashCd; // 重置冷卻

        pm.dashing = true;                     // 設定 dashing 狀態
        pm.maxYSpeed = maxDashYSpeed;          // 設定 Y 軸最大速度

        cam.DoFov(dashFov);                    // 攝影機切換為衝刺視野

        Transform forwardT;

        // 決定以攝影機朝向還是角色朝向為主
        if (useCameraForward)
            forwardT = playerCam;              // 以攝影機為主
        else
            forwardT = orientation;            // 以角色本體朝向為主

        Vector3 direction = GetDirection(forwardT); // 計算移動方向

        // 合成最終要施加的力（方向+向上）
        Vector3 forceToApply = direction * dashForce + orientation.up * dashUpwardForce;

        if (disableGravity)
            rb.useGravity = false;             // 關閉重力（如需）

        delayedForceToApply = forceToApply;
        Invoke(nameof(DelayedDashForce), 0.025f);      // 稍微延遲施加推力，避免與其它動作衝突

        Invoke(nameof(ResetDash), dashDuration);        // 衝刺結束後重置狀態
    }

    private Vector3 delayedForceToApply;

    /// <summary>
    /// 延遲一點點再真正施加衝刺力
    /// </summary>
    private void DelayedDashForce()
    {
        if (resetVel)
            rb.velocity = Vector3.zero;        // 衝刺前重置速度，確保力道純粹

        rb.AddForce(delayedForceToApply, ForceMode.Impulse); // 施加衝刺力
    }

    /// <summary>
    /// 衝刺結束後重置狀態
    /// </summary>
    private void ResetDash()
    {
        pm.dashing = false;            // 關閉 dashing 狀態
        pm.maxYSpeed = 0;              // 恢復 Y 軸速度限制

        cam.DoFov(60f);                // 攝影機視野恢復預設

        if (disableGravity)
            rb.useGravity = true;      // 恢復重力
    }

    /// <summary>
    /// 根據輸入決定移動方向
    /// </summary>
    /// <param name="forwardT">基準朝向</param>
    /// <returns>歸一化方向向量</returns>
    private Vector3 GetDirection(Transform forwardT)
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal"); // A/D
        float verticalInput = Input.GetAxisRaw("Vertical");     // W/S

        Vector3 direction = new Vector3();

        if (allowAllDirections)
            direction = forwardT.forward * verticalInput + forwardT.right * horizontalInput; // 支援所有方向
        else
            direction = forwardT.forward;    // 只支援正前方

        // 無輸入時仍向前
        if (verticalInput == 0 && horizontalInput == 0)
            direction = forwardT.forward;

        return direction.normalized;
    }
}
