using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;


/// <summary>
/// 玩家移動與鉤爪功能核心腳本
/// </summary>
public class PlayerMovementGrappling : MonoBehaviour
{
    // ----------------------- //
    //        參數宣告        //
    // ----------------------- //
    [Header("Movement")]
    private float moveSpeed;         // 當前移動速度
    public float walkSpeed;          // 行走速度
    public float sprintSpeed;        // 跑步速度
    public float dashSpeed;          // 衝刺速度
    public float dashSpeedChangeFactor; // 衝刺時加速因子
    public float climbingSpeed;      // 攀爬速度
    public float groundDrag;         // 地面摩擦力
    public float maxYSpeed;          // 最大Y軸速度（防止飛太高）

    [Header("Jumping")]
    public float jumpForce;          // 跳躍力
    public float jumpCooldown;       // 跳躍冷卻
    public float airMultiplier;      // 空中移動速度倍率
    bool readyToJump;                // 是否可跳

    [Header("Crouching")]
    public float crouchSpeed;        // 蹲下速度
    public float crouchYScale;       // 蹲下時Y縮放
    private float startYScale;       // 站立時Y縮放

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;    // 跳躍鍵
    public KeyCode sprintKey = KeyCode.LeftShift; // 跑步鍵
    public KeyCode crouchKey = KeyCode.LeftControl; // 蹲下鍵

    [Header("Ground Check")]
    public float playerHeight;       // 玩家高度（偵測用）
    public LayerMask whatIsGround;   // 哪些是地面
    public bool grounded;            // 是否落地

    [Header("Slope Handling")]
    public float maxSlopeAngle;      // 可行走最大坡度
    private RaycastHit slopeHit;     // 碰撞資訊
    private bool exitingSlope;       // 是否正在離開斜坡

    [Header("Camera Effects")]
    public PlayerCam cam;            // 攝影機控制腳本
    public float grappleFov = 95f;   // 勾爪視角

    // 玩家輸入
    float horizontalInput;           // 水平輸入
    float verticalInput;             // 垂直輸入

    Vector3 moveDirection;           // 移動方向
    Rigidbody rb;                    // 剛體

    // 狀態列舉
    public MovementState state;
    public enum MovementState
    {
        freeze,         // 禁止移動
        walking,        // 行走
        sprinting,      // 跑步
        crouching,      // 蹲下
        air ,            // 空中
        climbing,       // 攀爬
        dashing         // 衝刺
    }

    public bool freeze;          // 是否凍結移動
    public bool climbing;        // 是否攀爬
    public bool dashing;         // 是否衝刺


    // ------------------------ //
    //        Unity 事件        //
    // ------------------------ //

    // 初始化
    private void Start()
    {
        rb = GetComponent<Rigidbody>();      // 取得剛體
        rb.freezeRotation = true;            // 鎖定旋轉
        readyToJump = true;                  // 可跳
        startYScale = transform.localScale.y;// 初始Y縮放
    }

    // 每幀更新
    private void Update()
    {

        // 落地檢查
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        MyInput();          // 處理玩家輸入
        SpeedControl();     // 控制速度上限
        StateHandler();     // 狀態管理

        // 摩擦力調整
        if (state == MovementState.walking || state == MovementState.sprinting || state == MovementState.crouching)
            rb.drag = groundDrag;
        else
            rb.drag = 0;

        TextStuff();        // 更新UI顯示（速度/狀態）
    }

    // 物理更新
    private void FixedUpdate()
    {
        MovePlayer(); // 執行移動
    }

    // --------------------------- //
    //        主要邏輯方法         //
    // --------------------------- //

    /// <summary>
    /// 處理玩家輸入
    /// </summary>
    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // **改造跳跃逻辑** **
        if (Input.GetKeyDown(jumpKey) && readyToJump)
        {
            // 普通起跳
            if (grounded)
            {
                Jump();
                Invoke(nameof(ResetJump), jumpCooldown);
            }
        }

        // 開始蹲下
        if (Input.GetKeyDown(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }

        // 停止蹲下
        if (Input.GetKeyUp(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
    }

    // 狀態處理相關變數
    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    private MovementState lastState;
    private bool keepMomentum;
    private float speedChangeFactor;

    /// <summary>
    /// 管理玩家移動狀態
    /// </summary>
    private void StateHandler()
    {
        // 禁止移動
        if (freeze)
        {
            state = MovementState.freeze;
            moveSpeed = 0;
            rb.velocity = Vector3.zero;
        }
        // 蹲下
        else if (Input.GetKey(crouchKey))
        {
            state = MovementState.crouching;
            desiredMoveSpeed = crouchSpeed;
        }
        // 跑步
        else if (grounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
            desiredMoveSpeed = sprintSpeed;
        }
        // 行走
        else if (grounded && !dashing)
        {
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }
        // 攀爬
        else if (climbing)
        {
            state = MovementState.climbing;
            desiredMoveSpeed = climbingSpeed;
        }
        // 衝刺
        else if (dashing)
        {
            state = MovementState.dashing;
            desiredMoveSpeed = dashSpeed;
            speedChangeFactor = dashSpeedChangeFactor;
        }
        // 空中
        else
        {
            state = MovementState.air;
            if (desiredMoveSpeed < sprintSpeed)
                desiredMoveSpeed = walkSpeed;
            else
                desiredMoveSpeed = sprintSpeed;
        }

        // 移速變化/動量管理
        bool DesiredMoveSpeedHasChange = desiredMoveSpeed != lastDesiredMoveSpeed;
        if (lastState == MovementState.dashing) keepMomentum = true;

        if (DesiredMoveSpeedHasChange)
        {
            if (keepMomentum)
            {
                StopAllCoroutines();
                StartCoroutine(SmoothlyLerpMoveSpeed());
            }
            else
            {
                StopAllCoroutines();
                moveSpeed = desiredMoveSpeed;
            }
        }

        lastDesiredMoveSpeed = desiredMoveSpeed;
        lastState = state;
    }

    /// <summary>
    /// 平滑移動速度變化協程
    /// </summary>
    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        float time = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
        float startValue = moveSpeed;
        float boostFactor = speedChangeFactor;

        while (time < difference)
        {
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);
            time += Time.deltaTime * boostFactor;
            yield return null;
        }

        moveSpeed = desiredMoveSpeed;
        speedChangeFactor = 1f;
        keepMomentum = false;
    }

    /// <summary>
    /// 處理實際移動
    /// </summary>
    private void MovePlayer()
    {

        // 計算移動方向
        // moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // 斜坡移動
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 20f, ForceMode.Force);

            // 在斜坡往上時施加向下力，避免飛出去
            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
        // 地面
        else if (grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        // 空中
        else if (!grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

        // 斜坡時關閉重力
        rb.useGravity = !OnSlope();
    }

    /// <summary>
    /// 控制最大移動速度
    /// </summary>
    private void SpeedControl()
    {
        // 斜坡速度限制
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }
        // 地面/空中速度限制
        else
        {
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }

        // 限制Y軸速度（防止飛太高）
        if (maxYSpeed != 0 && rb.velocity.y > maxYSpeed)
            rb.velocity = new Vector3(rb.velocity.x, maxYSpeed, rb.velocity.z);
    }
    /// <summary>
    /// 跳躍
    /// </summary>
    private void Jump()
    {
        exitingSlope = true;
        // 重設Y速度
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        // 施加跳躍力
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    /// <summary>
    /// 跳躍冷卻重置
    /// </summary>
    private void ResetJump()
    {
        readyToJump = true;
        exitingSlope = false;
    }

    private bool enableMovementOnNextTouch;

    // 強制設置速度
    private Vector3 velocityToSet;
    private void SetVelocity()
    {
        enableMovementOnNextTouch = true;
        rb.velocity = velocityToSet;
        //cam.DoFov(grappleFov); // 可搭配視角動畫
    }

    // 落地判斷
    private void OnCollisionEnter(Collision collision)
    {
        if (enableMovementOnNextTouch)
        {
            enableMovementOnNextTouch = false;
            //GetComponent<DualHooks>().CancelActiveGrapples();
        }
    }

    // 判斷是否在斜坡
    private bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    // 取得斜坡移動方向
    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }

    /// <summary>
    /// 計算跳躍初速度（以到達指定拋物線高度）
    /// </summary>
    public Vector3 CalculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight)
    {
        float gravity = Physics.gravity.y;
        float displacementY = endPoint.y - startPoint.y;
        Vector3 displacementXZ = new Vector3(endPoint.x - startPoint.x, 0f, endPoint.z - startPoint.z);

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * trajectoryHeight);
        Vector3 velocityXZ = displacementXZ / (Mathf.Sqrt(-2 * trajectoryHeight / gravity)
            + Mathf.Sqrt(2 * (displacementY - trajectoryHeight) / gravity));

        return velocityXZ + velocityY;
    }

    // --------------------------- //
    //        UI/除錯功能         //
    // --------------------------- //
    #region Text & Debugging

    public TextMeshProUGUI text_speed; // 速度顯示
    public TextMeshProUGUI text_mode;  // 狀態顯示
    private void TextStuff()
    {
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        if (OnSlope())
            text_speed.SetText("移動速度: " + Round(rb.velocity.magnitude, 1) + " / " + Round(moveSpeed, 1));
        else
            text_speed.SetText("移動速度: " + Round(flatVel.magnitude, 1) + " / " + Round(moveSpeed, 1));
        text_mode.SetText("運作模式: " + state.ToString());
    }

    // 四捨五入
    public static float Round(float value, int digits)
    {
        float mult = Mathf.Pow(10.0f, (float)digits);
        return Mathf.Round(value * mult) / mult;
    }

    #endregion
}
