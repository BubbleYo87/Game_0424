// TriggerEcho.cs
using UnityEngine;

public class TriggerEcho : MonoBehaviour
{
    [Header("Debug")]
    public bool logEnter = true;
    public bool logStay = false;
    public bool logExit = true;

    private void OnTriggerEnter(Collider other)
    {
        if (logEnter)
            Debug.Log($"[TriggerEcho][ENTER] {name}({LayerMask.LayerToName(gameObject.layer)}) <-> {other.name}({LayerMask.LayerToName(other.gameObject.layer)})", this);
    }

    private void OnTriggerStay(Collider other)
    {
        if (logStay)
            Debug.Log($"[TriggerEcho][STAY] {name} <-> {other.name}", this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (logExit)
            Debug.Log($"[TriggerEcho][EXIT] {name} <-> {other.name}", this);
    }
}
