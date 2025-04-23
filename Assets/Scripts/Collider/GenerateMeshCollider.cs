using UnityEngine;

public class GenerateMeshCollider : MonoBehaviour {
    void Start() {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) {
            Debug.LogError("没有找到 MeshFilter 或 Mesh！");
            return;
        }

        // 确保 Mesh 可读
        Mesh mesh = Instantiate(meshFilter.sharedMesh);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // 创建 MeshCollider
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null) {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        meshCollider.sharedMesh = mesh;
        meshCollider.convex = false; // 关闭 Convex，确保低洼部分有碰撞
    }
}

