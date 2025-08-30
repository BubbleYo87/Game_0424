// PlayerHealth.cs  （若你已有 DamageReceiver，就把死亡→呼叫重生接上即可）
using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("血量")]
    public float maxHP = 100;
    public float currentHP;

    [Header("受擊/死亡參數")]
    public bool isDead = false;
    public float deathDelay = 0.2f; // 死亡後延遲重生（可以播動畫/黑幕）

    [Header("無敵與閃爍(可選)")]
    public bool invincible = false;
    public float iFrameBlinkInterval = 0.08f;
    public Renderer[] blinkRenderers; // 放玩家身上的 Renderer 來做閃爍（可不填）

    private void Start()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        if (isDead || invincible) return;

        currentHP = Mathf.Max(0f, currentHP - amount);
        if (currentHP <= 0)
        {
            Die();
        }
        else
        {
            // 受擊反應（硬直 / 相機震動 / 音效...）
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // 關閉移動/輸入（依專案）
        var pm = GetComponent<PlayerMovement>();
        if (pm) pm.enabled = false;

        // 播死亡動畫/音效（可選）
        // GetComponent<Animator>()?.SetTrigger("Die");

        StartCoroutine(Co_RespawnAfterDelay());
    }

    private IEnumerator Co_RespawnAfterDelay()
    {
        yield return new WaitForSeconds(deathDelay);

        // 呼叫重生
        RespawnManager.Instance?.Respawn(gameObject);

        // 重新啟用移動/輸入
        var pm = GetComponent<PlayerMovement>();
        if (pm) pm.enabled = true;

        isDead = false;
    }

    /// <summary>重生時恢復血量等</summary>
    public void RespawnRestore()
    {
        currentHP = maxHP;
        // 清 Debuff、狀態，關特效...
    }

    /// <summary>給予短暫無敵，並可閃爍提示</summary>
    public void GiveIFrames(float seconds)
    {
        StopAllCoroutines();
        StartCoroutine(Co_IFrames(seconds));
    }

    private IEnumerator Co_IFrames(float seconds)
    {
        invincible = true;

        float t = 0f;
        bool visible = true;
        while (t < seconds)
        {
            t += iFrameBlinkInterval;
            visible = !visible;
            SetRenderersVisible(visible);
            yield return new WaitForSeconds(iFrameBlinkInterval);
        }

        SetRenderersVisible(true);
        invincible = false;
    }

    private void SetRenderersVisible(bool v)
    {
        if (blinkRenderers == null) return;
        foreach (var r in blinkRenderers)
        {
            if (r == null) continue;
            foreach (var m in r.materials)
            {
                // 最簡單作法：關閉整個Renderer
            }
            r.enabled = v;
        }
    }
}
