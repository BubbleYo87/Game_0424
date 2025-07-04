using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallRunning : MonoBehaviour
{
    [Header("Wallrunning")]
    public LayerMask whatIsWall;
    public LayerMask whatIsGround;
    public float wallRunForce;
    public float wallClimbSpeed;
    public float maxWallRunTime;
    private float wallRunTimer;

    [Header("Wall Jump Settings")]
    public float wallJumpUpForce = 6f;    // 牆跑跳的垂直力：比 pm.jumpForce 要小
    public float wallJumpSideForce = 8f;  // 牆跑跳的水平力

    [Header("Input")]
    public KeyCode upwardsRunKey = KeyCode.LeftShift;
    public KeyCode downwardsRunKey = KeyCode.LeftControl;
    private bool upwardsRunning;
    private bool downwardsRunning;
    private float horizontalInput;
    private float verticalInput;

    [Header("Camera Tilt")]
    public Transform cam;
    public float cameraTilt = 15f;
    public float tiltSpeed = 5f;

    private float currentTilt;

    [Header("Camera FOV")]
    public Camera camComponent;
    public float wallRunFOV = 90f;
    public float defaultFOV = 60f;
    public float fovTransitionSpeed = 8f;

    [Header("Detection")]
    public float wallCheckDistance;
    public float minJumpHeight;
    private RaycastHit leftWallhit;
    private RaycastHit rightWallhit;
    private bool wallLeft;
    private bool wallRight;

    [Header("References")]
    public Transform orientation;
    private PlayerMovementGrappling pm;
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<PlayerMovementGrappling>();
    }

    private void Update()
    {
        CheckForWall();
        StateMachine();
        TiltCamera();
        HandleFOV();
        if (pm.wallrunning && Input.GetKeyDown(pm.jumpKey))
            WallJump();
    }

    private void FixedUpdate()
    {
        if (pm.wallrunning)
            WallRunningMovement();
    }

    private void CheckForWall()
    {
        wallRight = Physics.Raycast(transform.position, orientation.right, out rightWallhit, wallCheckDistance, whatIsWall);
        wallLeft = Physics.Raycast(transform.position, -orientation.right, out leftWallhit, wallCheckDistance, whatIsWall);
    }

    private bool AboveGround()
    {
        return !Physics.Raycast(transform.position, Vector3.down, minJumpHeight, whatIsGround);
    }

    private void StateMachine()
    {
        // Getting Inputs
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        upwardsRunning = Input.GetKey(upwardsRunKey);
        downwardsRunning = Input.GetKey(downwardsRunKey);

        // State 1 - Wallrunning
        if((wallLeft || wallRight) && verticalInput > 0 && AboveGround())
        {
            if (!pm.wallrunning)
                StartWallRun();
        }

        // State 3 - None
        else
        {
            if (pm.wallrunning)
                StopWallRun();
        }
    }

    private void StartWallRun()
    {
        pm.wallrunning = true;
    }

    private void WallRunningMovement()
    {
        rb.useGravity = false;
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;

        Vector3 wallForward = Vector3.Cross(wallNormal, transform.up);

        if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
            wallForward = -wallForward;

        // forward force
        rb.AddForce(wallForward * wallRunForce, ForceMode.Force);

        // upwards/downwards force
        if (upwardsRunning)
            rb.velocity = new Vector3(rb.velocity.x, wallClimbSpeed, rb.velocity.z);
        if (downwardsRunning)
            rb.velocity = new Vector3(rb.velocity.x, -wallClimbSpeed, rb.velocity.z);

        // push to wall force
        if (!(wallLeft && horizontalInput > 0) && !(wallRight && horizontalInput < 0))
            rb.AddForce(-wallNormal * 100, ForceMode.Force);
    }

    private void StopWallRun()
    {
        pm.wallrunning = false;
    }
    private void TiltCamera()
    {
        if (pm.wallrunning)
        {
            if (wallLeft)
                currentTilt = -cameraTilt;
            else if (wallRight)
                currentTilt = cameraTilt;
        }
        else
        {
            currentTilt = 0;
        }

        Quaternion targetRotation = Quaternion.Euler(0, 0, currentTilt);
        cam.localRotation = Quaternion.Lerp(cam.localRotation, targetRotation, Time.deltaTime * tiltSpeed);
    }
    private void HandleFOV()
    {
        float targetFOV = pm.wallrunning ? wallRunFOV : defaultFOV;
        camComponent.fieldOfView = Mathf.Lerp(camComponent.fieldOfView, targetFOV, Time.deltaTime * fovTransitionSpeed);
    }
    private void WallJump()
    {
        StopWallRun();
        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;

        // 傳入 wallJumpUpForce，而非 pm.jumpForce
        pm.WallRunJump(wallJumpUpForce, wallJumpSideForce, wallNormal);
    }


}
