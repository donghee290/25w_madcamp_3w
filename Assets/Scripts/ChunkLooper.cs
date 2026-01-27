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

    [Header("Progress")]
    [Tooltip("진행 기준(보통 Player). 이 Transform의 전진량(delta)로 무한생성 판단")]
    public Transform progressRoot;

    [Tooltip("progressRoot가 +Z로 전진하면 체크 해제, -Z로 전진/월드가 -Z로 흐르면 체크")]
    public bool progressIsNegativeZ = false;

    [Header("Runtime spawning")]
    public int chunksAhead = 12;
    public int chunksBehind = 3;

    [Header("Chunk prefabs (NO FLOOR/CEILING inside)")]
    public GameObject corridorChunkBase;
    public GameObject corridorChunkRandom;

    [Header("Floor prefab")]
    public GameObject floorPrefab;

    [Header("Ceiling prefab")]
    public GameObject ceilingPrefab;

    [Tooltip("Ceiling을 올리는 높이 (spawnPoint 기준)")]
    public float ceilingYOffset = 4.5f;

    [Tooltip("Ceiling을 진행방향으로 살짝 밀기(틈 방지). 보통 0~0.05")]
    public float ceilingForwardOffset = 0.02f;

    [Tooltip("Ceiling 프리팹 회전을 강제로 X=180 뒤집기(Plane 한쪽면 이슈 대응). 복합 프리팹이면 비주얼 자식만 뒤집습니다.")]
    public bool forceCeilingFlipX180 = true;

    [Header("Ceiling prefab (composite-safe)")]
    [Tooltip("Ceiling 프리팹 루트 아래에서 '비주얼'로 취급할 자식 이름. 비워두면 첫 Renderer를 자동 선택")]
    public string ceilingVisualChildName = "Ceiling_Visual";

    [Tooltip("플레이 시작 시 ceilingsParent 아래에 이미 존재하는 천장을 전부 제거(이전 실행 잔재/다른 곳에서 박힌 오브젝트 제거)")]
    public bool purgeExistingCeilingsOnStart = true;

    [Header("BackWall (opaque wall)")]
    public GameObject backWallPrefab;
    public bool spawnBackWall = true;

    [Tooltip("BackWall을 진행방향으로 살짝 당기거나 밀기(틈 방지). 보통 -0.03 정도")]
    public float backWallForwardOffset = -0.03f;

    [Header("Weights (higher = more frequent)")]
    public int wBase = 4;
    public int wRandom = 3;

    [Header("Decoration Props (placed on DecorationsSlot*)")]
    public GameObject[] lockerPrefabs;
    public GameObject[] singlePropPrefabs;

    [Range(0f, 1f)]
    public float propsChancePerSide = 0.85f;

    [Range(0f, 1f)]
    public float lockerGroupChancePerSide = 0.55f;

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

    [Header("Chunk size")]
    [Tooltip("청크 길이(진행방향). 실제 생성 간격. 반드시 실제 청크 1개 길이에 맞추세요.")]
    public float chunkLengthZ = 5.336f;

    [Header("Debug")]
    public bool debugLog = false;

    private float _nextZ;                 // 월드(진행축) 기준 다음 생성 위치
    private float _progressZ0;            // progressRoot 시작값(델타 기준)
    private readonly List<Spawned> _spawned = new();

    // ceilingsParent의 "시작 시점 자식 수" (NULL인데 천장이 생기는 케이스를 강제 제거할 때 기준)
    private int _ceilingChildCount0;

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

        // 기존 천장 잔재 제거 (다른 곳에서 박힌 오브젝트/이전 실행 잔재가 ceilingsParent 아래에 남아있는 경우)
        if (purgeExistingCeilingsOnStart && ceilingsParent != null)
        {
            for (int i = ceilingsParent.childCount - 1; i >= 0; i--)
                Destroy(ceilingsParent.GetChild(i).gameObject);
        }
        _ceilingChildCount0 = ceilingsParent != null ? ceilingsParent.childCount : 0;

        if (progressRoot == null)
        {
            // Player 태그가 있으면 자동 연결
            var p = GameObject.FindWithTag("Player");
            if (p != null) progressRoot = p.transform;
        }

        _progressZ0 = GetRawProgressZ();
        _nextZ = GetSpawnBaseZ(); // spawnPoint 기준 시작

        // 안전구간: 처음 3개는 Base로 고정
        for (int i = 0; i < 3; i++) SpawnOne(forceBase: true);

        FillAhead(force: true);
    }

    void Update()
    {
        FillAhead(force: false);
        CleanupBehind();

        // ceilingPrefab이 NULL인데도 ceilingsParent 아래에 천장이 늘어나는 경우:
        // ChunkLooper 외부에서 생성/주입되고 있는 것이므로, 게임플레이에 남지 못하도록 강제 제거.
        if (ceilingPrefab == null && ceilingsParent != null)
        {
            if (ceilingsParent.childCount > _ceilingChildCount0)
            {
                for (int i = ceilingsParent.childCount - 1; i >= _ceilingChildCount0; i--)
                    Destroy(ceilingsParent.GetChild(i).gameObject);

                if (debugLog)
                    Debug.LogWarning("[Ceiling] Detected ceilings while ceilingPrefab is NULL -> purged.");
            }
        }
    }

    float GetSpawnBaseZ()
    {
        // 진행축을 spawnPoint.forward로 통일
        // 하지만 Z값만 쓰는 로직이므로, 현재는 월드 z 축을 "진행 스칼라"로 사용
        // 씬이 회전되어도 안전하게 하려면 '스칼라 진행값'만 사용하도록 구성
        return spawnPoint.position.z;
    }

    float GetRawProgressZ()
    {
        if (progressRoot == null) return 0f;
        return progressRoot.position.z;
    }

    // 시작 대비 진행량(델타). 이 값이 커질수록 앞으로 간 것.
    float GetProgressDelta()
    {
        float raw = GetRawProgressZ();
        float delta = raw - _progressZ0;
        return progressIsNegativeZ ? -delta : delta;
    }

    void FillAhead(bool force)
    {
        float pz = GetProgressDelta();

        // targetZ는 "spawnPoint 기준 시작 + 진행량 + 앞쪽 확보"
        float targetZ = GetSpawnBaseZ() + pz + chunksAhead * chunkLengthZ;

        while (_nextZ < targetZ)
        {
            SpawnOne(forceBase: false);
            if (!force && _spawned.Count > 400) break;
        }
    }

    void CleanupBehind()
    {
        float pz = GetProgressDelta();
        float killZ = GetSpawnBaseZ() + pz - (chunkLengthZ * chunksBehind);

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

        // 모든 배치는 spawnPoint 기준으로, 진행축은 월드 Z로 쓴다.
        var basePos = new Vector3(spawnPoint.position.x, spawnPoint.position.y, z);

        // 청크
        var chunkGo = Instantiate(prefab, basePos, prefab.transform.rotation, chunksParent);

        // 바닥
        GameObject floorGo = null;
        if (floorPrefab != null)
        {
            floorGo = Instantiate(floorPrefab, basePos, floorPrefab.transform.rotation, floorsParent);
        }

        // 천장
        GameObject ceilingGo = null;

        if (ceilingPrefab != null)
        {
            // forward 오프셋을 '월드 forward'가 아니라 'spawnPoint forward'로 적용
            Vector3 fwd = spawnPoint.forward.normalized;
            if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.forward;

            var cPos = basePos + Vector3.up * ceilingYOffset + fwd * ceilingForwardOffset;

            // 복합 프리팹 안전: 루트는 프리팹 회전 그대로 두고, 비주얼(Plane)만 뒤집는다.
            ceilingGo = Instantiate(ceilingPrefab, cPos, ceilingPrefab.transform.rotation, ceilingsParent);

            Transform vis = null;

            // 1) 이름으로 찾기
            if (!string.IsNullOrEmpty(ceilingVisualChildName))
            {
                var t = ceilingGo.transform.Find(ceilingVisualChildName);
                if (t != null) vis = t;
            }

            // 2) 없으면 첫 Renderer 가진 트랜스폼
            if (vis == null)
            {
                var r = ceilingGo.GetComponentInChildren<Renderer>(true);
                if (r != null) vis = r.transform;
            }

            // 비주얼만 뒤집기
            if (forceCeilingFlipX180 && vis != null)
            {
                vis.localRotation = Quaternion.Euler(180f, 0f, 0f) * vis.localRotation;
            }

            if (debugLog)
            {
                var anyCollider = ceilingGo.GetComponentInChildren<Collider>(true);
                if (anyCollider == null)
                    Debug.LogWarning($"[Ceiling] collider missing in instantiated prefab: {ceilingGo.name}");
            }
        }
        else
        {
            // ceilingPrefab이 NULL인데 ceilingsParent에 천장이 생긴다면 ChunkLooper 외부 생성.
            // SpawnOne 호출 타이밍에 발견되면 즉시 제거.
            if (ceilingsParent != null && ceilingsParent.childCount > _ceilingChildCount0)
            {
                for (int i = ceilingsParent.childCount - 1; i >= _ceilingChildCount0; i--)
                    Destroy(ceilingsParent.GetChild(i).gameObject);

                if (debugLog)
                    Debug.LogWarning("[Ceiling] ceilingPrefab is NULL but ceilings existed -> purged newly appeared ceilings.");
            }
        }

        // 백월
        GameObject backGo = null;
        if (spawnBackWall && backWallPrefab != null)
        {
            Vector3 fwd = spawnPoint.forward.normalized;
            if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.forward;

            var bPos = basePos + fwd * backWallForwardOffset;
            backGo = Instantiate(backWallPrefab, bPos, backWallPrefab.transform.rotation, backdropsParent);
        }

        // 소품
        PlaceWallProps(chunkGo);

        _spawned.Add(new Spawned
        {
            zStart = z,
            chunk = chunkGo,
            floor = floorGo,
            ceiling = ceilingGo,
            backWall = backGo
        });

        _nextZ += (chunkLengthZ - seamEps);

        if (debugLog)
            Debug.Log($"[SpawnOne] zStart={z:F2} nextZ={_nextZ:F2} prefab={prefab.name} ceiling={(ceilingGo ? "Y" : "N")}");
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
    // Props placement (DecorationsSlot* prefix)
    // -------------------------

    void PlaceWallProps(GameObject chunk)
    {
        if (Time.time < PlayerMotor.SafeUntilTime)
            return;

        if (chunk == null) return;

        var slots = new List<Transform>(16);
        CollectSlotsByPrefix(chunk.transform, "DecorationsSlot", slots);

        if (slots.Count == 0) return;

        foreach (var slot in slots)
        {
            if (slot == null) continue;
            if (UnityEngine.Random.value > propsChancePerSide) continue;

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

            var pos = ApplyPrefabOffsets(slot, prefab, basePos, off);
            Instantiate(prefab, pos, slot.rotation, slot);

            placed++;
        }

        return placed;
    }

    static void CollectSlotsByPrefix(Transform root, string prefix, List<Transform> outList)
    {
        if (root == null) return;

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