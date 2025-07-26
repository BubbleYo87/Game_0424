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
    public float swingSpeed;         // 盪繩速度
    public float dashSpeed;          // 衝刺速度
    public float dashSpeedChangeFactor; // 衝刺時加速因子
    public float climbingSpeed;      // 攀爬速度
    public float groundDrag;         // 地面摩擦力
    public float wallrunSpeed;       // 牆跑速度
    public float maxYSpeed;          // 最大Y軸速度（防止飛太高）

    [Header("Jumping")]
    public float jumpForce;          // 跳躍力
    public float jumpCooldown;       // 跳躍冷卻
    public float airMultiplier;      // 空中移動速度倍率
    bool readyToJump;                // 是否可跳
    [Header("雙連跳設定")]
    public int   maxJumpCount       = 2;    // 最多算几次跳跃（1 = 禁双跳，2 = 一次双跳）
    public float doubleJumpCooldown = 0.5f; // 双跳之间最小间隔（秒）
    private int   jumpCount         = 0;    // 跳跃次数计数
    private float lastJumpTime      = -999f;

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

    public Transform orientation;    // 角色朝向
    [Header("Grappling")]
    public float timeToTarget;       // 到達目標的時間（鉤爪用）

    [Header("血量系統")]
    [Tooltip("最大血量")] 
    public float maxHP = 100;                         // 總血量
    private float currentHP;                        // 當前血量
/*  [Tooltip("血條物件陣列，長度請設定為 maxHP+1")]
    public GameObject[] hpBars;                   // 各血量對應的 UI 切換 */
    [Tooltip("剛剛是否被傷害過，用來讓 Enemy_BoomB 顯示 UI")]
    public bool hasTakenDamage = false;
    public float damageFlagDuration = 1f;  // 受傷後這段秒數內都算「剛受傷」
    [Header("被打飛設定(純位移)")]
    [Tooltip("擊退持續時間（秒）")]
    public float knockbackDuration = 0.5f;
    [Tooltip("擊退速度 (單位：單位/秒)")]
    public float knockbackSpeed = 25f;
    // 狀態用變數
    private bool isKnockback = false;           // 是否正在被擊退
    private Vector3 knockbackDirection;         // 擊退方向
    private float knockbackTimer = 0f;          // 計時用

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
        grappling,      // 鉤爪中
        swinging,       // 盪繩中
        walking,        // 行走
        sprinting,      // 跑步
        crouching,      // 蹲下
        air,            // 空中
        wallrunning,    // 牆跑
        climbing,       // 攀爬
        dashing         // 衝刺
    }

    public bool freeze;          // 是否凍結移動
    public bool activeGrapple;   // 是否在勾爪中
    public bool swinging;        // 是否在盪繩
    public bool wallrunning;     // 是否牆跑
    public bool climbing;        // 是否攀爬
    public bool dashing;         // 是否衝刺

    // 勾爪判定屬性
    public bool GrappleAttempted { get; private set; }
    public bool GrappleHit { get; private set; }

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
        currentHP = maxHP;                // 設定初始血量
    }

    // 每幀更新
    private void Update()
    {
        // Update() 裡，取代原本的「if (isKnockback) { … transform.position += … }」區塊
        if (isKnockback)
        {
            // 每幀欲位移的距離
            float step = knockbackSpeed * Time.deltaTime;
            // 緩衝半徑，避免緊貼 collider 卡住穿透
            float skin = 0.1f;

            // 1. 朝 knockbackDirection 打一條射線，長度 = step + skin
            if (Physics.Raycast(transform.position, knockbackDirection, out RaycastHit hit, step + skin, whatIsGround))
            {
                // 2a. 如果前方有障礙 (hit)，就把玩家卡在障礙表面前 skin 距離
                Vector3 safePos = hit.point + hit.normal * skin;
                transform.position = safePos;

                // 結束擊退
                EndKnockback();
            }
            else
            {
                // 2b. 沒碰到阻擋，就正常往後位移
                transform.position += knockbackDirection * step;

                // 倒數計時，檢查是否結束
                knockbackTimer -= Time.deltaTime;
                if (knockbackTimer <= 0f)
                    EndKnockback();
            }

            // 跳過後續移動／輸入機制
            return;
        }

        // 落地檢查
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        // 掉地重置 jumpCount
        if (grounded && jumpCount != 0)
            jumpCount = 0;
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
        if (activeGrapple)
        {
            // 勾爪時限速
            float maxSpeed = 20f;
            if (rb.velocity.magnitude > maxSpeed)
                rb.velocity = rb.velocity.normalized * maxSpeed;
        }

        if (!isKnockback)
        {
            if (activeGrapple && rb.velocity.magnitude > 20f)
                rb.velocity = rb.velocity.normalized * 20f;
            MovePlayer();
        }

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
                jumpCount = 1;
                lastJumpTime = Time.time;
                readyToJump = false;
                Invoke(nameof(ResetJump), jumpCooldown);
            }
            // 双跳判定
            else if (jumpCount < maxJumpCount 
                  && Time.time - lastJumpTime >= doubleJumpCooldown)
            {
                Jump();
                jumpCount++;
                lastJumpTime = Time.time;
                readyToJump = false;
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
        // 勾爪
        else if (activeGrapple)
        {
            state = MovementState.grappling;
            desiredMoveSpeed = sprintSpeed;
        }
        // 盪繩
        else if (swinging)
        {
            state = MovementState.swinging;
            desiredMoveSpeed = swingSpeed;
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
        else if (grounded)
        {
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }
        // 牆跑
        else if (wallrunning)
        {
            state = MovementState.wallrunning;
            moveSpeed = sprintSpeed;
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
            state = MovementState.swinging; //（這裡可改為 dashing 更直觀）
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
        if (activeGrapple) return;
        if (swinging) return;

        // 計算移動方向
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

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
        if (activeGrapple) return;

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
    /// 牆跑跳躍
    /// </summary>
    public void WallRunJump(float upwardForce, float outwardForce, Vector3 wallNormal)
    {
        exitingSlope = true;
        readyToJump = false;
        // 1. 重設Y速度
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        // 2. 跳躍方向（往上+推離牆面）
        Vector3 jumpDirection = transform.up * upwardForce + wallNormal * outwardForce;
        // 3. 施加跳躍力
        rb.AddForce(jumpDirection, ForceMode.Impulse);
        // 4. 跳躍冷卻
        Invoke(nameof(ResetJump), jumpCooldown);
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

    /// <summary>
    /// 以拋物線跳至目標點（鉤爪/跳板等用）
    /// </summary>
    public void JumpToPosition(Vector3 targetPosition, float trajectoryHeight)
    {
        activeGrapple = true;
        rb.velocity = Vector3.zero;

        float gravity = Mathf.Abs(Physics.gravity.y);
        float displacementY = targetPosition.y - transform.position.y;
        Vector3 displacementXZ = new Vector3(targetPosition.x - transform.position.x, 0, targetPosition.z - transform.position.z);

        timeToTarget = 0.4f; // 跳躍時間

        Vector3 velocityXZ = displacementXZ / timeToTarget;
        float velocityY = (displacementY / timeToTarget) + 0.5f * gravity * timeToTarget;
        Vector3 resultVelocity = velocityXZ + Vector3.up * velocityY;

        rb.velocity = resultVelocity;

        // 啟動軌道修正
        StartCoroutine(CorrectTrajectory(targetPosition, timeToTarget));
        Invoke(nameof(ResetRestrictions), timeToTarget + 0.1f);
    }

    /// <summary>
    /// 協程：強制拉向目標點，補正精度
    /// </summary>
    private IEnumerator CorrectTrajectory(Vector3 targetPosition, float duration)
    {
        float timeElapsed = 0f;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            Vector3 toTarget = (targetPosition - transform.position);
            float distance = toTarget.magnitude;
            // 接近目標時強制拉向目標
            if (distance < 3f)
            {
                float pullStrength = Mathf.Lerp(0, 50f, 1 - (distance / 3f));
                rb.AddForce(toTarget.normalized * pullStrength, ForceMode.Acceleration);
            }
            yield return null;
        }
    }

    // 強制設置速度
    private Vector3 velocityToSet;
    private void SetVelocity()
    {
        enableMovementOnNextTouch = true;
        rb.velocity = velocityToSet;
        //cam.DoFov(grappleFov); // 可搭配視角動畫
    }

    // 重置鉤爪限制
    public void ResetRestrictions()
    {
        activeGrapple = false;
        //cam.DoFov(85f); // 恢復視角
    }

    // 落地判斷
    private void OnCollisionEnter(Collision collision)
    {
        if (enableMovementOnNextTouch)
        {
            enableMovementOnNextTouch = false;
            ResetRestrictions();
            //GetComponent<DualHooks>().CancelActiveGrapples();
        }
    }
    // 若玩家用 Trigger 方式接觸到敵人，則造成傷害
    private void OnTriggerEnter(Collider other)
    {
        if(hasTakenDamage)
        {
            Debug.LogWarning("OnTriggerEnter: 玩家剛受傷，忽略此次傷害");
            return; // 忽略已受傷的情況
        }
        // 往父物件搜尋 Enemy_BoomB 腳本
        Enemy_BoomB boom = other.GetComponentInParent<Enemy_BoomB>();
        if (boom != null)
        {
            float dmg = boom.LastDamageValue;
            TakeDamage(dmg);
        }
        // 2. 碰到名為 Enemys_DMG_Guy_Rock 的 Trigger
        if (other.CompareTag("Enemys_DMG_Guy_Rock"))
        {
            // 扣 30 點血
            TakeDamage(30f);
            // 啟動擊退
            StartKnockback();
            return;
        }
    }

    /// <summary>
    /// 受到傷害時呼叫，更新血量，切換血條，死亡或進入 Hit 狀態
    /// </summary>
    public void TakeDamage(float amount)
    {
        // 1. 標記受傷 flag
        hasTakenDamage = true;
        StartCoroutine(ResetDamageFlagAfterDelay());

        // 2. 扣血（浮點數）
        currentHP = Mathf.Clamp(currentHP - amount, 0f, maxHP);
        Debug.Log($"[Player] 受到 {amount:F1} 點傷害，剩餘 HP={currentHP:F1}");

        // 3. 鏡頭抖動
        GameEvents.TriggerCameraShake(0.4f, 0.7f);

        // 4. 死亡處理
        if (currentHP <= 0f)
            Die();
    }
    private IEnumerator ResetDamageFlagAfterDelay()
    {
        yield return new WaitForSeconds(damageFlagDuration);
        hasTakenDamage = false;
    }
    private void Die()
    {
        Debug.Log("[Player] 死亡！");
        // 例如重載場景、播放動畫…
        // SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
    public TextMeshProUGUI text_hp;  // HP顯示
    private void TextStuff()
    {
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        if (OnSlope())
            text_speed.SetText("移動速度: " + Round(rb.velocity.magnitude, 1) + " / " + Round(moveSpeed, 1));
        else
            text_speed.SetText("移動速度: " + Round(flatVel.magnitude, 1) + " / " + Round(moveSpeed, 1));
        text_mode.SetText("運作模式: " + state.ToString());
        // 小數點後保留 1 位
        text_hp.SetText($"血量: {currentHP:F1} / {maxHP}");
    }

    // 四捨五入
    public static float Round(float value, int digits)
    {
        float mult = Mathf.Pow(10.0f, (float)digits);
        return Mathf.Round(value * mult) / mult;
    }

    // 是否正在勾爪狀態
    public bool IsGrapplingActive()
    {
        return activeGrapple;
    }

    #endregion

    /// <summary>
    /// 啟動純位移擊退
    /// </summary>
    private void StartKnockback()
    {
        // 1. 進入擊退狀態，凍結原本的移動機制
        freeze = true;
        isKnockback = true;

        // 2. 計算擊退方向：往玩家背後
        knockbackDirection = -orientation.forward.normalized;

        // 3. 設定倒數計時器
        knockbackTimer = knockbackDuration;
    }
    /// <summary>
    /// 結束擊退的共用方法
    /// </summary>
    private void EndKnockback()
    {
        isKnockback = false;
        freeze = false;
    }
}
