using UnityEngine;

public class GenerateMeshCollidersOnChildren : MonoBehaviour
{
    void Start()
    {
        // 获取当前物件自身的 MeshFilter（用于跳过）
        MeshFilter ownFilter = GetComponent<MeshFilter>();

        // 遍历所有子物件（包括多级嵌套）
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mf in meshFilters)
        {
            // 跳过自身，如果父物件也有 MeshFilter
            if (mf == ownFilter) 
                continue;

            if (mf.sharedMesh == null)
            {
                Debug.LogWarning($"子物件 “{mf.gameObject.name}” 的 MeshFilter 没有 Mesh，跳过。");
                continue;
            }

            // 克隆 Mesh 保证可读，并重计算法线和包围盒
            Mesh meshInstance = Instantiate(mf.sharedMesh);
            meshInstance.RecalculateNormals();
            meshInstance.RecalculateBounds();

            // 获取或添加 MeshCollider
            MeshCollider mc = mf.GetComponent<MeshCollider>();
            if (mc == null)
                mc = mf.gameObject.AddComponent<MeshCollider>();

            mc.sharedMesh = meshInstance;
            mc.convex = false;  // 关闭 convex 保证低洼处也能碰撞
        }
    }
}
