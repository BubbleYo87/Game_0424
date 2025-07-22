using UnityEngine;

public class PrefabSpawner : MonoBehaviour
{
    [Header("要生成的 Prefab")]
    public GameObject prefab;

    [Header("生成位置的 Transform")]
    public Transform spawnPoint;

    [Header("按下這個鍵就會生成 Prefab")]
    public KeyCode keyToPress = KeyCode.Space;

    void Update()
    {
        // 當玩家按下設定的鍵
        if (Input.GetKeyDown(keyToPress))
        {
            if (prefab == null)
            {
                Debug.LogWarning("Prefab 尚未設定！請在 Inspector 指定。");
                return;
            }

            // 決定生成的位置與旋轉（若沒設 spawnPoint，預設 (0,0,0)／Quaternion.identity）
            Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            // 執行生成
            Instantiate(prefab, pos, rot);
        }
    }
}
