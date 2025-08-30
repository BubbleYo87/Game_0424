// Assets/Scripts/Combat/MeleeWeapon.cs
using UnityEngine;
using System.Collections;

public class MeleeWeapon : MonoBehaviour
{
    [Header("引用")]
    public MeleeHitbox hitbox;        // 子物件 Hitbox
    public Transform ownerRoot;       // 玩家根物件（避免自傷）
    public Transform swingOrigin;     // 揮擊參考點（可不填，用於特效/音效）

    [Header("數值")]
    public float baseDamage  = 30f;
    public float windup      = 0.10f;   // 蓄力（按下→出手）
    public float activeTime  = 0.15f;   // 有效窗（Hitbox 開啟時間）
    public float cooldown    = 0.25f;   // 冷卻（結束→可再揮）

    [Header("輸入")]
    public KeyCode attackKey = KeyCode.Mouse0;

    [Header("手感（可選）")]
    public bool   cameraPunch = true;
    public Camera fpCamera;
    public float  punchPitch = 2.0f;
    public float  punchReturnSpeed = 10f;

    private bool busy;
    private float pendingPitch;

    void Reset()
    {
        // 嘗試自動抓子物件 Hitbox
        if (!hitbox) hitbox = GetComponentInChildren<MeleeHitbox>();
    }

    void Update()
    {
        // 輸入：無動畫版，直接在這裡讀
        if (Input.GetKeyDown(attackKey) && !busy)
            StartCoroutine(SwingRoutine());

        // 簡易鏡頭回彈
        if (cameraPunch && fpCamera && Mathf.Abs(pendingPitch) > 0.0001f)
        {
            float step = Mathf.Sign(pendingPitch) * Mathf.Min(Mathf.Abs(pendingPitch), punchReturnSpeed * Time.deltaTime);
            fpCamera.transform.localRotation *= Quaternion.Euler(step, 0f, 0f);
            pendingPitch -= step;
        }
    }

    private IEnumerator SwingRoutine()
    {
        busy = true;

        // 1) 蓄力（可在此播預備音效/Trail 啟動等）
        yield return new WaitForSeconds(windup);

        // 2) 開啟有效窗（配置此次傷害、避免打到自己）
        if (hitbox)
            hitbox.Configure(baseDamage, true, ownerRoot);

        // 3) 鏡頭小幅下壓，營造出手感
        if (cameraPunch && fpCamera)
        {
            fpCamera.transform.localRotation *= Quaternion.Euler(-punchPitch, 0f, 0f);
            pendingPitch += punchPitch;
        }

        // 4) 有效窗持續
        yield return new WaitForSeconds(activeTime);

        // 5) 關閉有效窗
        if (hitbox)
            hitbox.EnableHit(false);

        // 6) 冷卻
        yield return new WaitForSeconds(cooldown);
        busy = false;
    }

    // —— 之後若要改用「動畫事件」 —— //
    // 在動畫關鍵幀呼叫以下函式即可替代協程：
    public void AE_BeginSwing()  { if (hitbox) hitbox.Configure(baseDamage, true, ownerRoot); }
    public void AE_EndSwing()    { if (hitbox) hitbox.EnableHit(false); }
}
