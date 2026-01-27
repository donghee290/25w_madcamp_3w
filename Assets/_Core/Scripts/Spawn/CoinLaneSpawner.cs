using UnityEngine;

public class CoinLaneSpawner : MonoBehaviour
{
    [Header("References")]
    public Transform player;            // PlayerRoot (또는 Player)
    public GameObject coinPrefab;       // coin 프리팹

    [Header("Lane settings")]
    public float laneWidth = 1.2f;      // PlayerMotor의 laneWidth랑 동일하게
    public int laneCount = 3;           // 3레인
    public float laneCenterX = 0f;      // 트랙 중심 X (보통 0)

    [Header("Spawn along Z")]
    public float startAhead = 25f;      // 플레이어 앞 어디부터 깔지
    public float slotSpacing = 8f;      // "장애물 간격" = 스폰 슬롯 간격
    public int coinsPerSlot = 5;        // 한 슬롯에서 몇 개 깔지
    public float coinSpacing = 1.5f;    // 슬롯 안에서 코인 간격

    [Header("Rule")]
    public bool avoidSameLaneTwice = true; // 다음 슬롯은 다른 레인

    private float nextSlotZ;
    private int currentLane = 1;

    void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // 첫 슬롯 위치 초기화
        nextSlotZ = (player != null ? player.position.z : 0f) + startAhead;

        // 첫 레인 랜덤(원하면 고정도 가능)
        currentLane = Random.Range(0, laneCount);
    }

    void Update()
    {
        if (player == null || coinPrefab == null) return;

        // 플레이어가 앞으로 가면, 슬롯 단위로 계속 생성
        float spawnUntilZ = player.position.z + startAhead;

        while (nextSlotZ <= spawnUntilZ)
        {
            SpawnSlotCoins(nextSlotZ, currentLane);

            // 다음 슬롯으로 갈 때 레인 변경(규칙)
            int nextLane = Random.Range(0, laneCount);
            if (avoidSameLaneTwice && laneCount > 1)
            {
                while (nextLane == currentLane)
                    nextLane = Random.Range(0, laneCount);
            }
            currentLane = nextLane;

            nextSlotZ += slotSpacing;
        }
    }

    void SpawnSlotCoins(float slotZ, int lane)
    {
        float laneX = LaneToX(lane);

        // 슬롯 안에서 연속 코인 생성
        for (int i = 0; i < coinsPerSlot; i++)
        {
            float z = slotZ + i * coinSpacing;
            Vector3 pos = new Vector3(laneX, 0.6f, z); // y는 코인 높이에 맞춰 조절
            Instantiate(coinPrefab, pos, Quaternion.identity);
        }
    }

    float LaneToX(int lane)
    {
        // lane: 0,1,2 -> x: -laneWidth, 0, +laneWidth (중앙이 1일 때)
        int mid = laneCount / 2;
        return laneCenterX + (lane - mid) * laneWidth;
    }
}
