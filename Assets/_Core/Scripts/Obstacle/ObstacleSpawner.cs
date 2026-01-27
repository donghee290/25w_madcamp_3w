using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;      // Player Transform
    public PlayerMotor playerMotor;
    public GameManager gameManager;
    public SpawnsRoot spawnsRoot; // 추가.

    [Header("Prefabs (set in Inspector)")]
    public GameObject[] jumpPrefabs;        // Desk (Jump)
    public GameObject[] rollPrefabs;        // Banner (Roll)
    public GameObject[] movePrefabs;

    [Header("Spawn Space")]
    public float spawnZOffset = 35f;
    public float laneWidth = 1.2f;

    [Header("World Y per obstacle (IMPORTANT)")]
    [Tooltip("책상(바닥 장애물) 생성 높이")]
    public float deskWorldY = 0.5f;

    [Tooltip("사물함(큰 장애물) 생성 높이")]
    public float lockerWorldY = 1.1f;

    [Tooltip("배너(롤 장애물) 생성 높이 - 플레이어 머리보다 높게!")]
    public float bannerWorldY = 2.2f;

    [Header("Difficulty Timing")]
    public float stage1End = 20f;
    public float stage2End = 40f;

    [Header("Spawn Interval")]
    public float intervalStage1 = 1.6f;
    public float intervalStage2 = 1.2f;
    public float intervalStage3 = 0.85f;

    private float t = 0f;
    private float nextSpawnAt = 0f;

    void Start()
    {
        if (gameManager == null) gameManager = GameManager.I;
        if (spawnsRoot == null) spawnsRoot = FindFirstObjectByType<SpawnsRoot>();
        if (spawnsRoot != null && spawnsRoot.player == null && player != null) spawnsRoot.player = player;

        nextSpawnAt = 0.5f;
    }

    void Update()
    {
        if (player == null) return;
        if (gameManager != null && gameManager.State != GameState.Playing) return;
        if (spawnsRoot == null) return;

        t += Time.deltaTime;

        if (t >= nextSpawnAt)
        {
            SpawnByStage();
            nextSpawnAt = t + CurrentInterval();
        }

        // CleanupOldObstacles() 삭제: SpawnsRoot가 관리/정리
    }

    float CurrentInterval()
    {
        if (t < stage1End) return intervalStage1;
        if (t < stage2End) return intervalStage2;
        return intervalStage3;
    }

    void SpawnByStage()
    {
        if (t < stage1End)
        {
            SpawnSingle();
        }
        else if (t < stage2End)
        {
            if (Random.value < 0.7f) SpawnSingle();
            else SpawnDouble();
        }
        else
        {
            SpawnSingle();
            if (Random.value < 0.55f) Invoke(nameof(SpawnSingle), 0.25f);
        }
    }

    void SpawnSingle()
    {
        int lane = RandomLane();
        GameObject prefab = RandomObstaclePrefab();
        Spawn(prefab, lane);
    }

    void SpawnDouble()
    {
        int laneA = RandomLane();
        int laneB = laneA;
        while (laneB == laneA) laneB = RandomLane();

        const int maxTries = 20;
        GameObject prefab = null;

        for (int i = 0; i < maxTries; i++)
        {
            var p = RandomObstaclePrefab();
            if (p == null) continue;

            var m = p.GetComponent<ObstacleMarker>();
            int span = (m != null) ? Mathf.Clamp(m.laneSpan, 1, 3) : 1;

            if (span == 1)
            {
                prefab = p;
                break;
            }
        }

        if (prefab == null) return;

        Spawn(prefab, laneA);
        Spawn(prefab, laneB);
    }

    int RandomLane()
    {
        int r = Random.Range(0, 3);
        return (r == 0) ? -1 : (r == 1 ? 0 : 1);
    }

    GameObject RandomObstaclePrefab()
    {
        int r = Random.Range(0, 3);

        if (r == 0 && jumpPrefabs != null && jumpPrefabs.Length > 0)
            return jumpPrefabs[Random.Range(0, jumpPrefabs.Length)];

        if (r == 1 && rollPrefabs != null && rollPrefabs.Length > 0)
            return rollPrefabs[Random.Range(0, rollPrefabs.Length)];

        if (movePrefabs != null && movePrefabs.Length > 0)
            return movePrefabs[Random.Range(0, movePrefabs.Length)];

        if (jumpPrefabs != null && jumpPrefabs.Length > 0) return jumpPrefabs[0];
        if (rollPrefabs != null && rollPrefabs.Length > 0) return rollPrefabs[0];
        return null;
    }

    void Spawn(GameObject prefab, int lane)
    {
        if (prefab == null) return;

        var marker = prefab.GetComponent<ObstacleMarker>();
        int span = (marker != null) ? Mathf.Clamp(marker.laneSpan, 1, 3) : 1;

        float x;
        if (span == 3)
        {
            lane = 0;
            x = 0f;
        }
        else if (span == 2)
        {
            lane = (Random.value < 0.5f) ? -1 : 0;
            float centerLane = (lane == -1) ? -0.5f : 0.5f;
            x = centerLane * laneWidth;
        }
        else
        {
            x = lane * laneWidth;
        }

        float z = player.position.z + spawnZOffset;

        float y;
        if (marker != null && marker.type == ObstacleType.Jump) y = deskWorldY;
        else if (marker != null && marker.type == ObstacleType.Roll) y = bannerWorldY;
        else y = lockerWorldY;

        Vector3 pos = new Vector3(x, y, z);

        GameObject go = spawnsRoot.SpawnObstacle(prefab, pos, prefab.transform.rotation);
        if (go == null) return;

        go.name = $"{prefab.name}_span{span}_lane{lane}_{(int)t}s";
    }
}