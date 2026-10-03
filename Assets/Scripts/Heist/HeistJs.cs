using System.Runtime.InteropServices;
using UnityEngine;

public static class HeistJs
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void HeistNotifyComplete(string json);
    [DllImport("__Internal")] static extern void HeistNotifyJoinCode(string code);
#endif

    public static void Complete(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        HeistNotifyComplete(json);
#else
        Debug.Log(json);
#endif
    }

    public static void JoinCode(string code)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        HeistNotifyJoinCode(code);
#else
        Debug.Log("Join code " + code);
#endif
    }
}
