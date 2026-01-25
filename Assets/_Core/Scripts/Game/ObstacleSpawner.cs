using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;      // Player Transform
    public PlayerMotor playerMotor;
    public GameManager gameManager;

    [Header("Prefabs (set in Inspector)")]
    public GameObject deskPrefab;     // Desk (Jump)
    public GameObject bannerPrefab;   // Banner (Roll)
    public GameObject lockerPrefab;   // Locker (Lane move)

    [Header("Spawn Space")]
    public float spawnZOffset = 35f;
    public float laneWidth = 1.2f;
    public float cleanupBehindZ = 10f;

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
        nextSpawnAt = 0.5f;
    }

    void Update()
    {
        if (player == null) return;
        if (gameManager != null && gameManager.State != GameState.Playing) return;

        t += Time.deltaTime;

        if (t >= nextSpawnAt)
        {
            SpawnByStage();
            nextSpawnAt = t + CurrentInterval();
        }

        CleanupOldObstacles();
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

        GameObject prefab = RandomObstaclePrefab();
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
        if (r == 0 && deskPrefab != null) return deskPrefab;
        if (r == 1 && bannerPrefab != null) return bannerPrefab;
        if (r == 2 && lockerPrefab != null) return lockerPrefab;

        if (deskPrefab != null) return deskPrefab;
        if (bannerPrefab != null) return bannerPrefab;
        return lockerPrefab;
    }

    void Spawn(GameObject prefab, int lane)
    {
        if (prefab == null) return;

        float x = lane * laneWidth;
        float z = player.position.z + spawnZOffset;

        // ✅ prefab 종류에 따라 Y를 다르게 준다
        float y = 0f;
        if (prefab == deskPrefab) y = deskWorldY;
        else if (prefab == lockerPrefab) y = lockerWorldY;
        else if (prefab == bannerPrefab) y = bannerWorldY;
        else y = 0f; // fallback

        Vector3 pos = new Vector3(x, y, z);

        GameObject go = Instantiate(prefab, pos, Quaternion.identity);
        go.name = $"{prefab.name}_lane{lane}_{(int)t}s";
    }

    void CleanupOldObstacles()
    {
        var obs = GameObject.FindGameObjectsWithTag("Obstacle");
        float destroyZ = player.position.z - cleanupBehindZ;

        foreach (var o in obs)
        {
            if (o.transform.position.z < destroyZ)
                Destroy(o);
        }
    }
}
