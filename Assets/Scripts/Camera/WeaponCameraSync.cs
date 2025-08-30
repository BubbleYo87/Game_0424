using UnityEngine;

[DefaultExecutionOrder(50)]
public class WeaponCameraSync : MonoBehaviour
{
    [Header("參考主相機（不填則自動抓 MainCamera）")]
    public Camera mainCam;

    private Camera weaponCam;

    void Awake()
    {
        weaponCam = GetComponent<Camera>();
        if (!mainCam) mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (!mainCam || !weaponCam) return;

        // FOV 同步（瞄準時主相機縮放，武器相機也要跟著）
        weaponCam.fieldOfView = mainCam.fieldOfView;

        // 若不是子物件、或你想絕對同步：
        // transform.position = mainCam.transform.position;
        // transform.rotation = mainCam.transform.rotation;
    }
}
