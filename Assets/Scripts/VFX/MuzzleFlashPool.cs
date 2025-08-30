using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 槍口火焰物件池（建議粒子為 Local 模式；Stop Action 設 Disable 或 None）
/// 用法：在武器 Inspector 指定 prefab，呼叫 PlayAt(muzzle)
/// </summary>
public class MuzzleFlashPool : MonoBehaviour
{
    [Header("Prefab / 預載")]
    [Tooltip("槍口火焰的預置體（需有 ParticleSystem）")]
    public GameObject muzzleFlashPrefab;
    

    [Tooltip("啟動時預先建立幾個（避免首發卡頓）")]
    public int prewarmCount = 4;

    [Tooltip("是否允許自動擴充（Pool 不夠時再生新的）")]
    public bool autoExpand = true;

    [Tooltip("上限（0=不限制）。達上限且不擴充時，會重用最早借出的那顆")]
    public int maxInstances = 0;

    // 內部
    private readonly Queue<GameObject> pool = new Queue<GameObject>();
    private readonly List<GameObject> inUse = new List<GameObject>();

    void Awake()
    {
        if (!muzzleFlashPrefab)
        {
            Debug.LogWarning("[MuzzleFlashPool] 未指定 muzzleFlashPrefab");
            return;
        }
        // 預熱
        for (int i = 0; i < prewarmCount; i++)
        {
            var go = CreateInstance();
            ReturnToPool(go);
        }
    }

    private GameObject CreateInstance()
    {
        var go = Instantiate(muzzleFlashPrefab, transform);    // 先掛到 Pool 根下
        go.name = muzzleFlashPrefab.name + "_Pooled";
        go.SetActive(false);

        // 建議：確保粒子 Simulation Space=Local；Stop Action=Disable 或 None
        return go;
    }

    private GameObject Get()
    {
        // 1) Pool 內有 → 直接取
        if (pool.Count > 0)
        {
            var go = pool.Dequeue();
            inUse.Add(go);
            return go;
        }

        // 2) Pool 空了 → 可擴充
        if (autoExpand || (maxInstances == 0) || (inUse.Count < maxInstances))
        {
            var go = CreateInstance();
            inUse.Add(go);
            return go;
        }

        // 3) 達上限且不擴充 → 重用最早借出的
        if (inUse.Count > 0)
        {
            var go = inUse[0];
            inUse.RemoveAt(0);
            return go;
        }

        // 理論不會到這
        return null;
    }

    private void ReturnToPool(GameObject go)
    {
        if (!go) return;
        go.transform.SetParent(transform, false);
        go.SetActive(false);
        pool.Enqueue(go);
        inUse.Remove(go);
    }

    /// <summary>
    /// 在指定 muzzle（通常是槍口 Transform）處播放一次火焰，並在粒子壽命結束後自動回收。
    /// </summary>
    public void PlayAt(Transform muzzle)
    {
        if (!muzzle || !muzzleFlashPrefab) return;

        var go = Get();
        if (!go) return;

        // 掛成子物件，跟著槍口一起動
        go.transform.SetParent(muzzle, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.SetActive(true);

        // 播放所有子粒子
        var particles = go.GetComponentsInChildren<ParticleSystem>(true);
        float maxDuration = 0.15f; // 最低保險時間
        foreach (var ps in particles)
        {
            ps.Clear(true);
            ps.Play(true);

            // 粗估總時長：系統時長 + 最大生命（對單次爆發足夠）
            var main = ps.main;
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
            maxDuration = Mathf.Max(maxDuration, dur + life);
        }

        // 自動回收
        StartCoroutine(Co_AutoReturn(go, maxDuration));
    }

    private IEnumerator Co_AutoReturn(GameObject go, float delay)
    {
        // 若 prefab 的根 PS 設了 Stop Action=Disable，這裡也可以縮短等待，但為保守仍用計時回收
        yield return new WaitForSeconds(delay);
        ReturnToPool(go);
    }
}
