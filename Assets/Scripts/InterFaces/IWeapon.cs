using UnityEngine;

/// <summary>
/// 武器介面：讓 WeaponSwitcher 可以「通知武器被裝備/卸下」
/// 並控制「是否暫時禁止使用（例如切換後的裝備冷卻）」
/// </summary>
public interface IWeapon
{
    /// <summary>當此武器被裝備（啟用）時由管理器呼叫。</summary>
    /// <param name="owner">一般是玩家或 WeaponRoot</param>
    /// <param name="switcher">武器切換管理器，可用於查詢上下文或訂閱事件</param>
    void OnEquip(Transform owner, WeaponSwitcher switcher);

    /// <summary>當此武器被卸下（停用）時由管理器呼叫。</summary>
    void OnUnequip();

    /// <summary>設定是否暫時禁止使用（例如切換後的裝備冷卻）。</summary>
    void SetUseBlocked(bool blocked);

    /// <summary>查詢目前是否允許使用（例如開火）。</summary>
    bool CanUse();

    /// <summary>回傳此武器「裝備後要鎖多久不能使用」；若用 WeaponBase 可由欄位決定。</summary>
    float GetEquipLockDuration();
}
