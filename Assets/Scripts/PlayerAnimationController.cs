using UnityEngine;
using TMPro; // 引用 TextMeshPro 命名空間
using System.Collections;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    public PlayerMovementGrappling playerMovement;

    private Rigidbody rb;
    private bool wasGrapplingActive = false;
    private bool hasGrappled = false;

    // 🔫 武器模式：0 = 刀、1 = 槍
    private int weaponMode = 0;

    // 🔢 子彈相關
    [SerializeField] private int maxAmmo = 10;       // 最大子彈數
    [SerializeField] private int currentAmmo;        // 當前子彈數
    public TextMeshProUGUI ammoText;                 // UI 文字顯示用
    private bool isReloading = false;                // 是否正在換彈
    private bool isShooting = false;         // ✅ 是否正在開槍（鎖輸入）
    [SerializeField] private float fireRate = 0.35f; // ✅ 射擊間隔時間，跟動畫一樣長


    void Start()
    {
        animator = GetComponent<Animator>();

        if (playerMovement == null)
        {
            Debug.LogError("請在 Inspector 指定 PlayerMovementGrappling！");
            return;
        }

        rb = playerMovement.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("PlayerMovementGrappling 上沒有 Rigidbody！");
        }

        // 初始化
        animator.SetInteger("weaponMode", weaponMode);
        currentAmmo = maxAmmo; // 子彈初始化滿彈
    }

    void Update()
    {
        if (animator == null || rb == null) return;

        // ✅ Q 鍵切換武器模式
        if (Input.GetKeyDown(KeyCode.Q))
        {
            weaponMode = (weaponMode == 0) ? 1 : 0;
            animator.SetInteger("weaponMode", weaponMode);
            Debug.Log("當前武器模式: " + (weaponMode == 0 ? "持刀" : "持槍"));
        }

        // ✅ 左鍵攻擊邏輯
        if (Input.GetMouseButtonDown(0) && playerMovement.grounded)
        {
            if (weaponMode == 0)
            {
                // 🗡️ 持刀攻擊：不扣子彈，不限間隔
                animator.SetTrigger("atk");
            }
            else if (weaponMode == 1 && !isReloading && !isShooting)
            {
                if (currentAmmo > 0)
                {
                    // ✅ 槍攻擊：限制開火速率 + 同步動畫
                    StartCoroutine(FireAfterDelay(fireRate));
                }
                else
                {
                    // ⛔ 沒子彈，自動換彈
                    StartCoroutine(ReloadCoroutine());
                }
            }
        }

        // ✅ R 鍵手動補彈（只有在未滿彈、未在重裝時）
        if (Input.GetKeyDown(KeyCode.R) && weaponMode == 1 && !isReloading && currentAmmo < maxAmmo)
        {
            StartCoroutine(ReloadCoroutine());
        }

        // ✅ Space 鍵跳躍時切換動畫
        if (!playerMovement.grounded)
        {
            animator.SetBool("air", true);
        }
        else
        {
            animator.SetBool("air", false);
        }

        // ✅ 更新移動速度給 Blend Tree
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float currentSpeed = horizontalVelocity.magnitude;
        animator.SetFloat("walk_speed", currentSpeed);

        // ✅ 勾鎖動畫狀態更新
        bool currentGrapplingState = playerMovement.activeGrapple;

        if (currentGrapplingState) hasGrappled = true;

        if (playerMovement.freeze)
        {
            animator.SetBool("vine_bool", true);
        }

        if (!playerMovement.freeze && !hasGrappled)
        {
            animator.SetBool("vine_bool", false);
        }

        if (hasGrappled && wasGrapplingActive && !currentGrapplingState)
        {
            animator.SetBool("vine_bool", false);
            hasGrappled = false;
        }

        wasGrapplingActive = currentGrapplingState;

        // ✅ 顯示子彈數量在 UI
        if (ammoText != null)
        {
            currentAmmo = Mathf.Clamp(currentAmmo, 0, maxAmmo); // 防止負值
            ammoText.text = "子彈數量: " + currentAmmo + " / " + maxAmmo;
        }
    }

    // ✅ 換彈 Coroutine（動畫 + 等待時間）
    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        animator.SetTrigger("reload");
        animator.SetBool("reload_bool", true);

        yield return new WaitForSeconds(0.85f); // 依照你的動畫長度調整

        currentAmmo = maxAmmo;
        animator.SetBool("reload_bool", false);
        isReloading = false;
    }
    // ✅ 延遲射擊邏輯（與動畫同步）
    private IEnumerator FireAfterDelay(float delay)
    {
        isShooting = true;

        animator.SetTrigger("atk");

        yield return new WaitForSeconds(delay); // 等待動畫結束

        if (currentAmmo > 0) // ✅ 再次檢查以防萬一
        {
            currentAmmo--;

            GunShooter shooter = GetComponent<GunShooter>();
            if (shooter != null)
                shooter.Shoot();
        }

        isShooting = false;
    }

}
