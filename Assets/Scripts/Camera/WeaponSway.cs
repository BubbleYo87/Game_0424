using UnityEngine;

/// <summary>
/// 武器慣性搖晃（看向左/右/上/下時，武器在位置/旋轉上有微小延遲與反向偏移）
/// 建議掛在「武器根物件」（通常在 WeaponCamera 底下）
/// </summary>
[DefaultExecutionOrder(60)] // 在相機移動之後執行，避免撕裂感
public class WeaponSway : MonoBehaviour
{
    [Header("參考：用來量測晃動的相機（主相機）")]
    public Transform referenceCamera; // 一般填 Main Camera；若本物件已是相機子物件可不填，自動抓

    [Header("基準姿勢（起始時自動記錄）")]
    public Vector3 baseLocalPos;
    public Vector3 baseLocalEuler;

    [Header("位置搖晃（Inertia Offset）")]
    [Tooltip("視角每度的位移量（越大搖越大），X=左右、Y=上下（單位：公尺/度）")]
    public Vector2 posAmount = new Vector2(0.0035f, 0.0025f);
    [Tooltip("位置搖晃的最大夾限（避免過大）")]
    public Vector2 posClamp = new Vector2(0.06f, 0.04f);
    [Tooltip("位置回彈平滑速度（越大越快趨近）")]
    public float posSmooth = 12f;

    [Header("旋轉搖晃（Tilt）")]
    [Tooltip("每度視角對應的旋轉量（度/度）。x=隨上下視角做Pitch，y=隨左右視角做Yaw，z=左右視角造成Roll（常為負）")]
    public Vector3 rotAmount = new Vector3(0.6f, -0.8f, -1.2f);
    [Tooltip("旋轉夾限（度）")]
    public Vector3 rotClamp = new Vector3(10f, 10f, 15f);
    [Tooltip("旋轉回彈平滑速度")]
    public float rotSmooth = 12f;

    [Header("ADS 衰減（瞄準時縮小搖晃）")]
    [Tooltip("是否啟用 ADS 衰減")]
    public bool useADSAttenuation = true;
    [Tooltip("瞄準時的搖晃倍率（0~1）")]
    [Range(0f, 1f)] public float adsSwayMult = 0.25f;

    [Tooltip("可選：若有 ProjectileGun，勾這個會自動讀它的 IsAiming")]
    public bool readAimingFromProjectileGun = true;
    public ProjectileGun gun; // 你的射擊腳本（請在 ProjectileGun 補一個 public bool IsAiming => isAiming;）

    // 內部
    private Vector3 targetLocalPos;
    private Quaternion targetLocalRot;
    private Vector3 velPos; // SmoothDamp 速度
    private Quaternion lastRefRot;
    private bool hasRef;

    void Awake()
    {
        if (!referenceCamera)
        {
            var cam = Camera.main;
            referenceCamera = cam ? cam.transform : null;
        }

        // 記錄基準
        baseLocalPos = transform.localPosition;
        baseLocalEuler = transform.localEulerAngles;

        targetLocalPos = baseLocalPos;
        targetLocalRot = Quaternion.Euler(baseLocalEuler);

        if (referenceCamera)
        {
            lastRefRot = referenceCamera.rotation;
            hasRef = true;
        }

        // 若要自動抓同層的 ProjectileGun
        if (readAimingFromProjectileGun && !gun)
            gun = GetComponentInParent<ProjectileGun>();
    }

    void LateUpdate()
    {
        if (!hasRef && referenceCamera)
        {
            lastRefRot = referenceCamera.rotation;
            hasRef = true;
        }
        if (!referenceCamera) return;

        // 1) 取得「本幀相機旋轉差」→ 轉成近似的 pitch/yaw 差（度）
        Quaternion current = referenceCamera.rotation;
        Quaternion deltaQ = current * Quaternion.Inverse(lastRefRot);
        lastRefRot = current;

        // 把旋轉差轉成「小角度的近似歐拉」，使用短角差避免跳值
        deltaQ.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f; // 取最短路徑
        Vector3 deltaEuler = angle * axis; // 近似的小角度（世界空間）

        // 轉到「相機的右手/上/前」座標系，方便理解左右/上下
        Vector3 localDelta = Quaternion.Inverse(referenceCamera.rotation) * deltaEuler;
        float dx = localDelta.y; // 左右（Yaw，繞Y轉 → 對應 localDelta 的 y）
        float dy = -localDelta.x; // 上下（Pitch，繞X轉 → 對應 localDelta 的 x，取負較自然）

        // 2) ADS 衰減（若啟用）
        float adsMult = 1f;
        if (useADSAttenuation)
        {
            bool aiming = false;
            if (readAimingFromProjectileGun && gun != null)
            {
                // 需要在你的 ProjectileGun 補一個公開屬性：
                // public bool IsAiming => isAiming;
                aiming = false;
            }
            // 也可改成由外部 SetAiming(bool) 餵值（另外做 public 方法即可）
            adsMult = aiming ? adsSwayMult : 1f;
        }

        // 3) 計算「目標位置偏移」：視角往左 → 武器往右（取反向）
        Vector3 posOffset = new Vector3(
            Mathf.Clamp(-dx * posAmount.x, -posClamp.x, posClamp.x),
            Mathf.Clamp(-dy * posAmount.y, -posClamp.y, posClamp.y),
            0f
        ) * adsMult;

        targetLocalPos = baseLocalPos + posOffset;

        // 4) 計算「目標旋轉傾斜」
        Vector3 eulerOffset = new Vector3(
            Mathf.Clamp(dy * rotAmount.x, -rotClamp.x, rotClamp.x),   // Pitch：抬頭就槍口微上
            Mathf.Clamp(dx * rotAmount.y, -rotClamp.y, rotClamp.y),   // Yaw：看左就槍身微向左
            Mathf.Clamp(dx * rotAmount.z, -rotClamp.z, rotClamp.z)    // Roll：看左就槍身微外翻
        ) * adsMult;

        targetLocalRot = Quaternion.Euler(baseLocalEuler + eulerOffset);

        // 5) 平滑插值到目標（位置用 SmoothDamp，旋轉用 Slerp）
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, targetLocalPos, ref velPos, 1f / Mathf.Max(0.0001f, posSmooth));
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRot, Time.unscaledDeltaTime * rotSmooth);
    }

    /// <summary>外部（例如你的槍腳本）若想強制重置到基準，可呼叫這個</summary>
    public void SnapToBasePose()
    {
        transform.localPosition = baseLocalPos;
        transform.localRotation = Quaternion.Euler(baseLocalEuler);
        targetLocalPos = baseLocalPos;
        targetLocalRot = Quaternion.Euler(baseLocalEuler);
        velPos = Vector3.zero;
        if (referenceCamera) lastRefRot = referenceCamera.rotation;
    }
}
