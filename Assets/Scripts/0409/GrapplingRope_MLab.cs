// GrapplingRope_MLab.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrapplingRope_MLab : MonoBehaviour
{
    [Header("References")]
    public Grappling grappling;

    [Header("Settings")]
    public int quality = 200;
    public float damper = 14;
    public float strength = 800;
    public float velocity = 15;
    public float waveCount = 3;
    public float waveHeight = 1;
    public AnimationCurve affectCurve;

    private Spring_MLab spring;
    private LineRenderer lr;
    private Vector3 currentGrapplePosition;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        spring = new Spring_MLab();
        spring.SetTarget(0);
    }

    private void LateUpdate()
    {
        if (lr == null || grappling == null)
            return; // 安全檢查

        DrawRope();
    }

    void DrawRope()
    {
        // 如果沒在勾爪狀態，重置繩子
        if (!grappling.IsGrappling())
        {
            currentGrapplePosition = grappling.gunTip.position;
            spring.Reset();

            if (lr.positionCount > 0)
                lr.positionCount = 0;

            return;
        }

        // 初始化繩子點數
        if (lr.positionCount != quality + 1)
        {
            spring.SetVelocity(velocity);
            lr.positionCount = quality + 1;
        }

        spring.SetDamper(damper);
        spring.SetStrength(strength);
        spring.Update(Time.deltaTime);

        Vector3 grapplePoint = grappling.GetGrapplePoint();
        Vector3 gunTipPosition = grappling.gunTip.position;

        Vector3 up = Quaternion.LookRotation((grapplePoint - gunTipPosition).normalized) * Vector3.up;

        // 平滑過渡目前抓取點，讓繩索收束更自然
        currentGrapplePosition = Vector3.Lerp(currentGrapplePosition, grapplePoint, Time.deltaTime * 8f);

        int pointCount = lr.positionCount;
        if (pointCount < 2) return; // 保險：不繪製少於兩點的線

        for (int i = 0; i < pointCount; i++)
        {
            float delta = i / (float)(pointCount - 1);
            Vector3 offset = up * waveHeight * Mathf.Sin(delta * waveCount * Mathf.PI) * spring.Value * affectCurve.Evaluate(delta);

            lr.SetPosition(i, Vector3.Lerp(gunTipPosition, currentGrapplePosition, delta) + offset);
        }
    }
}