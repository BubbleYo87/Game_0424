using UnityEngine;
using System.Collections;

/// <summary>
/// 霰彈槍（無 ADS）：支援 Semi / Pump / Auto
/// 每次射擊生成多顆「實體彈丸」(Projectile)，用「錐角」決定散佈；包含強後座、裝彈（可選單顆上彈）
/// 需求：子彈 Prefab 需帶 Projectile + Rigidbody + Collider
/// 與 WeaponSwitcher / WeaponBase 整合：
/// - 切槍後的裝備冷卻由 WeaponBase.equipLockDuration 控制（Inspector 可調）
/// - 任何使用前都請先檢查 CanUse()
/// - 在泵動/換彈期間以 SetUseBlocked(true/false) 通知切換器「我在使用中」
/// </summary>
public class ShotgunGun : WeaponBase
{
    public enum ShotgunMode { Semi, Pump, Auto }

    [Header("引用")]
    [Tooltip("決定射擊方向的相機（通常是第一人稱相機）")]
    public Camera aimCamera;
    [Tooltip("槍口 Transform（生成彈丸/火焰特效）")]
    public Transform muzzle;
    [Tooltip("彈丸 Prefab（需有 Projectile + Rigidbody + Collider）")]
    public GameObject projectilePrefab;

    [Header("模式/節奏")]
    public ShotgunMode mode = ShotgunMode.Pump;
    [Tooltip("每秒幾發（Semi/Auto 皆受限；Pump 另外受 cycleTime 限制）")]
    public float fireRate = 1.5f;
    [Tooltip("泵動循環時間（拉柄/上膛），期間不可再射")]
    public float pumpCycleTime = 0.5f;

    private float nextShootTime = 0f;
    private bool  isPumping = false;

    [Header("彈道/散佈")]
    [Tooltip("每次射擊的彈丸數量（例如 8 或 12）")]
    public int pelletsPerShot = 8;
    [Tooltip("散佈錐角（半角，度）。越大越散，建議 3~7 度起手")]
    public float coneHalfAngleDeg = 5f;
    [Tooltip("彈丸初速（m/s）")]
    public float pelletMuzzleVelocity = 55f;

    [Header("傷害/衰減（由彈丸自身計算）")]
    [Tooltip("單顆彈丸的基礎傷害（總傷害≈ pelletsPerShot * pelletDamage）")]
    public float pelletDamage = 6f;
    public float falloffStart = 10f;
    public float falloffEnd   = 30f;
    public float minDamage    = 1.5f;

    [Header("彈藥/裝彈")]
    public int   magSize     = 6;
    public int   ammoInMag   = 6;
    public int   reserveAmmo = 24;
    [Tooltip("整體換彈時間（一次裝滿用），若啟用單顆上彈則只作為起手/結尾拉柄動畫時間")]
    public float reloadTime = 2.0f;
    [Tooltip("是否「單顆上彈」（tube 裝彈），每顆花 shellInsertTime 秒")]
    public bool  reloadIndividually = true;
    [Tooltip("單顆上彈所需時間（秒）")]
    public float shellInsertTime = 0.5f;
    private bool isReloading = false;

    [Header("後座/手感（霰彈建議較大）")]
    public bool  applyRecoil = true;
    public float recoilPitch = 6f;   // 上仰
    public float recoilYaw   = 2f;   // 左右
    public float recoilReturnSpeed = 16f;
    private float pendingPitch = 0f;
    private float pendingYaw   = 0f;

    [Header("特效/音效（可選）")]
    public AudioSource audioSource;
    public AudioClip shootSFX;
    public AudioClip reloadSFX;
    public AudioClip pumpSFX;
    public GameObject muzzleFlashPrefab;
    public Transform muzzleFlashPosition;

    [Header("輸入")]
    public KeyCode keyReload = KeyCode.R;

    private void Start()
    {
        if (!aimCamera) aimCamera = Camera.main;
    }

    private void Update()
    {
        HandleReloadInput();

        // 補：保留舊狀態判斷（更直觀），再搭配 useBlocked 雙重保險
        if (isReloading) return;                 // 換彈中不可射
        if (mode == ShotgunMode.Pump && isPumping) return; // 泵動中不可射

        bool firePressed = Input.GetMouseButtonDown(0);
        bool fireHold    = Input.GetMouseButton(0);

        switch (mode)
        {
            case ShotgunMode.Semi:
                if (firePressed) TryShootOnce();
                break;
            case ShotgunMode.Pump:
                if (firePressed) TryShootOnce();
                break;
            case ShotgunMode.Auto:
                if (fireHold)    TryShootOnce();
                break;
        }

        RecoverRecoil(Time.deltaTime);
    }

    // ===== 射擊流程 =====
    private void TryShootOnce()
    {
        // ★ 1) 切槍後裝備冷卻 / 任何鎖定狀態 → 不可射擊
        if (!CanUse()) return;

        // 射速冷卻
        if (Time.time < nextShootTime) return;

        // 沒子彈 → 嘗試換彈
        if (ammoInMag <= 0)
        {
            TryReload();
            return;
        }

        if (!projectilePrefab || !muzzle || !aimCamera) return;

        nextShootTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
        ammoInMag--;

        // 1) 基準方向
        Vector3 forward = aimCamera.transform.forward;
        Transform tCam  = aimCamera.transform;

        // 2) 連發生成多顆彈丸
        for (int i = 0; i < pelletsPerShot; i++)
        {
            Vector3 dir = SampleDirectionInCone(forward, tCam.up, tCam.right, coneHalfAngleDeg);
            var go = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir, Vector3.up));
            var rb = go.GetComponent<Rigidbody>();
            var pj = go.GetComponent<Projectile>();
            if (pj)
            {
                pj.baseDamage   = pelletDamage;
                pj.falloffStart = falloffStart;
                pj.falloffEnd   = falloffEnd;
                pj.minDamage    = minDamage;
                pj.owner        = transform.root; // 避免剛生成打到自己
            }
            if (rb) rb.velocity = dir * pelletMuzzleVelocity;
        }

        // 3) 視覺/音效/手感
        PlayMuzzleFlash();
        PlayShootSFX();
        AddRecoil();

        // 4) 泵動循環（若為 Pump 模式）
        if (mode == ShotgunMode.Pump)
            StartCoroutine(Co_PumpCycle());
    }

    /// <summary>
    /// 在給定的前向附近，取一個「均勻隨機」方向；使用錐體半角（度）
    /// </summary>
    private static Vector3 SampleDirectionInCone(Vector3 forward, Vector3 camUp, Vector3 camRight, float halfAngleDeg)
    {
        float phi = Random.Range(0f, Mathf.PI * 2f);
        float r   = Mathf.Sqrt(Random.value) * halfAngleDeg; // 均勻分佈的角半徑
        float yawDeg   = r * Mathf.Cos(phi);  // 左右
        float pitchDeg = r * Mathf.Sin(phi);  // 上下

        Quaternion rot = Quaternion.AngleAxis(yawDeg, camUp) * Quaternion.AngleAxis(-pitchDeg, camRight);
        return (rot * forward).normalized;
    }

    // ===== 後座恢復 =====
    private void AddRecoil()
    {
        if (!applyRecoil || !aimCamera) return;

        float yawSign = Random.value < 0.5f ? -1f : 1f;
        pendingPitch += recoilPitch;
        pendingYaw   += recoilYaw * yawSign;

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

    // ===== 泵動循環 =====
    private IEnumerator Co_PumpCycle()
    {
        isPumping = true;

        // ★ 2) 動作期間鎖住使用 → 切換器可判定「使用中」而擋切槍
        SetUseBlocked(true);

        if (audioSource && pumpSFX) audioSource.PlayOneShot(pumpSFX);
        yield return new WaitForSeconds(pumpCycleTime);

        SetUseBlocked(false);
        isPumping = false;
    }

    // ===== 裝彈 =====
    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(keyReload)) TryReload();
    }

    private void TryReload()
    {
        if (isReloading) return;
        if (ammoInMag >= magSize) return;
        if (reserveAmmo <= 0) return;

        if (reloadIndividually) StartCoroutine(Co_ReloadByShell());
        else                    StartCoroutine(Co_ReloadAll());
    }

    /// <summary>整體換彈（盒式彈匣）</summary>
    private IEnumerator Co_ReloadAll()
    {
        isReloading = true;

        // ★ 4) 動作期間鎖住使用
        SetUseBlocked(true);

        if (audioSource && reloadSFX) audioSource.PlayOneShot(reloadSFX);
        yield return new WaitForSeconds(reloadTime);

        int need = magSize - ammoInMag;
        int take = Mathf.Min(need, reserveAmmo);
        ammoInMag += take;
        reserveAmmo -= take;

        // ★ 解除鎖定
        SetUseBlocked(false);
        isReloading = false;
    }

    /// <summary>單顆上彈（管式彈倉）</summary>
    private IEnumerator Co_ReloadByShell()
    {
        isReloading = true;
        SetUseBlocked(true); // ★ 鎖住使用（整段裝彈流程）

        // 起手動作（可用 reloadSFX 當拉開槍機聲）
        if (audioSource && reloadSFX) audioSource.PlayOneShot(reloadSFX);
        yield return new WaitForSeconds(Mathf.Min(0.2f, reloadTime * 0.25f));

        while (ammoInMag < magSize && reserveAmmo > 0)
        {
            yield return new WaitForSeconds(shellInsertTime);
            ammoInMag++;
            reserveAmmo--;
            // 可在此播放每顆的「卡嗒」音效
        }

        // 結尾拉柄（可與泵動一致的聲音）
        if (audioSource && pumpSFX) audioSource.PlayOneShot(pumpSFX);
        yield return new WaitForSeconds(Mathf.Min(0.25f, reloadTime * 0.25f));

        SetUseBlocked(false); // ★ 解鎖
        isReloading = false;
    }

    // ===== 視覺/音效 =====
    private void PlayMuzzleFlash()
    {
        if (!muzzleFlashPrefab || !muzzle || !muzzleFlashPosition) return;
        var vfx = Instantiate(muzzleFlashPrefab, muzzleFlashPosition.position, muzzleFlashPosition.rotation);
        Destroy(vfx, 0.4f);
    }

    private void PlayShootSFX()
    {
        if (!audioSource || !shootSFX) return;
        audioSource.PlayOneShot(shootSFX);
    }

    // ===== 與切換器/基底的整合點 =====

    /// <summary>
    /// 切到這把武器時（被啟用）
    /// </summary>
    public override void OnEquip(Transform owner, WeaponSwitcher switcher)
    {
        base.OnEquip(owner, switcher);
        // TODO：播放「拿槍動畫」。若用動畫事件控制解鎖：
        //  1) 把 WeaponBase.equipLockDuration 調為 0
        //  2) 這裡手動 SetUseBlocked(true)
        //  3) 在動畫最後一幀事件呼叫 SetUseBlocked(false)
    }

    /// <summary>
    /// 切走這把武器時（被停用）→ 把任何進行中的流程收乾淨
    /// </summary>
    public override void OnUnequip()
    {
        base.OnUnequip();     // 內含 StopAllCoroutines() + 解鎖保險
        StopAllCoroutines();  // 再保險一次，避免子類新開的協程殘留
        isReloading = false;
        isPumping   = false;
        SetUseBlocked(false); // 確保離場時不會把「使用鎖」留給下次
    }
}
