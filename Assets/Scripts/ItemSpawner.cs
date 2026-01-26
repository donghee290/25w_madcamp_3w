using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public GameObject wingsPrefab;
    public Transform player;

    [Header("Lane")]
    public float laneWidth = 1.2f;
    public float spawnY = 1.6f;
    public float spawnAheadZ = 22f;

    [Header("Rare Spawn")]
    public float checkInterval = 8f;      // 8초마다 체크
    [Range(0f, 1f)] public float chance = 0.07f; // 7%면 “매우 간간이” 느낌

    float t;

    void Update()
    {
        if (wingsPrefab == null || player == null) return;
        if (GameManager.I != null && GameManager.I.State != GameState.Playing) return;

        t += Time.deltaTime;
        if (t < checkInterval) return;
        t = 0f;

        if (Random.value > chance) return;

        int lane = Random.Range(-1, 2); // -1,0,1
        float x = lane * laneWidth;

        Vector3 pos = new Vector3(x, spawnY, player.position.z + spawnAheadZ);
        Instantiate(wingsPrefab, pos, Quaternion.identity);
    }
}