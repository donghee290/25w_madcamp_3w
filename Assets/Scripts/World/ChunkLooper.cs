using System;
using System.Collections.Generic;
using UnityEngine;

public class ChunkLooper : MonoBehaviour
{
    [Header("Scene references")]
    public Transform chunksParent;
    public Transform floorsParent;
    public Transform ceilingsParent;
    public Transform backdropsParent;
    public Transform spawnPoint;

    [Header("Runtime spawning")]
    public Transform player;
    public int chunksAhead = 12;
    public int chunksBehind = 3;

    [Header("Chunk prefabs (NO FLOOR/CEILING inside)")]
    public GameObject corridorChunkBase;
    public GameObject corridorChunkRandom; // Sub는 제외

    [Header("Floor/Ceiling prefab")]
    public GameObject floorPrefab;
    public GameObject ceilingPrefab;

    [Tooltip("Ceiling 높이 보정(월드 Y). 흰 벽 아래로 숨기고 싶으면 이 값으로 조절")]
    public float ceilingYOffset = 2.8f;

    [Tooltip("Ceiling을 청크 안쪽으로 살짝 당김(+Z). backWall과 겹치거나 가려지면 0.01~0.03 추천")]
    public float ceilingZOffset = 0.02f;

    [Header("BackWall (opaque wall)")]
    public GameObject backWallPrefab;
    public bool spawnBackWall = true;

    [Tooltip("BackWall을 뒤로 살짝 밀기(-Z). 천장/벽 가림 이슈 있으면 -0.02~-0.05 추천")]
    public float backWallZOffset = -0.03f;

    [Header("Weights (higher = more frequent)")]
    public int wBase = 4;
    public int wRandom = 3;

    [Header("Decoration Props (placed on DecorationsSlot*)")]
    public GameObject[] lockerPrefabs;
    public GameObject[] singlePropPrefabs;

    [Range(0f, 1f)]
    public float propsChancePerSide = 0.85f;

    [Range(0f, 1f)]
    public float lockerGroupChancePerSide = 0.55f; // 락커 줄이기(기본값만)

    public int singlePropsMin = 0;
    public int singlePropsMax = 2;

    public int lockerRunMin = 2;
    public int lockerRunMax = 6;

    [Tooltip("사물함 연속 배치 간격 (slot.right 방향)")]
    public float lockerStepZ = 0.6f;

    [Header("Tweak")]
    [Tooltip("청크 경계 미세 오차(틈) 방지용. 0.0~0.02 사이")]
    public float seamEps = 0.01f;

    [Tooltip("벽 밖으로 살짝 밀어내기 (slot.forward 방향)")]
    public float wallPushEps = 0.002f;

    [Tooltip("소품 Y 위치 오프셋 (slot 기준)")]
    public float propYOffset = 0f;

    [Tooltip("싱글 소품 겹침 방지용 (slot.right 기준) 반폭")]
    public float singlePropHalfWidth = 0.45f;

    [Tooltip("락커 런 구간 마진(싱글과 겹침 방지)")]
    public float lockerRunMargin = 0.3f;

    [Header("Debug")]
    public bool debugLog = false;

    [Header("Chunk size")]
    [Tooltip("청크 길이(Z). 실제 생성 간격. 반드시 실제 청크 1개 길이에 맞추세요.")]
    public float chunkLengthZ = 5.336f;

    private float _nextZ;
    private readonly List<Spawned> _spawned = new();

    [Serializable]
    private class Spawned
    {
        public float zStart;
        public GameObject chunk;
        public GameObject floor;
        public GameObject ceiling;
        public GameObject backWall;
    }

    void Start()
    {
        if (chunksParent == null) chunksParent = transform;
        if (floorsParent == null) floorsParent = chunksParent;
        if (ceilingsParent == null) ceilingsParent = chunksParent;
        if (backdropsParent == null) backdropsParent = chunksParent;
        if (spawnPoint == null) spawnPoint = transform;
        if (player == null) player = GameObject.FindWithTag("Player")?.transform;

        _nextZ = spawnPoint.position.z;

        // 안전구간: 처음 3개는 Base로 고정
        for (int i = 0; i < 3; i++) SpawnOne(forceBase: true);

        FillAhead(force: true);
    }

    void Update()
    {
        if (player == null) return;

        FillAhead(force: false);
        CleanupBehind();
    }

    void FillAhead(bool force)
    {
        float pz = player.position.z;
        float targetZ = pz + chunksAhead * chunkLengthZ;

        while (_nextZ < targetZ)
        {
            SpawnOne(forceBase: false);
            if (!force && _spawned.Count > 400) break; // 안전장치
        }
    }

    void CleanupBehind()
    {
        float pz = player.position.z;
        float killZ = pz - (chunkLengthZ * chunksBehind);

        int i = 0;
        while (i < _spawned.Count)
        {
            if (_spawned[i].zStart < killZ)
            {
                if (_spawned[i].chunk) Destroy(_spawned[i].chunk);
                if (_spawned[i].floor) Destroy(_spawned[i].floor);
                if (_spawned[i].ceiling) Destroy(_spawned[i].ceiling);
                if (_spawned[i].backWall) Destroy(_spawned[i].backWall);
                _spawned.RemoveAt(i);
                continue;
            }
            i++;
        }
    }

    void SpawnOne(bool forceBase)
    {
        var prefab = forceBase ? corridorChunkBase : PickChunkPrefab();
        if (prefab == null) return;

        float z = _nextZ;

        // 청크는 spawnPoint 기준
        var chunkPos = new Vector3(spawnPoint.position.x, spawnPoint.position.y, z);
        var chunkGo = Instantiate(prefab, chunkPos, prefab.transform.rotation, chunksParent);

        // 바닥: 청크와 같은 zStart (프리팹 스케일로 길이 맞추는 방식 유지)
        GameObject floorGo = null;
        if (floorPrefab != null)
        {
            floorGo = Instantiate(floorPrefab, chunkPos, floorPrefab.transform.rotation, floorsParent);
        }

        // 천장: y/z 보정 (backWall이 불투명이라, 천장은 살짝 “청크 안쪽”으로 넣는 게 안전)
        GameObject ceilGo = null;
        if (ceilingPrefab != null)
        {
            var ceilPos = new Vector3(chunkPos.x, ceilingYOffset, chunkPos.z + ceilingZOffset);
            ceilGo = Instantiate(ceilingPrefab, ceilPos, ceilingPrefab.transform.rotation, ceilingsParent);
        }

        // backWall: 불투명 진짜 벽. 천장 가림/겹침 방지 위해 -Z로 살짝 뒤로
        GameObject backGo = null;
        if (spawnBackWall && backWallPrefab != null)
        {
            var backPos = new Vector3(chunkPos.x, chunkPos.y, chunkPos.z + backWallZOffset);
            backGo = Instantiate(backWallPrefab, backPos, backWallPrefab.transform.rotation, backdropsParent);
        }

        // 벽 소품 배치
        PlaceWallProps(chunkGo);

        _spawned.Add(new Spawned
        {
            zStart = z,
            chunk = chunkGo,
            floor = floorGo,
            ceiling = ceilGo,
            backWall = backGo
        });

        _nextZ += (chunkLengthZ - seamEps);
    }

    GameObject PickChunkPrefab()
    {
        int a = Mathf.Max(0, wBase);
        int b = Mathf.Max(0, wRandom);

        int total = a + b;
        if (total <= 0) total = 1;

        int r = UnityEngine.Random.Range(0, total);
        if (r < a) return corridorChunkBase;
        return corridorChunkRandom != null ? corridorChunkRandom : corridorChunkBase;
    }

    // -------------------------
    // Props placement (DecorationsSlot 기반: "이름 프리픽스"로만)
    // -------------------------

    void PlaceWallProps(GameObject chunk)
    {
        if (chunk == null) return;

        // DecorationsSlot_L / DecorationsSlot_R 뿐 아니라 DecorationsSlot 로 시작하는 모든 슬롯 수집
        var slots = new List<Transform>(16);
        CollectSlotsByPrefix(chunk.transform, "DecorationsSlot", slots);

        if (debugLog)
            Debug.Log($"[Props] chunk={chunk.name} slotsFound={slots.Count}");

        if (slots.Count == 0) return;

        foreach (var slot in slots)
        {
            if (slot == null) continue;

            if (UnityEngine.Random.value > propsChancePerSide)
                continue;

            // 슬롯 “한쪽 벽” 단위로 겹침 방지 관리
            var usedRanges = new List<Vector2>(8);

            bool canLocker = lockerPrefabs != null && lockerPrefabs.Length > 0;
            bool canSingle = singlePropPrefabs != null && singlePropPrefabs.Length > 0;
            if (!canLocker && !canSingle) continue;

            bool doLockerRun = canLocker && (!canSingle || UnityEngine.Random.value < lockerGroupChancePerSide);

            int spawned = 0;
            if (doLockerRun) spawned += SpawnLockerRun(slot, usedRanges);
            else spawned += SpawnSingles(slot, usedRanges);

            if (debugLog)
                Debug.Log($"[Props] slot={slot.name} spawned={spawned}");
        }
    }

    static bool Overlaps(List<Vector2> used, float aMin, float aMax)
    {
        for (int i = 0; i < used.Count; i++)
        {
            var b = used[i];
            if (aMin <= b.y && aMax >= b.x) return true;
        }
        return false;
    }

    static void AddRange(List<Vector2> used, float min, float max)
    {
        used.Add(new Vector2(min, max));
    }

    Vector3 BaseAttachPos(Transform slot)
    {
        return slot.position + slot.forward * wallPushEps + Vector3.up * propYOffset;
    }

    int SpawnLockerRun(Transform slot, List<Vector2> usedRanges)
    {
        if (lockerPrefabs == null || lockerPrefabs.Length == 0 || slot == null) return 0;

        int min = Mathf.Max(2, lockerRunMin);
        int max = Mathf.Max(min, lockerRunMax);
        int count = UnityEngine.Random.Range(min, max + 1);

        Vector3 basePos = BaseAttachPos(slot);

        float startOffset = UnityEngine.Random.Range(-0.8f, 0.8f);
        float runMin = startOffset;
        float runMax = startOffset + (count - 1) * lockerStepZ;

        float margin = Mathf.Max(0f, lockerRunMargin);

        int tries = 12;
        while (tries-- > 0 && Overlaps(usedRanges, runMin - margin, runMax + margin))
        {
            startOffset = UnityEngine.Random.Range(-0.8f, 0.8f);
            runMin = startOffset;
            runMax = startOffset + (count - 1) * lockerStepZ;
        }

        if (Overlaps(usedRanges, runMin - margin, runMax + margin))
            return 0;

        AddRange(usedRanges, runMin - margin, runMax + margin);

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            var prefab = lockerPrefabs[UnityEngine.Random.Range(0, lockerPrefabs.Length)];
            if (prefab == null) continue;

            float off = startOffset + (i * lockerStepZ);

            // PropOffset 반영 + slot 기준 부착
            var pos = ApplyPrefabOffsets(slot, prefab, basePos, off);
            Instantiate(prefab, pos, slot.rotation, slot);

            spawned++;
        }

        return spawned;
    }

    int SpawnSingles(Transform slot, List<Vector2> usedRanges)
    {
        if (singlePropPrefabs == null || singlePropPrefabs.Length == 0 || slot == null) return 0;

        int min = Mathf.Max(0, singlePropsMin);
        int max = Mathf.Max(min, singlePropsMax);

        int count = UnityEngine.Random.Range(min, max + 1);
        if (count <= 0) return 0;

        Vector3 basePos = BaseAttachPos(slot);
        float halfW = Mathf.Max(0.05f, singlePropHalfWidth);

        int placed = 0;
        int safety = 60;

        while (placed < count && safety-- > 0)
        {
            var prefab = singlePropPrefabs[UnityEngine.Random.Range(0, singlePropPrefabs.Length)];
            if (prefab == null) continue;

            float off = UnityEngine.Random.Range(-1.8f, 1.8f);
            float aMin = off - halfW;
            float aMax = off + halfW;

            if (Overlaps(usedRanges, aMin, aMax))
                continue;

            AddRange(usedRanges, aMin, aMax);

            // 여기서도 PropOffset 반영(동바가 말한 “개별 프리팹 y값”이 씹히는 원인 제거)
            var pos = ApplyPrefabOffsets(slot, prefab, basePos, off);
            Instantiate(prefab, pos, slot.rotation, slot);

            placed++;
        }

        return placed;
    }

    static void CollectSlotsByPrefix(Transform root, string prefix, List<Transform> outList)
    {
        if (root == null) return;

        // DecorationsSlot, DecorationsSlot_L, DecorationsSlot_R, DecorationsSlot_L (1) 전부 포함
        if (root.name.StartsWith(prefix, StringComparison.Ordinal))
            outList.Add(root);

        for (int i = 0; i < root.childCount; i++)
            CollectSlotsByPrefix(root.GetChild(i), prefix, outList);
    }

    Vector3 ApplyPrefabOffsets(Transform slot, GameObject prefab, Vector3 basePos, float along)
    {
        var off = prefab.GetComponent<PropOffset>();
        if (off == null) return basePos + slot.right * along;

        return basePos
            + slot.right * (along + off.alongOffset)
            + Vector3.up * off.yOffset
            + slot.forward * off.zOffset;
    }
}