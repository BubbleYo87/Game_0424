using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// GameEvents.cs
using System;

public static class GameEvents
{
    // 1. 宣告一個全域事件 (帶 duration, magnitude 兩個參數)
    public static event Action<float, float> OnCameraShake;

    // 2. 觸發事件的方法
    public static void TriggerCameraShake(float duration, float magnitude)
        => OnCameraShake?.Invoke(duration, magnitude);
}


