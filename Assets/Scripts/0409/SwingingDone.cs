using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwingingDone : MonoBehaviour
{
    [Header("References")]
    public LineRenderer lr;
    public Transform gunTip, cam, player;
    public LayerMask whatIsGrappleable;
    public PlayerMovementGrappling pm;

    [Header("Swinging")]
    private float maxSwingDistance = 25f;
    private Vector3 swingPoint;
    private SpringJoint joint;

    [Header("OdmGear")]
    public Transform orientation;
    public Rigidbody rb;
    public float horizontalThrustForce;
    public float forwardThrustForce;
    public float extendCableSpeed;

    [Header("Prediction")]
    public RaycastHit predictionHit;
    public float predictionSphereCastRadius;
    public Transform predictionPoint;

    [Header("Input")]
    public KeyCode swingKey = KeyCode.Mouse0;

    [Header("Rope Visual Settings")]
    public int quality = 100;
    public float damper = 14;
    public float strength = 800;
    public float velocity = 15;
    public float waveCount = 2;
    public float waveHeight = 1f;
    public AnimationCurve affectCurve;

    private Spring_MLab spring;
    private Vector3 currentGrapplePosition;

    private void Start()
    {
        spring = new Spring_MLab();
        spring.SetTarget(0);
    }

    private void Update()
    {
        if (Input.GetKeyDown(swingKey)) StartSwing();
        if (Input.GetKeyUp(swingKey)) StopSwing();

        CheckForSwingPoints();

        if (joint != null) OdmGearMovement();
    }

    private void LateUpdate()
    {
        DrawRope();
    }

    private void CheckForSwingPoints()
    {
        if (joint != null) return;

        RaycastHit sphereCastHit;
        Physics.SphereCast(cam.position, predictionSphereCastRadius, cam.forward,
            out sphereCastHit, maxSwingDistance, whatIsGrappleable);

        RaycastHit raycastHit;
        Physics.Raycast(cam.position, cam.forward,
            out raycastHit, maxSwingDistance, whatIsGrappleable);

        Vector3 realHitPoint;

        if (raycastHit.point != Vector3.zero)
            realHitPoint = raycastHit.point;
        else if (sphereCastHit.point != Vector3.zero)
            realHitPoint = sphereCastHit.point;
        else
            realHitPoint = Vector3.zero;

        if (realHitPoint != Vector3.zero)
        {
            predictionPoint.gameObject.SetActive(true);
            predictionPoint.position = realHitPoint;
        }
        else
        {
            predictionPoint.gameObject.SetActive(false);
        }

        predictionHit = raycastHit.point == Vector3.zero ? sphereCastHit : raycastHit;
    }

    private void StartSwing()
    {
        if (predictionHit.point == Vector3.zero) return;

        if (GetComponent<Grappling>() != null)
            GetComponent<Grappling>().StopGrapple();

        pm.ResetRestrictions();
        pm.swinging = true;

        swingPoint = predictionHit.point;
        joint = player.gameObject.AddComponent<SpringJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedAnchor = swingPoint;

        float distanceFromPoint = Vector3.Distance(player.position, swingPoint);

        joint.maxDistance = distanceFromPoint * 0.8f;
        joint.minDistance = distanceFromPoint * 0.25f;

        joint.spring = 4.5f;
        joint.damper = 7f;
        joint.massScale = 4.5f;

        spring.SetVelocity(velocity);
        lr.positionCount = quality + 1;
        currentGrapplePosition = gunTip.position;
    }

    public void StopSwing()
    {
        pm.swinging = false;
        lr.positionCount = 0;
        Destroy(joint);
        spring.Reset();
    }

    private void OdmGearMovement()
    {
        if (Input.GetKey(KeyCode.D)) rb.AddForce(orientation.right * horizontalThrustForce * Time.deltaTime);
        if (Input.GetKey(KeyCode.A)) rb.AddForce(-orientation.right * horizontalThrustForce * Time.deltaTime);
        if (Input.GetKey(KeyCode.W)) rb.AddForce(orientation.forward * horizontalThrustForce * Time.deltaTime);

        if (Input.GetKey(KeyCode.Space))
        {
            Vector3 directionToPoint = swingPoint - transform.position;
            rb.AddForce(directionToPoint.normalized * forwardThrustForce * Time.deltaTime);

            float distanceFromPoint = Vector3.Distance(transform.position, swingPoint);
            joint.maxDistance = distanceFromPoint * 0.8f;
            joint.minDistance = distanceFromPoint * 0.25f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            float extendedDistanceFromPoint = Vector3.Distance(transform.position, swingPoint) + extendCableSpeed;
            joint.maxDistance = extendedDistanceFromPoint * 0.8f;
            joint.minDistance = extendedDistanceFromPoint * 0.25f;
        }
    }

    private void DrawRope()
    {
        if (!joint) return;

        spring.SetDamper(damper);
        spring.SetStrength(strength);
        spring.Update(Time.deltaTime);

        Vector3 gunTipPosition = gunTip.position;
        Vector3 up = Quaternion.LookRotation((swingPoint - gunTipPosition).normalized) * Vector3.up;

        currentGrapplePosition = Vector3.Lerp(currentGrapplePosition, swingPoint, Time.deltaTime * 8f);

        if (lr.positionCount != quality + 1)
            lr.positionCount = quality + 1;

        for (int i = 0; i < lr.positionCount; i++)
        {
            float delta = i / (float)(lr.positionCount - 1);
            Vector3 offset = up * waveHeight * Mathf.Sin(delta * waveCount * Mathf.PI) * spring.Value * affectCurve.Evaluate(delta);
            lr.SetPosition(i, Vector3.Lerp(gunTipPosition, currentGrapplePosition, delta) + offset);
        }
    }
}
