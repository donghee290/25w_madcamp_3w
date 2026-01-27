using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public PlayerMotor playerMotor;
    public GameManager gameManager;
    public SpawnsRoot spawnsRoot;

    [Header("Prefabs (set in Inspector)")]
    public GameObject[] jumpPrefabs;
    public GameObject[] rollPrefabs;
    public GameObject[] movePrefabs;

    [Header("Spawn Space")]
    public float spawnZOffset = 35f;
    public float laneWidth = 1.2f;

    [Header("World Y per obstacle (IMPORTANT)")]
    public float deskWorldY = 0.5f;
    public float lockerWorldY = 1.1f;
    public float bannerWorldY = 2.2f;

    [Header("Difficulty Timing")]
    public float stage1End = 20f;
    public float stage2End = 40f;

    [Header("Spawn Interval (easier = bigger numbers)")]
    public float intervalStage1 = 2.0f;
    public float intervalStage2 = 1.6f;
    public float intervalStage3 = 1.2f;

    [Header("Difficulty Options")]
    [Range(0f, 1f)] public float spawnChanceStage1 = 0.85f;
    [Range(0f, 1f)] public float spawnChanceStage2 = 0.80f;
    [Range(0f, 1f)] public float spawnChanceStage3 = 0.75f;

    public bool singleOnly = true;

    [Header("Gap (NEW)")]
    [Tooltip("장애물끼리 최소 Z 간격. 값이 클수록 더 듬성듬성 나와서 쉬워짐")]
    public float minObstacleGapZ = 10.0f;

    [Header("Overlap Avoidance")]
    public float zBlockRange = 2.0f;
    public float laneBlockHalfWidth = 0.55f;
    public int maxSpawnTries = 10;

    float t = 0f;
    float nextSpawnAt = 0f;

    // NEW: 마지막 스폰 Z
    float _lastSpawnZ = float.NegativeInfinity;

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
            TrySpawnByStage();
            nextSpawnAt = t + CurrentInterval();
        }
    }

    float CurrentInterval()
    {
        if (t < stage1End) return intervalStage1;
        if (t < stage2End) return intervalStage2;
        return intervalStage3;
    }

    float CurrentSpawnChance()
    {
        if (t < stage1End) return spawnChanceStage1;
        if (t < stage2End) return spawnChanceStage2;
        return spawnChanceStage3;
    }

    void TrySpawnByStage()
    {
        if (Random.value > CurrentSpawnChance())
            return;

        // NEW: 최소 간격 체크 (이번 타이밍 스폰 자체를 스킵)
        float plannedZ = player.position.z + spawnZOffset;
        if (plannedZ - _lastSpawnZ < minObstacleGapZ)
            return;

        SpawnOneSafe();
    }

    void SpawnOneSafe()
    {
        float z = player.position.z + spawnZOffset;

        for (int attempt = 0; attempt < maxSpawnTries; attempt++)
        {
            int lane = RandomLane();
            GameObject prefab = RandomObstaclePrefab();
            if (prefab == null) return;

            var marker = prefab.GetComponent<ObstacleMarker>();
            int span = (marker != null) ? Mathf.Clamp(marker.laneSpan, 1, 3) : 1;

            int laneA, laneB;
            float x = LaneToX(span, lane, out laneA, out laneB);

            float y;
            if (marker != null && marker.type == ObstacleType.Jump) y = deskWorldY;
            else if (marker != null && marker.type == ObstacleType.Roll) y = bannerWorldY;
            else y = lockerWorldY;

            Vector3 pos = new Vector3(x, y, z);

            if (IsBlockedByExisting(z, laneA, laneB))
                continue;

            GameObject go = spawnsRoot.SpawnObstacle(prefab, pos, prefab.transform.rotation);
            if (go == null) return;

            go.name = $"{prefab.name}_span{span}_lane{laneA}to{laneB}_{(int)t}s";

            // NEW: 스폰 성공시에만 마지막 스폰 Z 갱신
            _lastSpawnZ = z;
            return;
        }
    }

    bool IsBlockedByExisting(float z, int laneA, int laneB)
    {
        float minX = LaneIndexToX(laneA) - laneBlockHalfWidth;
        float maxX = LaneIndexToX(laneB) + laneBlockHalfWidth;

        if (spawnsRoot.obstaclesParent != null)
        {
            for (int i = 0; i < spawnsRoot.obstaclesParent.childCount; i++)
            {
                var tr = spawnsRoot.obstaclesParent.GetChild(i);
                if (tr == null) continue;

                if (Mathf.Abs(tr.position.z - z) > zBlockRange) continue;

                float x = tr.position.x;
                if (x >= minX && x <= maxX) return true;
            }
        }

        if (spawnsRoot.itemsParent != null)
        {
            for (int i = 0; i < spawnsRoot.itemsParent.childCount; i++)
            {
                var tr = spawnsRoot.itemsParent.GetChild(i);
                if (tr == null) continue;

                if (Mathf.Abs(tr.position.z - z) > zBlockRange) continue;

                float x = tr.position.x;
                if (x >= minX && x <= maxX) return true;
            }
        }

        return false;
    }

    float LaneToX(int span, int lane, out int laneA, out int laneB)
    {
        if (span == 3)
        {
            laneA = -1; laneB = 1;
            return 0f;
        }
        if (span == 2)
        {
            int start = (Random.value < 0.5f) ? -1 : 0;
            laneA = start;
            laneB = start + 1;

            float centerLane = (laneA == -1) ? -0.5f : 0.5f;
            return centerLane * laneWidth;
        }

        laneA = lane;
        laneB = lane;
        return lane * laneWidth;
    }

    float LaneIndexToX(int laneIdx) => laneIdx * laneWidth;

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
}