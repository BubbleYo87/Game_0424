using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DashController : MonoBehaviour
{
    [Header("Dash Settings")]
    public float dashForce = 10f;        // 衝刺推力大小（可在 Inspector 调整）
    public float dashDuration = 0.2f;    // 衝刺持续时间（秒）
    public float tapDelay = 0.3f;        // 双击 Shift 最大间隔

    [Tooltip("最大允许的冲刺垂直分量，0.5 相当于最多 45° 向上")]
    public float maxVerticalComponent = 0.5f;

    [Header("References")]
    public Camera cam;                   // 主摄像机

    private Rigidbody rb;
    private int tapCount;
    private float lastTapTime;
    private bool isDashing;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (cam == null && Camera.main != null)
            cam = Camera.main;
    }

    void Update()
    {
        if (isDashing) return;

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            if (Time.time - lastTapTime <= tapDelay)
                tapCount++;
            else
                tapCount = 1;

            lastTapTime = Time.time;

            if (tapCount >= 2)
            {
                StartCoroutine(PerformDash());
                tapCount = 0;
            }
        }
    }

    private IEnumerator PerformDash()
    {
        isDashing = true;

        Vector3 dir = cam.transform.forward;
        dir.y = Mathf.Clamp(dir.y, -maxVerticalComponent, maxVerticalComponent);
        dir.Normalize();

        float dashSpeed = dashForce;           // 每秒移動的距離（速度感）
        float elapsed = 0f;

        Vector3 startVelocity = rb.velocity;
        rb.useGravity = false;                 // 可選，關掉重力更像「滑行」

        while (elapsed < dashDuration)
        {
            float delta = Time.deltaTime;
            elapsed += delta;

            // 使用推力加速（ForceMode.Acceleration 可達成慣性）
            rb.AddForce(dir * dashSpeed * delta * 50f, ForceMode.Acceleration);

            yield return null;
        }

        rb.useGravity = true;
        isDashing = false;
    }

}
