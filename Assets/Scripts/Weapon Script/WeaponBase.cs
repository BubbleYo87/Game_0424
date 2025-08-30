using UnityEngine;
using System.Collections;

/// <summary>
/// 可選基底類別：
/// - 幫你記錄 owner、switcher
/// - 提供「裝備冷卻」欄位與 SetUseBlocked/CanUse 的預設行為
/// 之後你的武器可以繼承這個，並在 Fire/Use 前檢查 CanUse()
/// </summary>
public class WeaponBase : MonoBehaviour, IWeapon
{
    [Header("裝備冷卻（切換後這段時間內不能使用）")]
    [Tooltip("切換裝備成功後，新武器有一段時間不能使用（開火/重裝等）")]
    public float equipLockDuration = 0.4f;

    protected Transform owner;
    protected WeaponSwitcher switcher;
    protected bool useBlocked = false; // true = 不能使用（例如正在裝備冷卻）

    // 你也可以在這裡掛 Animator、AudioSource 之類的通用引用
    // protected Animator anim;

    public virtual void OnEquip(Transform owner, WeaponSwitcher switcher)
    {
        this.owner = owner;
        this.switcher = switcher;

        // 啟用自己（保險）
        gameObject.SetActive(true);

        // 進入裝備冷卻：禁止使用
        if (equipLockDuration > 0f)
        {
            StopAllCoroutines();
            StartCoroutine(EquipLockCoroutine(equipLockDuration));
        }

        // TODO: 這裡可觸發「拿取武器動畫」的播放
        // 例如：anim?.SetTrigger("Equip");
        // 或 switcher.OnEquipStarted?.Invoke(this);
    }

    public virtual void OnUnequip()
    {
        // 如果有協程取消
        StopAllCoroutines();

        // 卸下時確保允許狀態重置（避免後面再被啟用時殘留）
        useBlocked = false;

        // 保險：可以選擇在切換器中統一 SetActive(false)，或這裡自己關閉
        // 這裡通常不關閉，由 WeaponSwitcher 統一控管 GameObject 啟用/關閉
    }

    public virtual void SetUseBlocked(bool blocked)
    {
        useBlocked = blocked;
    }

    public virtual bool CanUse()
    {
        return !useBlocked;
    }

    public virtual float GetEquipLockDuration()
    {
        return equipLockDuration;
    }

    protected IEnumerator EquipLockCoroutine(float duration)
    {
        useBlocked = true;
        // 等待裝備冷卻期（你也可以在這段時間做 UI 提示或動畫）
        yield return new WaitForSeconds(duration);
        useBlocked = false;

        // TODO: 若要在「裝備完成（可用）」時觸發事件，可在這裡呼叫
        // switcher?.OnEquipReady?.Invoke(this);
    }
}
