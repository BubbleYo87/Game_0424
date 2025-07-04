using UnityEngine;
using UnityMeshSimplifier;

public class SimplifyMeshesForColliders : MonoBehaviour {
    [Header("Mesh Simplification Settings")]
    [Tooltip("简化质量，范围 0.1（极简） 到 1.0（完整）")]
    public float quality = 0.5f;

    void Start() {
        // 遍历当前物件及所有子物件中的 MeshFilter
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mf in meshFilters) {
            if (mf.sharedMesh == null) {
                Debug.LogError($"物件 {mf.gameObject.name} 没有 Mesh！");
                continue;
            }

            // 复制并简化 Mesh
            Mesh originalMesh = Instantiate(mf.sharedMesh);
            MeshSimplifier simplifier = new MeshSimplifier();
            simplifier.Initialize(originalMesh);
            simplifier.SimplifyMesh(quality);
            Mesh simplifiedMesh = simplifier.ToMesh();

            // 新增或获取 MeshCollider，并将简化后的 Mesh 指派给它
            MeshCollider meshCollider = mf.gameObject.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = mf.gameObject.AddComponent<MeshCollider>();
            
            meshCollider.sharedMesh = simplifiedMesh;
            meshCollider.convex = false; // 关闭 Convex，以确保低洼部分可行走

            Debug.Log($"为 {mf.gameObject.name} 添加/更新了 MeshCollider");
        }
    }
}
