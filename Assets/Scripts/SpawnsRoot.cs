using System.Collections.Generic;
using UnityEngine;

public class SpawnsRoot : MonoBehaviour
{
    [Header("Parents (auto-created if missing)")]
    public Transform itemsParent;
    public Transform obstaclesParent;

    [Header("Refs")]
    public Transform player;

    [Tooltip("player 뒤로 이 거리만큼 지나가면 제거")]
    public float cleanupBehindZ = 15f;

    private readonly List<Transform> _items = new();
    private readonly List<Transform> _obstacles = new();

    void Awake()
    {
        EnsureParents();
    }

    void Update()
    {
        if (player == null) return;
        Cleanup();
    }

    void EnsureParents()
    {
        if (itemsParent == null)
        {
            var t = transform.Find("Items");
            if (t != null) itemsParent = t;
            else
            {
                var go = new GameObject("Items");
                go.transform.SetParent(transform, false);
                itemsParent = go.transform;
            }
        }

        if (obstaclesParent == null)
        {
            var t = transform.Find("Obstacles");
            if (t != null) obstaclesParent = t;
            else
            {
                var go = new GameObject("Obstacles");
                go.transform.SetParent(transform, false);
                obstaclesParent = go.transform;
            }
        }
    }

    public GameObject SpawnItem(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;
        EnsureParents();

        var go = Instantiate(prefab, pos, rot, itemsParent);
        _items.Add(go.transform);
        return go;
    }

    public GameObject SpawnObstacle(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;
        EnsureParents();

        var go = Instantiate(prefab, pos, rot, obstaclesParent);
        _obstacles.Add(go.transform);
        return go;
    }

    void Cleanup()
    {
        float destroyZ = player.position.z - cleanupBehindZ;

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var tr = _items[i];
            if (tr == null) { _items.RemoveAt(i); continue; }
            if (tr.position.z < destroyZ)
            {
                Destroy(tr.gameObject);
                _items.RemoveAt(i);
            }
        }

        for (int i = _obstacles.Count - 1; i >= 0; i--)
        {
            var tr = _obstacles[i];
            if (tr == null) { _obstacles.RemoveAt(i); continue; }
            if (tr.position.z < destroyZ)
            {
                Destroy(tr.gameObject);
                _obstacles.RemoveAt(i);
            }
        }
    }
}