using UnityEngine;
using System.Collections;

/// <summary>
/// 固定在場景的 VFX 切換器：常駐在指定位置，透過 Show/Hide/Pulse 控制顯示。
/// 建議：粒子 Simulation Space=Local，Play On Awake=關，Stop Action=None 或 Disable。
/// </summary>
public class FixedVFXToggle : MonoBehaviour
{
    [Header("開場狀態")]
    public bool startHidden = true;         // 一開始是否隱藏（保留腳本可呼叫）

    [Header("控制方式")]
    [Tooltip("若勾選：只切換 Renderer.enabled（物件保持啟用）。未勾選：整個 GameObject SetActive 切換。")]
    public bool hideByRendererOnly = true;  // 建議勾選：可在不 Disable 物件的情況下操作粒子

    private ParticleSystem[] ps;
    private Renderer[] rends;
    private Coroutine autoHideCo;

    void Awake()
    {
        ps = GetComponentsInChildren<ParticleSystem>(true);
        rends = GetComponentsInChildren<Renderer>(true);

        if (startHidden) HideImmediate();
        else StopAndClear(); // 確保初始是乾淨的
    }

    /// <summary>顯示並播放所有粒子</summary>
    public void Show()
    {
        if (hideByRendererOnly)
        {
            foreach (var r in rends) if (r) r.enabled = true;
            // 重新播放
            foreach (var p in ps) { if (!p) continue; p.Clear(true); p.Play(true); }
        }
        else
        {
            gameObject.SetActive(true);
            // 重新抓一次，避免 OnEnable 時機問題
            if (ps == null || ps.Length == 0) ps = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in ps) { if (!p) continue; p.Clear(true); p.Play(true); }
        }
    }

    /// <summary>隱藏並停止所有粒子</summary>
    public void Hide()
    {
        if (hideByRendererOnly)
        {
            // 先停止再關 renderer，避免殘留
            StopAndClear();
            foreach (var r in rends) if (r) r.enabled = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>顯示一段時間後自動隱藏（時間不填就依粒子最大時長估算）</summary>
    public void Pulse(float seconds = -1f)
    {
        if (autoHideCo != null) StopCoroutine(autoHideCo);
        Show();
        float d = (seconds > 0f) ? seconds : ComputeMaxDuration();
        autoHideCo = StartCoroutine(Co_AutoHide(d));
    }

    public void HideImmediate()
    {
        if (hideByRendererOnly)
        {
            foreach (var r in rends) if (r) r.enabled = false;
            StopAndClear();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void StopAndClear()
    {
        if (ps == null) return;
        foreach (var p in ps)
        {
            if (!p) continue;
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            p.Clear(true);
        }
    }

    private float ComputeMaxDuration()
    {
        float maxDur = 0.05f;
        if (ps == null) return maxDur;
        foreach (var p in ps)
        {
            if (!p) continue;
            var main = p.main;
            float dur = main.duration;
            float life = 0f;
            var lt = main.startLifetime;
            switch (lt.mode)
            {
                case ParticleSystemCurveMode.TwoConstants: life = lt.constantMax; break;
                case ParticleSystemCurveMode.TwoCurves:    life = lt.constantMax; break; // 近似
                case ParticleSystemCurveMode.Curve:        life = lt.constant;    break; // 近似
                default:                                   life = lt.constant;    break;
            }
            maxDur = Mathf.Max(maxDur, dur + life);
        }
        return maxDur;
    }

    private IEnumerator Co_AutoHide(float delay)
    {
        yield return new WaitForSeconds(delay);
        Hide();
        autoHideCo = null;
    }
}
