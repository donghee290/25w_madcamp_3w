using UnityEngine;

public class PoseInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }            // -1,0,1
    public bool JumpTriggered { get; private set; }  // 1프레임 트리거
    public bool RollHeld { get; private set; }       // 누르고 있는 상태
    public float MoveLevel { get; private set; }     // 0~1

    [Header("Lane (shoulder center X)")]
    [Tooltip("중앙 데드존. 이 안이면 Lane=0")]
    public float laneDeadZone = 0.08f;
    [Tooltip("이 정도 벗어나면 Lane=-1/1 확정")]
    public float laneStrongThreshold = 0.22f;

    [Header("Jump (hands up)")]
    [Tooltip("양 손목이 어깨보다 위면 점프")]
    public float handsUpMargin = 0.03f;
    [Tooltip("연속 점프 방지 쿨다운(초)")]
    public float jumpCooldown = 0.6f;

    [Header("Roll (squat)")]
    [Tooltip("엉덩이(hip) 기준보다 이만큼 내려가면 롤")]
    public float squatThreshold = 0.10f;

    [Header("Run/Stop")]
    [Tooltip("포즈가 잡히면 기본 달리기(1). 원하면 제스처로 stop 추가 가능.")]
    public bool autoRun = true;

    // ---- 내부 상태 ----
    [SerializeField] private bool hasLandmarksDebug;
    [SerializeField] private float lastLandmarkTime;

    private readonly Vector3[] _lm = new Vector3[33];   // 최신 랜드마크
    private bool _hasLm = false;

    private float _jumpCd = 0f;
    private float _baseHipY = -1f;

    /// <summary>
    /// Bridge에서 매 프레임(또는 결과 수신 시) 호출해서 33개 랜드마크를 넣어준다.
    /// x,y는 0~1 normalized. y는 위가 0, 아래가 1.
    /// </summary>
    public void SetLandmarks(Vector3[] src)
    {
        if (src == null || src.Length < 33) return;
        for (int i = 0; i < 33; i++) _lm[i] = src[i];
        _hasLm = true;
        _hasLm = true;
        hasLandmarksDebug = true;
        lastLandmarkTime = Time.time;

    }

    void Update()
    {
        // JumpTriggered는 1프레임만 true여야 함
        JumpTriggered = false;

        // 1초 이상 랜드마크 업데이트가 없으면 끊긴 걸로 보고 stop
        if (_hasLm && Time.time - lastLandmarkTime > 1.0f)
        {
            _hasLm = false;
            hasLandmarksDebug = false;
            _baseHipY = -1f;   // 다시 잡힐 때 기준 재설정
        }


        if (_jumpCd > 0f) _jumpCd -= Time.deltaTime;

        // 랜드마크가 아직 안 들어오면 "정지" 상태 유지
        if (!_hasLm)
        {
            Lane = 0;
            RollHeld = false;
            MoveLevel = 0f;
            return;
        }

        // 기본 달리기
        MoveLevel = autoRun ? 1f : 0f;

        // ---- 랜드마크 인덱스 ----
        // 11/12: 어깨, 15/16: 손목, 23/24: 힙
        Vector3 lSh = _lm[11];
        Vector3 rSh = _lm[12];
        Vector3 lWr = _lm[15];
        Vector3 rWr = _lm[16];
        Vector3 lHip = _lm[23];
        Vector3 rHip = _lm[24];

        // 1) Lane: 어깨 중심의 x가 화면 중앙(0.5)에서 얼마나 벗어났는지
        float centerX = (lSh.x + rSh.x) * 0.5f; // 0~1
        float dx = centerX - 0.5f;

        if (dx < -laneStrongThreshold) Lane = -1;
        else if (dx > laneStrongThreshold) Lane = 1;
        else if (Mathf.Abs(dx) < laneDeadZone) Lane = 0;
        // deadzone~strong 사이에서는 Lane 유지하고 싶으면 else 생략해도 됨

        // 2) Jump: 양 손목이 어깨보다 "위"(y 더 작음)
        bool handsUp =
            (lWr.y < lSh.y - handsUpMargin) &&
            (rWr.y < rSh.y - handsUpMargin);

        if (handsUp && _jumpCd <= 0f)
        {
            JumpTriggered = true;
            _jumpCd = jumpCooldown;
        }

        // 3) Roll: 힙이 기준보다 내려가면(squat)
        float hipY = (lHip.y + rHip.y) * 0.5f;
        if (_baseHipY < 0f) _baseHipY = hipY; // 첫 프레임 기준 저장

        RollHeld = (hipY - _baseHipY) > squatThreshold;
    }
}
