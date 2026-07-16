#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class DebugFlagPositions
{
    [MenuItem("Tools/Debug Flag Positions")]
    public static void CheckFlags()
    {
        var flags = GameObject.FindGameObjectsWithTag("Flag");
        Debug.Log($"Found {flags.Length} objects with tag 'Flag'.");
        foreach(var f in flags)
        {
            Debug.Log($"Tag Flag: {f.name} at {f.transform.position}");
        }
    }
}
#endif
