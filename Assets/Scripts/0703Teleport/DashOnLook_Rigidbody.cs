// DashOnLook_Rigidbody.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class DashOnLook_Rigidbody : MonoBehaviour
{
    [Header("射線設定")]
    public Camera playerCamera;
    public float maxDistance = 50f;
    public LayerMask teleportLayerMask = ~0;

    [Header("位移參數")]
    public float backOffDistance = 2f;
    public float heightOffset = 1f;
    public float dashDuration = 0.1f;

    [Header("指示圖示")]
    public Image indicatorImage;  // 要改色的 UI Image

    private Rigidbody rb;
    private bool isDashing = false;
    private bool canDash = false; 

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (playerCamera == null) Debug.LogError("請在 Inspector 指定 playerCamera！");
        if (indicatorImage == null) Debug.LogError("請在 Inspector 指定 indicatorImage！");
    }

    private void Update()
    {
        // 每幀先更新 canDash，並同步指示圖顏色
        UpdateDashAvailability();

        // 只有在可以 Dash 且不在 Dash 中，按鍵時才執行
        if (canDash && !isDashing && Input.GetMouseButtonDown(0))
            StartDashCoroutine();
    }

    private void UpdateDashAvailability()
    {
        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            float distance = Vector3.Distance(transform.position, hit.point);
            int hitLayer = hit.collider.gameObject.layer;
            bool isAllowedLayer = (teleportLayerMask.value & (1 << hitLayer)) != 0;

            canDash = isAllowedLayer && distance > backOffDistance;
        }
        else
        {
            canDash = false;
        }

        // 更新顏色：可 Dash 顯示紅，不可顯示白
        indicatorImage.color = canDash 
            ? new Color32(0xFF, 0x00, 0x00, 0xFF) 
            : new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    }

    private void StartDashCoroutine()
    {
        // 射線重跑一次，拿最終的 hit.point
        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            return;

        float distance = Vector3.Distance(transform.position, hit.point);
        int hitLayer = hit.collider.gameObject.layer;
        bool isAllowedLayer = (teleportLayerMask.value & (1 << hitLayer)) != 0;

        if (!isAllowedLayer || distance <= backOffDistance)
            return;

        Vector3 dir    = (hit.point - playerCamera.transform.position).normalized;
        Vector3 basePos = hit.point - dir * backOffDistance;
        Vector3 targetPos = basePos + Vector3.up * heightOffset;

        StartCoroutine(DashCoroutine(transform.position, targetPos));
    }

    private IEnumerator DashCoroutine(Vector3 startPos, Vector3 targetPos)
    {
        isDashing = true;
        bool origColl = rb.detectCollisions;
        rb.detectCollisions = false;

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            float t = elapsed / dashDuration;
            rb.MovePosition(Vector3.Lerp(startPos, targetPos, t));
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(targetPos);
        rb.detectCollisions = origColl;
        isDashing = false;
    }
}
