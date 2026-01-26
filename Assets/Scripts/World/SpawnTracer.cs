using System.Collections.Generic;
using UnityEngine;

public class SpawnTracer : MonoBehaviour
{
    HashSet<int> seen = new HashSet<int>();

    void Update()
    {
        var all = GameObject.FindObjectsOfType<Transform>(true);
        foreach (var t in all)
        {
            if (t == null) continue;
            var go = t.gameObject;
            if (!go.name.Contains("Wall_Varient_")) continue;

            int id = go.GetInstanceID();
            if (seen.Contains(id)) continue;
            seen.Add(id);

            Debug.LogWarning($"[SpawnTracer] NEW: {GetPath(t)} (active={go.activeSelf})", go);
        }
    }

    static string GetPath(Transform t)
    {
        var s = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            s = t.name + "/" + s;
        }
        return s;
    }
}