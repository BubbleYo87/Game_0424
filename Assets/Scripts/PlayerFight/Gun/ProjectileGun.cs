using UnityEngine;
using System.Collections;

/// <summary>
/// 可切換模式的拋體武器：Auto / Semi / Burst
/// 右鍵瞄準（長按或切換可選），散佈、後座、彈藥/裝彈、傷害衰減等
/// 生成「子彈 Prefab（Projectile.cs）」並賦予初速
/// ★ 不再改變相機 FOV（ADS 只影響散佈/準度）
/// </summary>
public class ProjectileGun : WeaponBase
{
    public enum FireMode { Auto, Semi, Burst }

    [Header("引用")]
    [Tooltip("用來決定射擊方向的相機（通常是第一人稱相機）")]
    public Camera aimCamera;

    [Tooltip("槍口位置（生成子彈/火花）")]
    public Transform muzzle;

    [Tooltip("子彈預置體（需帶 Projectile + Rigidbody + Collider）")]
    public GameObject projectilePrefab;

    [Header("射擊模式")]
    public FireMode fireMode = FireMode.Auto;  // 初始模式
    public KeyCode keySwitchMode = KeyCode.V;  // 切換模式鍵
    [Tooltip("點放（Burst）時每梭的發數")]
    public int burstCount = 3;
    [Tooltip("點放內部的每發間隔秒數")]
    public float burstInterval = 0.08f;

    [Header("射擊節奏")]
    [Tooltip("每秒幾發（Auto/Semi 都受此影響）")]
    public float fireRate = 10f;
    private float nextShootTime = 0f;

    [Header("彈道/值")]
    public float muzzleVelocity = 60f; // 子彈初速（m/s）
    public float baseDamage = 20f;
    public float falloffStart = 30f;
    public float falloffEnd = 80f;
    public float minDamage = 8f;

    [Header("命中層級（僅供散佈參考，可不改）")]
    public LayerMask hitMask = ~0;

    [Header("散佈/準度")]
    [Tooltip("腰射散佈角（度）")]
    public float hipfireSpread = 1.0f;
    [Tooltip("瞄準散佈角（度）")]
    public float adsSpread = 0.1f;
    [Tooltip("每發增加的附加散佈（度）")]
    public float spreadIncreasePerShot = 0.15f;
    [Tooltip("散佈回復速度（度/秒）")]
    public float spreadRecoverRate = 1.5f;
    private float currentSpread = 0f;


    [Header("後座/手感")]
    public bool applyRecoil = true;
    public float recoilPitch = 1.5f;           // 每發上仰
    public float recoilYaw = 0.6f;             // 每發左右
    public float recoilReturnSpeed = 18f;      // 回正速度（度/秒）
    private float pendingPitch = 0f;
    private float pendingYaw = 0f;

    [Header("彈藥/裝彈")]
    public int magSize = 30;
    public int ammoInMag = 30;
    public int reserveAmmo = 90;
    public float reloadTime = 1.6f;
    public bool canShootWhileReload = false;
    private bool isReloading = false;

    [Header("音效/特效（可選）")]
    public AudioSource audioSource;
    public AudioClip shootSFX;
    public AudioClip reloadSFX;
    public GameObject muzzleFlashPrefab;

    [Header("輸入（可自訂）")]
    public KeyCode keyReload = KeyCode.R;

    private void Start()
    {
        if (!aimCamera) aimCamera = Camera.main;
        // ★ 不再改變相機 FOV（移除 defaultFOV/adsFOV/adsTime）
    }

    private void Update()
    {
        HandleModeSwitchInput();
        HandleReloadInput();

        if (isReloading && !canShootWhileReload) return;

        // 射擊輸入
        bool firePressed  = Input.GetMouseButtonDown(0); // 左鍵按下
        bool fireHolding  = Input.GetMouseButton(0);     // 左鍵長按

        switch (fireMode)
        {
            case FireMode.Semi:
                if (firePressed) TryShootOnce();
                break;
            case FireMode.Auto:
                if (fireHolding) TryShootOnce();
                break;
            case FireMode.Burst:
                if (firePressed) StartCoroutine(BurstRoutine());
                break;
        }

        RecoverSpread(Time.deltaTime);
        RecoverRecoil(Time.deltaTime);
        // ★ 移除 LerpFOV()
    }

    // ====== 輸入 ======
    private void HandleModeSwitchInput()
    {
        if (Input.GetKeyDown(keySwitchMode))
        {
            // Auto → Semi → Burst → Auto…
            fireMode = (FireMode)(((int)fireMode + 1) % 3);
            // TODO: 這裡可以加 UI 提示目前模式
        }
    }

    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(keyReload))
            TryReload();
    }

    // ====== 射擊主流程 ======
    private void TryShootOnce()
    {
        if (Time.time < nextShootTime) return;
        if (ammoInMag <= 0)
        {
            TryReload();
            return;
        }

        // 冷卻
        nextShootTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
        ammoInMag--;

        // 1) 算發射方向（相機中心 + 散佈）
        Vector3 shootDir = GetShootDirectionWithSpread();

        // 2) 生成子彈
        if (projectilePrefab && muzzle)
        {
            var go = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(shootDir, Vector3.up));
            var pj = go.GetComponent<Projectile>();
            var rb = go.GetComponent<Rigidbody>();

            if (pj)
            {
                // 將槍的數值餵給子彈（讓子彈自行依距離計算衰減）
                pj.baseDamage   = baseDamage;
                pj.falloffStart = falloffStart;
                pj.falloffEnd   = falloffEnd;
                pj.minDamage    = minDamage;
                pj.owner        = transform.root; // 或者玩家 Transform
            }

            if (rb)
            {
                rb.velocity = shootDir * muzzleVelocity;
            }
        }

        // 3) 視覺/音效/手感
        PlayMuzzleFlash();
        PlayShootSFX();
        AddRecoil();

        // 4) 散佈累積
        currentSpread += spreadIncreasePerShot;
    }

    private IEnumerator BurstRoutine()
    {
        if (Time.time < nextShootTime) yield break;
        if (ammoInMag <= 0) { TryReload(); yield break; }

        // 將整段點放時間粗略鎖定（避免太快再次觸發）
        nextShootTime = Time.time + (burstCount - 1) * burstInterval + 1f / Mathf.Max(0.01f, fireRate);

        int shots = Mathf.Min(burstCount, ammoInMag);
        for (int i = 0; i < shots; i++)
        {
            TryShootOnce(); // 單發處理
            yield return new WaitForSeconds(burstInterval);
            if (ammoInMag <= 0) { TryReload(); yield break; }
        }
    }

    // ====== 工具：散佈/後座/散佈回復 ======
    private Vector3 GetShootDirectionWithSpread()
    {
        Vector3 dir = aimCamera ? aimCamera.transform.forward : transform.forward;

        float baseSpread = hipfireSpread; // 預設為腰射散佈
        float totalSpread = Mathf.Max(0f, baseSpread + currentSpread);
        
        // 以角度（度）隨機擺動：在相機的局部空間內加一個小角度
        float yaw   = Random.Range(-totalSpread, totalSpread);
        float pitch = Random.Range(-totalSpread, totalSpread);
        Quaternion spreadRot = Quaternion.Euler(pitch, yaw, 0f);

        return spreadRot * dir;
    }

    private void RecoverSpread(float dt)
    {
        currentSpread = Mathf.MoveTowards(currentSpread, 0f, spreadRecoverRate * dt);
    }

    private void AddRecoil()
    {
        if (!applyRecoil || !aimCamera) return;

        float yawSign = Random.value < 0.5f ? -1f : 1f;
        pendingPitch += recoilPitch;
        pendingYaw   += recoilYaw * yawSign;

        // 立刻打一點抬頭/偏移（增強手感）
        aimCamera.transform.localRotation *= Quaternion.Euler(-recoilPitch, recoilYaw * yawSign, 0f);
    }

    private void RecoverRecoil(float dt)
    {
        if (!aimCamera) return;

        float pitchStep = Mathf.Sign(pendingPitch) * Mathf.Min(Mathf.Abs(pendingPitch), recoilReturnSpeed * dt);
        float yawStep   = Mathf.Sign(pendingYaw)   * Mathf.Min(Mathf.Abs(pendingYaw),   recoilReturnSpeed * dt);

        aimCamera.transform.localRotation *= Quaternion.Euler(pitchStep, -yawStep, 0f);

        pendingPitch -= pitchStep;
        pendingYaw   -= yawStep;
    }

    // ====== 裝彈 ======
    private void TryReload()
    {
        if (isReloading) return;
        if (ammoInMag >= magSize) return;
        if (reserveAmmo <= 0) return;

        StartCoroutine(Co_Reload());
    }

    private IEnumerator Co_Reload()
    {
        isReloading = true;
        if (audioSource && reloadSFX) audioSource.PlayOneShot(reloadSFX);

        yield return new WaitForSeconds(reloadTime);

        int need = magSize - ammoInMag;
        int take = Mathf.Min(need, reserveAmmo);
        ammoInMag += take;
        reserveAmmo -= take;

        isReloading = false;
    }

    // ====== 視覺/音效 ======
    private void PlayMuzzleFlash()
    {
        if (!muzzleFlashPrefab || !muzzle) return;
        var vfx = Instantiate(muzzleFlashPrefab, muzzle.position, muzzle.rotation);
        Destroy(vfx, 2f);
    }

    private void PlayShootSFX()
    {
        if (!audioSource || !shootSFX) return;
        audioSource.PlayOneShot(shootSFX);
    }
}
