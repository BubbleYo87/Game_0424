using UnityEngine;

/// <summary>
/// 提供給動畫層（或 UI/VFX）讀取的只讀資料介面。
/// 不要在這裡放任何控制行為的函式，維持「呈現層」單向依賴。
/// </summary>
public interface IEnemyChargerProvider
{
    float Speed { get; }             // 目前移動速度（給 Blend Tree）
    float RagePercent { get; }       // 怒氣 0~1
    float Awareness01 { get; }       // 察覺 0~1
    Transform RootTransform { get; } // 根 Transform（看向/朝向可用）
    string CurrentStateName { get; } // 目前狀態（"Idle/Chase/Dash/Breathe/Search"...）
}
