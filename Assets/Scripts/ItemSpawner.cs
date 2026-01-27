using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject wingsPrefab;

    [Tooltip("A단계: 맵에 랜덤 생성되는 바나나(먹는 아이템). BananaPickup 붙어있어야 함")]
    public GameObject bananaPickupPrefab;

    [Tooltip("B단계: 먹는 순간 뒤로 떨어지는 바나나(연출용). 없어도 됨")]
    public GameObject bananaDropPrefab;

    [Header("Refs")]
    public Transform player;

    [Header("Lane")]
    public float laneWidth = 1.2f;
    public float spawnY = 1.6f;
    public float spawnAheadZ = 22f;

    [Header("Wings Spawn")]
    public float wingsCheckInterval = 8f;
    [Range(0f, 1f)] public float wingsChance = 0.07f;

    [Header("Banana Spawn")]
    public float bananaCheckInterval = 6f;
    [Range(0f, 1f)] public float bananaChance = 0.10f;

    [Header("Banana Mode")]
    [Tooltip("켜면 B단계(드랍 연출)까지 같이 사용. 꺼두면 A단계(즉시 스턴)만")]
    public bool enableBananaDropVisual = false;

    float wingsT;
    float bananaT;

    void Update()
    {
        if (player == null) return;
        if (GameManager.I != null && GameManager.I.State != GameState.Playing) return;

        // Wings
        if (wingsPrefab != null)
        {
            wingsT += Time.deltaTime;
            if (wingsT >= wingsCheckInterval)
            {
                wingsT = 0f;
                if (Random.value <= wingsChance)
                    SpawnOnRandomLane(wingsPrefab);
            }
        }

        // Banana pickup (A단계)
        if (bananaPickupPrefab != null)
        {
            bananaT += Time.deltaTime;
            if (bananaT >= bananaCheckInterval)
            {
                bananaT = 0f;
                if (Random.value <= bananaChance)
                {
                    var go = SpawnOnRandomLane(bananaPickupPrefab);

                    // B단계 프리팹을 BananaPickup에 주입(원하면 Inspector에서 직접 넣어도 됨)
                    if (enableBananaDropVisual && bananaDropPrefab != null && go != null)
                    {
                        var pickup = go.GetComponent<BananaPickup>();
                        if (pickup != null && pickup.bananaDropPrefab == null)
                            pickup.bananaDropPrefab = bananaDropPrefab;
                    }
                }
            }
        }
    }

    GameObject SpawnOnRandomLane(GameObject prefab)
    {
        int lane = Random.Range(-1, 2); // -1,0,1
        float x = lane * laneWidth;

        Vector3 pos = new Vector3(x, spawnY, player.position.z + spawnAheadZ);
        return Instantiate(prefab, pos, Quaternion.identity);
    }
}