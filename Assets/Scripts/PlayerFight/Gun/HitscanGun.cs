using UnityEngine;
using System.Collections;

public class HitscanGun : MonoBehaviour
{
    public enum FireMode { Semi, Auto, Burst }

    [Header("必填引用")]
    [Tooltip("第一人稱相機（用來發射中心射線）")]
    public Camera aimCamera;
    [Tooltip("槍口位置（生成火花/彈殼/軌跡）")]
    public Transform muzzle;

    [Header("開火模式")]
    public FireMode fireMode = FireMode.Auto;
    [Tooltip("每秒幾發（10 = 0.1 秒一發）")]
    public float fireRate = 10f;
    [Tooltip("三連點發數")]
    public int burstCount = 3;
    [Tooltip("三連點每發間隔（秒）")]
    public float burstInterval = 0.08f;

    [Header("傷害/射程/衰減")]
    public float damage = 20f;
    public float range = 120f;
    [Tooltip("開始衰減距離（小於等於這個距離不衰減）")]
    public float falloffStart = 30f;
    [Tooltip("衰減至最低傷害的距離（>= 這個距離使用 minDamage）")]
    public float falloffEnd = 80f;
    [Tooltip("最小傷害（遠距離下限）")]
    public float minDamage = 8f;

    [Header("命中判定")]
    public LayerMask hitMask = ~0;  // 允許命中的 Layer
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("散佈/準度")]
    [Tooltip("腰射散佈角（度）")]
    public float hipfireSpread = 1.0f;
    [Tooltip("瞄準散佈角（度）")]
    public float adsSpread = 0.1f;
    [Tooltip("每發增加散佈量（度）")]
    public float spreadIncreasePerShot = 0.15f;
    [Tooltip("散佈回復速度（度/秒）")]
    public float spreadRecoverRate = 1.5f;

    [Header("ADS（右鍵）")]
    public bool toggleAim = false;           // 按一下切換或長按
    public float defaultFOV = 60f;
    public float adsFOV = 45f;
    public float adsTime = 0.12f;

    [Header("後座/手感")]
    public bool applyRecoil = true;
    [Tooltip("每發上仰角度")]
    public float recoilPitch = 1.5f;
    [Tooltip("每發水平偏移（正負隨機）")]
    public float recoilYaw = 0.6f;
    [Tooltip("回正速度（度/秒）")]
    public float recoilReturnSpeed = 18f;

    [Header("彈藥/裝彈")]
    public int magSize = 30;
    public int ammoInMag = 30;
    public int reserveAmmo = 90;
    public float reloadTime = 1.6f;
    public bool canShootWhileReload = false;

    [Header("視覺/音效（可選）")]
    public GameObject muzzleFlashPrefab;
    public Transform shellEjectPoint;
    public GameObject shellPrefab;
    public AudioSource audioSource;
    public AudioClip shootSFX;
    public AudioClip reloadSFX;

    // —— 內部狀態 —— //
    private bool isAiming = false;
    private bool isReloading = false;
    private float nextShootTime = 0f;
    private float currentSpread = 0f;
    private float pendingPitch = 0f;  // 用於回彈
    private float pendingYaw = 0f;

    private void Start()
    {
        if (!aimCamera) aimCamera = Camera.main;
        if (aimCamera) aimCamera.fieldOfView = defaultFOV;
        currentSpread = 0f;
    }

    private void Update()
    {
        HandleAimInput();
        HandleReloadInput();

        if (isReloading && !canShootWhileReload) return;

        switch (fireMode)
        {
            case FireMode.Semi:
                if (Input.GetMouseButtonDown(0)) TryShootOnce();
                break;
            case FireMode.Auto:
                if (Input.GetMouseButton(0)) TryShootOnce();
                break;
            case FireMode.Burst:
                if (Input.GetMouseButtonDown(0)) StartCoroutine(BurstRoutine());
                break;
        }

        RecoverSpread(Time.deltaTime);
        RecoverRecoil(Time.deltaTime);
        LerpFOV(Time.deltaTime);
    }

    // ——— 輸入：瞄準 & 裝彈 ——— //
    private void HandleAimInput()
    {
        if (!toggleAim)
        {
            // 長按右鍵瞄準
            isAiming = Input.GetMouseButton(1);
        }
        else
        {
            // 點按切換
            if (Input.GetMouseButtonDown(1)) isAiming = !isAiming;
        }
    }

    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
            TryReload();
    }

    // ——— 射擊主流程 ——— //
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

        // 射擊：計算方向（含散佈）
        Vector3 shootDir = GetShootDirectionWithSpread();

        // Raycast 命中
        Vector3 origin = aimCamera.transform.position;
        if (Physics.Raycast(origin, shootDir, out RaycastHit hit, range, hitMask, triggerInteraction))
        {
            float finalDmg = ComputeDamageWithFalloff(hit.distance);
            // 優先帶命中點的介面
            if (hit.collider.TryGetComponent<IDamageableWithHit>(out var adv))
            {
                adv.TakeDamage(finalDmg, hit.point, hit.normal);
            }
            else if (hit.collider.TryGetComponent<IDamageable>(out var simple))
            {
                simple.TakeDamage(finalDmg);
            }

            // TODO: 命中特效（血花/彈孔）可在這裡生成
        }

        // 視覺 & 手感
        PlayMuzzleFlash();
        EjectShell();
        PlayShootSFX();
        AddRecoil();

        // 散佈累積
        currentSpread += spreadIncreasePerShot;
    }

    private IEnumerator BurstRoutine()
    {
        if (Time.time < nextShootTime) yield break;
        if (ammoInMag <= 0) { TryReload(); yield break; }

        // 將連發時間鎖到下一個可開火點
        nextShootTime = Time.time + (burstCount - 1) * burstInterval + 1f / Mathf.Max(0.01f, fireRate);

        int shots = Mathf.Min(burstCount, ammoInMag);
        for (int i = 0; i < shots; i++)
        {
            TryShootOnce(); // 單次會處理彈藥/命中/手感
            yield return new WaitForSeconds(burstInterval);
            if (ammoInMag <= 0) { TryReload(); yield break; }
        }
    }

    // ——— 工具：方向/傷害/散佈/後座/FOV ——— //
    private Vector3 GetShootDirectionWithSpread()
    {
        // 以相機正前方為基準方向
        Vector3 dir = aimCamera.transform.forward;

        // 決定本發的目標散佈角
        float baseSpread = isAiming ? adsSpread : hipfireSpread;
        float totalSpread = Mathf.Max(0f, baseSpread + currentSpread);

        // 在單位球面上增加一個小偏移（角度制散佈）
        // 作法：在相機的局部空間內，隨機一個小角度偏移
        float yaw = Random.Range(-totalSpread, totalSpread);
        float pitch = Random.Range(-totalSpread, totalSpread);

        Quaternion spreadRot = Quaternion.Euler(pitch, yaw, 0f);
        return spreadRot * dir;
    }

    private float ComputeDamageWithFalloff(float distance)
    {
        if (distance <= falloffStart) return damage;
        if (distance >= falloffEnd) return Mathf.Min(damage, minDamage);

        float t = Mathf.InverseLerp(falloffStart, falloffEnd, distance);
        float dmg = Mathf.Lerp(damage, Mathf.Min(damage, minDamage), t);
        return dmg;
    }

    private void RecoverSpread(float dt)
    {
        // 逐步回復擴散
        float target = 0f;
        currentSpread = Mathf.MoveTowards(currentSpread, target, spreadRecoverRate * dt);
    }

    private void AddRecoil()
    {
        if (!applyRecoil || !aimCamera) return;

        // 每發疊加等待回正的位移量
        float yawSign = Random.value < 0.5f ? -1f : 1f;
        pendingPitch += recoilPitch;
        pendingYaw += recoilYaw * yawSign;

        // 立即打上抬頭/偏移（增強手感）
        aimCamera.transform.localRotation *= Quaternion.Euler(-recoilPitch, recoilYaw * yawSign, 0f);
    }

    private void RecoverRecoil(float dt)
    {
        if (!aimCamera) return;
        // 緩慢往反方向回正
        float pitchStep = Mathf.Sign(pendingPitch) * Mathf.Min(Mathf.Abs(pendingPitch), recoilReturnSpeed * dt);
        float yawStep = Mathf.Sign(pendingYaw) * Mathf.Min(Mathf.Abs(pendingYaw), recoilReturnSpeed * dt);

        aimCamera.transform.localRotation *= Quaternion.Euler(pitchStep, -yawStep, 0f);

        pendingPitch -= pitchStep;
        pendingYaw -= yawStep;
    }

    private void LerpFOV(float dt)
    {
        if (!aimCamera) return;
        float target = isAiming ? adsFOV : defaultFOV;
        aimCamera.fieldOfView = Mathf.MoveTowards(aimCamera.fieldOfView, target, Mathf.Abs(defaultFOV - adsFOV) / Mathf.Max(0.01f, adsTime) * dt);
    }

    // ——— 視覺/音效 ——— //
    private void PlayMuzzleFlash()
    {
        if (!muzzleFlashPrefab || !muzzle) return;
        var vfx = Instantiate(muzzleFlashPrefab, muzzle.position, muzzle.rotation);
        Destroy(vfx, 2f);
    }

    private void EjectShell()
    {
        if (!shellPrefab || !shellEjectPoint) return;
        var shell = Instantiate(shellPrefab, shellEjectPoint.position, shellEjectPoint.rotation);
        Destroy(shell, 6f);
    }

    private void PlayShootSFX()
    {
        if (!audioSource || !shootSFX) return;
        audioSource.PlayOneShot(shootSFX);
    }

    // ——— 裝彈 ——— //
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
}
