using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;      // Player Transform
    public PlayerMotor playerMotor;
    public GameManager gameManager;

    [Header("Prefabs (set in Inspector)")]
    public GameObject[] jumpPrefabs;        // Desk (Jump)
    public GameObject[] rollPrefabs;        // Banner (Roll)  ✅ 여러 개로 변경
    public GameObject[] movePrefabs;

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
        // 서로 다른 두 레인 선택(-1,0,1)
        int laneA = RandomLane();
        int laneB = laneA;
        while (laneB == laneA) laneB = RandomLane();

        // span==1인 프리팹만 뽑기 (최대 N번 시도 후 실패하면 종료)
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

        // ✅ Roll: 단일 bannerPrefab -> rollPrefabs 배열 랜덤
        if (r == 1 && rollPrefabs != null && rollPrefabs.Length > 0)
            return rollPrefabs[Random.Range(0, rollPrefabs.Length)];

        if (movePrefabs != null && movePrefabs.Length > 0)
            return movePrefabs[Random.Range(0, movePrefabs.Length)];

        // fallback
        if (jumpPrefabs != null && jumpPrefabs.Length > 0) return jumpPrefabs[0];
        if (rollPrefabs != null && rollPrefabs.Length > 0) return rollPrefabs[0];
        return null;
    }

    GameObject RandomSingleLaneMovePrefab()
    {
        if (movePrefabs == null) return null;

        // span==1만 후보
        var candidates = new System.Collections.Generic.List<GameObject>();
        foreach (var p in movePrefabs)
        {
            if (p == null) continue;
            var m = p.GetComponent<ObstacleMarker>();
            int span = (m != null) ? m.laneSpan : 1;
            if (span == 1) candidates.Add(p);
        }
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    void Spawn(GameObject prefab, int lane)
    {
        if (prefab == null) return;

        var marker = prefab.GetComponent<ObstacleMarker>();
        int span = (marker != null) ? Mathf.Clamp(marker.laneSpan, 1, 3) : 1;

        // span에 따른 lane/x 결정
        float x;
        if (span == 3)
        {
            lane = 0;
            x = 0f;
        }
        else if (span == 2)
        {
            // (-1,0) 또는 (0,1) 페어만 가능
            lane = (Random.value < 0.5f) ? -1 : 0;
            float centerLane = (lane == -1) ? -0.5f : 0.5f;
            x = centerLane * laneWidth;
        }
        else
        {
            // span == 1
            x = lane * laneWidth;
        }

        float z = player.position.z + spawnZOffset;

        // 타입 기준으로 Y 결정
        float y;
        if (marker != null && marker.type == ObstacleType.Jump) y = deskWorldY;
        else if (marker != null && marker.type == ObstacleType.Roll) y = bannerWorldY;
        else y = lockerWorldY;

        Vector3 pos = new Vector3(x, y, z);
        GameObject go = Instantiate(prefab, pos, prefab.transform.rotation);
        go.name = $"{prefab.name}_span{span}_lane{lane}_{(int)t}s";
    }

    void CleanupOldObstacles()
    {
        float destroyZ = player.position.z - cleanupBehindZ;

        // "Obstacle" 태그에 의존하지 않고, 씬 전체에서 ObstacleMarker 가진 것만 정리
        var markers = GameObject.FindObjectsOfType<ObstacleMarker>();
        foreach (var m in markers)
        {
            if (m != null && m.transform.position.z < destroyZ)
                Destroy(m.gameObject);
        }
    }

}