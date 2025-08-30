// RespawnManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [Header("重生點列表(可選)")]
    [Tooltip("如果不手動拉，會在 Start 自動搜尋場景中的 Checkpoint")]
    public List<Checkpoint> checkpoints = new List<Checkpoint>();

    [Header("預設出生點")]
    [Tooltip("如果還沒啟用任何重生點，會使用這個 Transform 當出生位置")]
    public Transform defaultSpawnPoint;

    [Header("保存重生點(可選)")]
    [Tooltip("是否使用 PlayerPrefs 保存最後啟用的重生點")]
    public bool saveLastCheckpoint = true;

    private Checkpoint _activeCheckpoint;
    private const string PREF_KEY = "LAST_CHECKPOINT_ID_";

    private void Awake()
    {
        // 單例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 可選：跨場景保留
        // DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 自動抓場景所有 Checkpoint
        if (checkpoints == null || checkpoints.Count == 0)
        {
            checkpoints = new List<Checkpoint>(FindObjectsOfType<Checkpoint>());
        }

        // 載入上次的重生點
        if (saveLastCheckpoint)
        {
            string key = PREF_KEY + SceneManager.GetActiveScene().buildIndex;
            if (PlayerPrefs.HasKey(key))
            {
                int lastId = PlayerPrefs.GetInt(key, -1);
                var found = checkpoints.Find(c => c.checkpointId == lastId);
                if (found != null) _activeCheckpoint = found;
            }
        }
    }

    /// <summary>
    /// 設定目前啟用的重生點
    /// </summary>
    public void SetActiveCheckpoint(Checkpoint cp)
    {
        _activeCheckpoint = cp;

        if (saveLastCheckpoint && cp != null)
        {
            string key = PREF_KEY + SceneManager.GetActiveScene().buildIndex;
            PlayerPrefs.SetInt(key, cp.checkpointId);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// 取得重生位置（優先返回啟用的 Checkpoint，其次 defaultSpawnPoint）
    /// </summary>
    public Vector3 GetRespawnPosition()
    {
        if (_activeCheckpoint != null) return _activeCheckpoint.transform.position;
        if (defaultSpawnPoint != null) return defaultSpawnPoint.position;
        // 如果什麼都沒設，就回傳世界原點
        return Vector3.zero;
    }

    /// <summary>
    /// 取得重生朝向（可依需求回傳 Checkpoint forward）
    /// </summary>
    public Quaternion GetRespawnRotation()
    {
        if (_activeCheckpoint != null) return _activeCheckpoint.transform.rotation;
        if (defaultSpawnPoint != null) return defaultSpawnPoint.rotation;
        return Quaternion.identity;
    }

    /// <summary>
    /// 核心：把玩家重置到重生點，並重置剛體/狀態/血量等。
    /// </summary>
    public void Respawn(GameObject player)
    {
        if (player == null) return;

        // 1) 位置與朝向
        var rb = player.GetComponent<Rigidbody>();
        var targetPos = GetRespawnPosition();
        var targetRot = GetRespawnRotation();

        // 關閉一次碰撞移動中可能觸發的異常（可選）
        bool origDetect = true;
        if (rb)
        {
            origDetect = rb.detectCollisions;
            rb.detectCollisions = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        player.transform.SetPositionAndRotation(targetPos, targetRot);

        // 2) 重置你的移動/狀態機（依你的專案調整）
        ResetPlayerState(player);

        // 3) 回復碰撞
        if (rb)
        {
            rb.detectCollisions = origDetect;
        }

        // 4) 回滿血或設定重生血量
        var hp = player.GetComponent<PlayerHealth>();
        if (hp) hp.RespawnRestore();

        // 5) 可選：短暫無敵
        if (hp) hp.GiveIFrames(0.8f); // 0.8秒無敵
    }

    /// <summary>
    /// 重生時重置你專案的各類狀態（依需求修改）
    /// </summary>
    private void ResetPlayerState(GameObject player)
    {
        // 關閉各種暫態效果
        // e.g. 你的 PlayerMovement / DoubleTapDash / Grappling / WallRun 等
        var pm = player.GetComponent<PlayerMovement>();
        if (pm)
        {
            pm.dashing = false;
            // 其他旗標視你的程式碼補上…
        }

        var dtd = player.GetComponent<DoubleTapDash>();
        if (dtd && dtd.playerCamera != null)
        {
            // 還原 FOV / 模糊（避免玩家死前特效殘留）
            dtd.playerCamera.fieldOfView = dtd.playerCamera.fieldOfView; // 如果有原始值可還原就還原
            // 建議在 DoubleTapDash 內提供一個 ResetEffects() 來呼叫
            // dtd.ResetEffects();
        }

        // 若有 Grappling 系統：關閉繩索、清掉 Joint/LineRenderer
        // 若有 WallRun：關閉牆跑狀態
        // 若有 Animator：切回 Idle/Locomotion layer
        // …依你的專案補上
    }
}
