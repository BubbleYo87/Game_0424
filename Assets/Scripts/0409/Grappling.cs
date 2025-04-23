using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grappling : MonoBehaviour
{
    [Header("References")]
    private PlayerMovementGrappling pm;
    public Transform cam;
    public Transform gunTip;
    public LayerMask whatIsGrappleable;
    public LineRenderer lr;

    [Header("Grappling")]
    public float maxGrappleDistance;
    public float grappleDelayTime;
    public float overshootYAxis;
    public float ShowGrappleRopeAfterDelayTime = 0.3f;

    private Vector3 grapplePoint;

    [Header("Cooldown")]
    public float grapplingCd;
    private float grapplingCdTimer;

    [Header("Input")]
    public KeyCode grappleKey = KeyCode.Mouse1;

    public bool grappling;

    public bool GrappleAttempted { get; private set; }
    public bool GrappleHit { get; private set; }


    private void Start()
    {
        pm = GetComponent<PlayerMovementGrappling>();
        // 初始化 LineRenderer，防止出问题
        lr.positionCount = 2;
        lr.SetPosition(0, gunTip.position);
        lr.SetPosition(1, gunTip.position);
    }

    private void Update()
    {
        if (Input.GetKeyDown(grappleKey))
            {
                StartCoroutine(DelayedStartGrapple());
            }

        if (grapplingCdTimer > 0)
            grapplingCdTimer -= Time.deltaTime;
    }

    private void LateUpdate()
    {
        if (grappling)
            lr.SetPosition(0, gunTip.position);
    }

    private void StartGrapple()
    {
        GrappleAttempted = true;
        GrappleHit = false;

        if (grapplingCdTimer > 0) return;

        grappling = true;
        pm.freeze = true;

        RaycastHit hit;
        if (Physics.Raycast(cam.position, cam.forward, out hit, maxGrappleDistance, whatIsGrappleable))
        {
            GrappleHit = true;
            grapplePoint = hit.point;
            Invoke(nameof(ExecuteGrapple), grappleDelayTime);
        }
        else
        {
            grapplePoint = cam.position + cam.forward * maxGrappleDistance;
            Invoke(nameof(StopGrapple), grappleDelayTime);
        }

        // ✅ 改成延遲顯示繩索
        StartCoroutine(ShowGrappleRopeAfterDelay(ShowGrappleRopeAfterDelayTime));
    }


    private void ExecuteGrapple()
    {
        pm.freeze = false;

        float desiredArcHeight = 3f; // 可調參數：弧線高度感
        pm.JumpToPosition(grapplePoint, desiredArcHeight);

        Invoke(nameof(StopGrapple), 0.7f); // 跟 timeToTarget 差不多，留點緩衝
    }


    public void StopGrapple()
    {
        pm.freeze = false;

        grappling = false;

        grapplingCdTimer = grapplingCd;

        lr.enabled = false;

        GrappleAttempted = false;
        GrappleHit = false;
    }

    public bool IsGrappling()
    {
        return grappling;
    }

    public Vector3 GetGrapplePoint()
    {
        return grapplePoint;
    }
    private IEnumerator DelayedStartGrapple()
    {
        yield return new WaitForSeconds(0.2f);
        StartGrapple();
    }
    private IEnumerator ShowGrappleRopeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        lr.enabled = true;
        lr.positionCount = 2;
        lr.SetPosition(0, gunTip.position);
        lr.SetPosition(1, grapplePoint);
    }


}
