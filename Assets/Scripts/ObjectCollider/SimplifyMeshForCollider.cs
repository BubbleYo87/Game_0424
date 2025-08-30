using UnityEngine;
using UnityMeshSimplifier;

public class SimplifyMeshForCollider : MonoBehaviour {
    public float quality = 0.5f; // 质量 0.1（极简） - 1.0（完整）
    
    void Start() {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) {
            Debug.LogError("未找到 MeshFilter 或 Mesh！");
            return;
        }

        // 复制 Mesh 并简化
        Mesh originalMesh = Instantiate(meshFilter.sharedMesh);
        MeshSimplifier simplifier = new MeshSimplifier();
        simplifier.Initialize(originalMesh);
        simplifier.SimplifyMesh(quality);
        Mesh simplifiedMesh = simplifier.ToMesh();

        // 应用到 MeshCollider
        MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = simplifiedMesh;
        meshCollider.convex = false; // 关闭 Convex，确保低洼部分可行走
    }
}
