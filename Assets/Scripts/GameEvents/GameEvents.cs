using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameEvents
{
    // 1. 宣告一個全域事件
    public static event Action OnCameraShake;
    // Start is called before the first frame update
    // 2. 觸發事件的方法
    public static void TriggerCameraShake()
        => OnCameraShake?.Invoke();

}
