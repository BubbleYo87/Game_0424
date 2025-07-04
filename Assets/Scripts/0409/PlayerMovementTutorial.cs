using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerMovementTutorial : MonoBehaviour
{
    // ------ 基本移動參數 ------
    [Header("Movement")]
    private float moveSpeed; // 當前移動速度（根據狀態變化）
    public float walkSpeed; // 行走速度
    public float sprintSpeed; // 奔跑速度
    public float groundDrag; // 地面摩擦力

    // ------ 跳躍參數 ------
    [Header("Jumping")]
    public float jumpForce; // 跳躍力度
    public float jumpCooldown; // 跳躍冷卻時間
    public float airMultiplier; // 空中移動力度倍率
    bool readyToJump; // 是否可以跳躍

    // ------ 蹲伏參數 ------
    [Header("Crouching")]
    public float crouchSpeed; // 蹲伏移動速度
    public float crouchYScale; // 蹲伏時角色縮放高度
    private float startYScale; // 初始角色高度

    // ------ 按鍵綁定 ------
    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space; // 跳躍鍵
    public KeyCode sprintKey = KeyCode.LeftShift; // 奔跑鍵
    public KeyCode crouchKey = KeyCode.LeftControl; // 蹲伏鍵

    // ------ 地面檢測 ------
    [Header("Ground Check")]
    public float playerHeight; // 角色高度
    public LayerMask whatIsGround; // 地面圖層
    bool grounded; // 是否在地面

    // ------ 斜坡處理 ------
    [Header("Slope Handling")]
    public float maxSlopeAngle; // 可行走的最大斜坡角度
    private RaycastHit slopeHit; // 斜坡偵測結果
    private bool exitingSlope; // 是否離開斜坡狀態

    public Transform orientation; // 角色面朝的方向（通常與攝像機對齊）

    float horizontalInput; // 水平輸入
    float verticalInput; // 垂直輸入

    Vector3 moveDirection; // 計算後的移動方向

    Rigidbody rb; // 角色剛體

    public MovementState state; // 當前移動狀態
    public enum MovementState
    {
        walking,
        sprinting,
        crouching,
        air
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // 鎖定剛體旋轉，避免傾倒

        readyToJump = true; // 開始時允許跳躍

        startYScale = transform.localScale.y; // 記錄初始角色高度
    }

    private void Update()
    {
        // 地面檢測
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        MyInput(); // 處理玩家輸入
        SpeedControl(); // 限制速度
        StateHandler(); // 狀態切換

        // 根據是否在地面調整剛體阻力
        if (grounded)
            rb.drag = groundDrag;
        else
            rb.drag = 0;
    }

    private void FixedUpdate()
    {
        MovePlayer(); // 在 FixedUpdate 處理移動，確保物理穩定
    }

    // 處理玩家輸入
    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal"); // A/D 或 左右鍵
        verticalInput = Input.GetAxisRaw("Vertical"); // W/S 或 上下鍵

        // 跳躍輸入
        if(Input.GetKey(jumpKey) && readyToJump && grounded)
        {
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown); // 設置跳躍冷卻
        }

        // 開始蹲伏
        if (Input.GetKeyDown(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse); // 給予向下衝擊力，使角色貼地
        }

        // 停止蹲伏
        if (Input.GetKeyUp(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
    }

    // 處理移動狀態
    private void StateHandler()
    {
        // 蹲伏模式
        if (Input.GetKey(crouchKey))
        {
            state = MovementState.crouching;
            moveSpeed = crouchSpeed;
        }
        // 奔跑模式
        else if(grounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
            moveSpeed = sprintSpeed;
        }
        // 行走模式
        else if (grounded)
        {
            state = MovementState.walking;
            moveSpeed = walkSpeed;
        }
        // 空中模式
        else
        {
            state = MovementState.air;
        }
    }

    // 處理角色移動
    private void MovePlayer()
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // 在斜坡上移動
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 20f, ForceMode.Force);

            // 當角色向上跳斜坡時，施加額外下壓力避免彈起
            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
        // 地面移動
        else if(grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);

        // 空中移動
        else if(!grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

        // 在斜坡上時關閉重力，避免角色滑落
        rb.useGravity = !OnSlope();
    }

    // 限制移動速度
    private void SpeedControl()
    {
        // 斜坡上限速
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }
        // 地面或空中限速
        else
        {
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }

    // 跳躍動作
    private void Jump()
    {
        exitingSlope = true;

        // 重置垂直速度
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    // 跳躍冷卻重置
    private void ResetJump()
    {
        readyToJump = true;
        exitingSlope = false;
    }

    // 斜坡檢測
    private bool OnSlope()
    {
        if(Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    // 獲取斜坡移動方向
    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }
}
