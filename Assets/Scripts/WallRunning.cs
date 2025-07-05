// WallRunning.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
    public float wallJumpUpForce = 6f;
    public float wallJumpSideForce = 8f;

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

    [Header("URP Motion Blur")]
    [Tooltip("Global Volume with Motion Blur Override")] public Volume postProcessVolume;
    [Tooltip("Target Motion Blur intensity (0-1)")] public float blurTarget = 1f;
    [Tooltip("Blur transition speed")] public float blurSpeed = 8f;
    private MotionBlur motionBlur;
    private float originalBlur;

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
        // initialize URP Motion Blur
        if (postProcessVolume != null && postProcessVolume.profile.TryGet<MotionBlur>(out motionBlur))
        {
            originalBlur = motionBlur.intensity.value;
            motionBlur.active = false;
        }
    }

    private void Update()
    {
        CheckForWall();
        StateMachine();
        TiltCamera();
        HandleFOVAndBlur();
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
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
        upwardsRunning = Input.GetKey(upwardsRunKey);
        downwardsRunning = Input.GetKey(downwardsRunKey);

        if ((wallLeft || wallRight) && verticalInput > 0 && AboveGround())
        {
            if (!pm.wallrunning) StartWallRun();
        }
        else
        {
            if (pm.wallrunning) StopWallRun();
        }
    }

    private void StartWallRun()
    {
        pm.wallrunning = true;
        wallRunTimer = maxWallRunTime;
    }

    private void WallRunningMovement()
    {
        rb.useGravity = false;
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        Vector3 wallForward = Vector3.Cross(wallNormal, transform.up);
        if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
            wallForward = -wallForward;

        rb.AddForce(wallForward * wallRunForce, ForceMode.Force);
        if (upwardsRunning) rb.velocity = new Vector3(rb.velocity.x, wallClimbSpeed, rb.velocity.z);
        if (downwardsRunning) rb.velocity = new Vector3(rb.velocity.x, -wallClimbSpeed, rb.velocity.z);
        if (!(wallLeft && horizontalInput > 0) && !(wallRight && horizontalInput < 0))
            rb.AddForce(-wallNormal * 100, ForceMode.Force);

        wallRunTimer -= Time.deltaTime;
        if (wallRunTimer <= 0) StopWallRun();
    }

    private void StopWallRun()
    {
        pm.wallrunning = false;
        rb.useGravity = true;
    }

    private void TiltCamera()
    {
        currentTilt = 0f;
        if (pm.wallrunning)
            currentTilt = wallLeft ? -cameraTilt : cameraTilt;
        Quaternion targetRot = Quaternion.Euler(0, 0, currentTilt);
        cam.localRotation = Quaternion.Lerp(cam.localRotation, targetRot, Time.deltaTime * tiltSpeed);
    }

    private void HandleFOVAndBlur()
    {
        bool running = pm.wallrunning;
        float targetFOV = running ? wallRunFOV : defaultFOV;
        camComponent.fieldOfView = Mathf.Lerp(camComponent.fieldOfView, targetFOV, Time.deltaTime * fovTransitionSpeed);

        if (motionBlur != null)
        {
            motionBlur.active = running;
            float target = running ? blurTarget : originalBlur;
            motionBlur.intensity.value = Mathf.Lerp(motionBlur.intensity.value, target, Time.deltaTime * blurSpeed);
        }
    }

    private void WallJump()
    {
        StopWallRun();
        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        pm.WallRunJump(wallJumpUpForce, wallJumpSideForce, wallNormal);
    }
}
