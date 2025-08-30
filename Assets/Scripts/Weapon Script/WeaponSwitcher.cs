using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 武器切換管理器：
/// - 監聽「滑鼠滾輪 / 數字鍵」輸入
/// - 切換武器（確保只有當前武器啟用）
/// - 具有「切換冷卻」（避免連續瞬切）
/// - 在切換成功後，對新武器套用「裝備冷卻」（一段時間不能使用）
/// - 預留 TODO：可在 OnEquipStarted / OnEquipReady 事件連接動畫
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    [Header("掛點與收集")]
    [Tooltip("武器群組根節點（其直屬子物件視為每一把武器）。")]
    public Transform weaponRoot;

    [Header("切換行為")]
    [Tooltip("切換後，多少秒內不能再次切換（避免瞬間連切）")]
    public float switchCooldown = 0.35f;

    [Tooltip("是否允許使用數字鍵 1~9 直接選擇武器")]
    public bool allowNumberKeys = true;

    [Tooltip("限制切換：開槍/重裝時是否禁止切換（若你的武器會回報狀態，可利用這個）")]
    public bool blockSwitchWhileUsing = false;

    [Header("輸入選項")]
    [Tooltip("使用舊 Input（GetAxis / mouseScrollDelta），若用新 Input System 可自行改寫")]
    public bool useLegacyInput = true;

    [Header("Debug")]
    [SerializeField] private int currentIndex = 0;     // 目前武器索引
    [SerializeField] private bool isSwitchCooling = false;

    // 收集到的武器物件與腳本
    private readonly List<GameObject> weaponGOs = new();
    private readonly List<IWeapon> weaponBehaviors = new();

    // 事件（你可在外部訂閱，例如 UI 或動畫控制器）
    public event Action<int, int> OnWeaponSwitched; // (oldIndex, newIndex)
    public event Action<IWeapon> OnEquipStarted;    // TODO: 播放拿取動畫時機
    public event Action<IWeapon> OnEquipReady;      // TODO: 動畫播完、可使用時機（如需外部把關）

    private void Awake()
    {
        if (weaponRoot == null)
            weaponRoot = transform; // 沒指定就用自己

        CollectWeapons();
        ActivateOnly(currentIndex);
    }

    private void Update()
    {
        HandleInput();
    }

    #region 輸入
    private void HandleInput()
    {
        if (weaponGOs.Count == 0) return;

        // 1) 數字鍵直接選武器
        if (allowNumberKeys)
        {
            for (int key = 1; key <= 9; key++)
            {
                if (Input.GetKeyDown(key.ToString()))
                {
                    int target = key - 1;
                    if (target < weaponGOs.Count)
                    {
                        TrySwitchTo(target);
                        return;
                    }
                }
            }
        }

        // 2) 滑鼠滾輪切換（舊 Input）
        if (useLegacyInput)
        {
            float scroll = Input.mouseScrollDelta.y; // >0 往上；<0 往下（有時滑鼠設定不同）
            if (scroll > 0.05f) { TrySwitchDelta(+1); return; }
            if (scroll < -0.05f) { TrySwitchDelta(-1); return; }
        }
        else
        {
            // 若你使用新 Input System，可在此改為讀取你的 Action（如 scroll/y）
            // e.g. float scroll = myActions.UI.Scroll.ReadValue<Vector2>().y;
        }
    }
    #endregion

    #region 切換核心
    /// <summary>
    /// 嘗試切換到下一把/上一把（dir=+1/-1）
    /// </summary>
    public void TrySwitchDelta(int dir)
    {
        if (dir == 0 || weaponGOs.Count <= 1) return;

        int target = WrapIndex(currentIndex + dir);
        TrySwitchTo(target);
    }

    /// <summary>
    /// 嘗試切換到指定索引位（會檢查切換冷卻、狀態等）
    /// </summary>
    public void TrySwitchTo(int targetIndex)
    {
        if (weaponGOs.Count == 0) return;
        if (targetIndex == currentIndex) return;

        // 切換冷卻中，直接忽略（實現「斷落感」）
        if (isSwitchCooling) return;

        // 若限制「使用中不能切換」
        if (blockSwitchWhileUsing)
        {
            var currentWpn = weaponBehaviors[currentIndex];
            if (currentWpn != null && !currentWpn.CanUse())
            {
                // 當前武器處於「不能用」（可能正在開槍、重裝），那就不允許切換
                return;
            }
        }

        StartCoroutine(SwitchCoroutine(targetIndex));
    }

    private IEnumerator SwitchCoroutine(int targetIndex)
    {
        isSwitchCooling = true;

        int old = currentIndex;
        int next = Mathf.Clamp(targetIndex, 0, weaponGOs.Count - 1);

        // 卸下舊武器
        var oldWpn = weaponBehaviors[old];
        oldWpn?.OnUnequip();
        weaponGOs[old].SetActive(false);

        currentIndex = next;

        // 啟用新武器
        var newGO = weaponGOs[currentIndex];
        var newWpn = weaponBehaviors[currentIndex];

        newGO.SetActive(true);

        // 通知裝備開始（TODO: 可在外部接「拿取武器」動畫）
        OnEquipStarted?.Invoke(newWpn);

        // 呼叫 OnEquip（其內會啟動裝備冷卻，暫時 SetUseBlocked(true)）
        newWpn?.OnEquip(weaponRoot, this);

        // 通知 UI：已切換索引
        OnWeaponSwitched?.Invoke(old, currentIndex);

        // 切換冷卻（避免瞬間連切）
        if (switchCooldown > 0f)
            yield return new WaitForSeconds(switchCooldown);

        isSwitchCooling = false;

        // 如果你要「等到武器可用」再丟事件，可另外在 WeaponBase 的冷卻結束時呼叫；
        // 這裡先提供一個立即的 OnEquipReady 範例（若不需要可移除）
        OnEquipReady?.Invoke(newWpn);
    }
    #endregion

    #region 初始化與工具
    private void CollectWeapons()
    {
        weaponGOs.Clear();
        weaponBehaviors.Clear();

        if (weaponRoot == null) return;

        // 只收集「直屬子物件」作為每一把武器（避免深層誤收）
        for (int i = 0; i < weaponRoot.childCount; i++)
        {
            Transform child = weaponRoot.GetChild(i);
            weaponGOs.Add(child.gameObject);

            // 找 IWeapon 實作（可在子層）
            IWeapon iw = child.GetComponentInChildren<IWeapon>(true);
            weaponBehaviors.Add(iw);
        }
    }

    private void ActivateOnly(int indexToEnable)
    {
        for (int i = 0; i < weaponGOs.Count; i++)
        {
            bool active = (i == indexToEnable);
            weaponGOs[i].SetActive(active);
            if (!active)
            {
                // 確保非當前武器狀態乾淨
                weaponBehaviors[i]?.SetUseBlocked(false);
                weaponBehaviors[i]?.OnUnequip();
            }
        }
        currentIndex = Mathf.Clamp(indexToEnable, 0, weaponGOs.Count - 1);

        // 啟動當前武器（通知）
        var cur = weaponBehaviors[currentIndex];
        weaponGOs[currentIndex].SetActive(true);
        cur?.OnEquip(weaponRoot, this);
    }

    private int WrapIndex(int idx)
    {
        if (weaponGOs.Count == 0) return 0;
        if (idx < 0) return weaponGOs.Count - 1;
        if (idx >= weaponGOs.Count) return 0;
        return idx;
    }
    #endregion
}
