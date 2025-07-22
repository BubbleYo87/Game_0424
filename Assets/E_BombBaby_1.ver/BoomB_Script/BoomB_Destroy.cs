using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoomB_Destroy : MonoBehaviour
{
    /// <summary>
    /// 關閉父物件的所有 Collider 與 Renderer，並在 5 秒後銷毀父物件
    /// （可從 Animation Event 或程式邏輯中呼叫）
    /// </summary>
    public void DestroyParent()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        GameObject parentGO = parent.gameObject;

        // 1. 關閉所有 Collider（包括子孫物件上的）
        Collider[] colliders = parentGO.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // 2. 關閉所有 Renderer（MeshRenderer、SkinnedMeshRenderer、SpriteRenderer…）
        //    確保模型及各種可視化都隱藏
        Renderer[] renderers = parentGO.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            rend.enabled = false;
        }

        // 3. 延遲 5 秒後才真正銷毀父物件
        Destroy(parentGO, 5f);
    }
}
