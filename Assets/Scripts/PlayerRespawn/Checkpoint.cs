// Checkpoint.cs
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Header("識別")]
    [Tooltip("本重生點的唯一編號（同場景內不可重複）。可用 0,1,2…")] 
    public int checkpointId = 0;

    [Header("外觀/提示(可選)")]
    [Tooltip("玩家啟用此重生點時要打開的特效/光柱/UI")]
    public GameObject activateVfx;
    [Tooltip("是否在 Scene 視窗畫出Gizmos")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.6f);

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // 設為觸發器
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // 設定為目前啟用的重生點
        RespawnManager.Instance?.SetActiveCheckpoint(this);

        if (activateVfx != null) activateVfx.SetActive(true);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position + Vector3.up * 1.2f, 0.2f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(0.6f, 2f, 0.6f));
    }
}
