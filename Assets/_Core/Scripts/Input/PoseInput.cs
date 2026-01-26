using UnityEngine;

public class PoseInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }            // -1,0,1
    public bool JumpTriggered { get; private set; }  // 트리거(짧게 유지)
    public bool RollHeld { get; private set; }       // 트리거(짧게 유지)
    public float MoveLevel { get; private set; }     // 0=STOP, ~0.5=WALK, 1=RUN

    /* ================= LANE (Left/Right) ================= */
    [Header("Lane (Body Left/Right)")]
    [Tooltip("어깨 중심 X가 중앙(0.5)에서 이 안이면 Lane=0")]
    public float laneDeadZone = 0.06f;

    [Tooltip("이 이상 벗어나면 Lane=-1/1 확정")]
    public float laneStrongThreshold = 0.16f;

    [Tooltip("Lane이 흔들리지 않게 확정까지 유지할 최소 시간(초)")]
    public float laneHoldSeconds = 0.10f;

    [Tooltip("카메라가 거울처럼 보이면(좌우 반전) true")]
    public bool mirrorX = false;

    /* ================= JUMP (Hands Up) ================= */
    [Header("Jump (Hands Up)")]
    public float handsUpMargin = 0.03f;
    public float jumpCooldown = 0.6f;
    public int handsUpFramesRequired = 2;
    public float jumpHoldSeconds = 0.12f;

    /* ================= ROLL (Bend + Hands Below Hip) ================= */
    [Header("Roll (Bend + Hands Below Hip)")]
    public float wristBelowHipMargin = 0.08f;
    public float torsoCloseThreshold = 0.22f;
    public int rollFramesRequired = 2;
    public float rollHoldSeconds = 0.18f;
    public float rollCooldown = 0.7f;

    /* ================= MOVE (Shoulder Y Motion Energy) ================= */
    [Header("Move (Shoulder Y Motion)")]
    [Tooltip("어깨 중심 y 변화량을 이만큼까지는 0으로(잡음 제거)")]
    public float shoulderDeltaDeadzone = 0.0025f;

    [Tooltip("STOP/WALK 경계 (EMA 에너지 기준)")]
    public float walkThreshold = 0.010f;

    [Tooltip("WALK/RUN 경계 (EMA 에너지 기준)")]
    public float runThreshold = 0.030f;

    [Tooltip("에너지 EMA 스무딩 속도. 클수록 빠르게 반응")]
    public float energySmoothing = 10f;

    [Tooltip("상태 변경이 튀지 않게 최소 유지 시간(초)")]
    public float stateHoldSeconds = 0.15f;

    /* ================= Debug ================= */
    [Header("Debug")]
    public bool hasLandmarksDebug;
    public bool rollBendDebug;
    public float shYDebug, hipYDebug, torsoGapDebug;
    public float energyDebug;
    public int moveStateDebug; // 0 stop, 1 walk, 2 run

    public float centerXDebug;
    public float dxDebug;
    public int laneCandidateDebug;

    /* ================= Internal ================= */
    private readonly Vector3[] _lm = new Vector3[33];
    private bool _hasLm;

    private float _jumpCd;
    private int _handsUpFrames;
    private float _jumpHold;

    private float _rollCd;
    private int _rollFrames;
    private float _rollHold;

    // move energy
    private float _prevShY;
    private bool _hasPrevShY = false;
    private float _energyEma = 0f;

    private int _state = 0; // 0 stop, 1 walk, 2 run
    private int _pendingState = 0;
    private float _stateHold = 0f;

    // lane stabilize
    private int _pendingLane = 0;
    private float _laneHold = 0f;

    public void SetLandmarks(Vector3[] src)
    {
        if (src == null || src.Length < 33) return;
        for (int i = 0; i < 33; i++) _lm[i] = src[i];
        _hasLm = true;
        hasLandmarksDebug = true;
    }

    void Update()
    {
        // 홀드 처리(트리거처럼)
        if (_jumpHold > 0f) { _jumpHold -= Time.deltaTime; JumpTriggered = true; }
        else JumpTriggered = false;

        if (_rollHold > 0f) { _rollHold -= Time.deltaTime; RollHeld = true; }
        else RollHeld = false;

        // 쿨다운
        if (_jumpCd > 0f) _jumpCd -= Time.deltaTime;
        if (_rollCd > 0f) _rollCd -= Time.deltaTime;

        if (!_hasLm)
        {
            MoveLevel = 0f;
            Lane = 0;
            _pendingLane = 0;
            _laneHold = 0f;

            _hasPrevShY = false;
            _energyEma = 0f;
            return;
        }

        // Landmarks
        Vector3 lSh = _lm[11];
        Vector3 rSh = _lm[12];
        Vector3 lWr = _lm[15];
        Vector3 rWr = _lm[16];
        Vector3 lHip = _lm[23];
        Vector3 rHip = _lm[24];

        float shY = (lSh.y + rSh.y) * 0.5f;
        float hipY = (lHip.y + rHip.y) * 0.5f;

        shYDebug = shY;
        hipYDebug = hipY;

        /* ================= LANE (left/right by shoulder center X) ================= */
        float centerX = (lSh.x + rSh.x) * 0.5f; // 0~1
        if (mirrorX) centerX = 1f - centerX;    // 좌우가 반대로 나오면 이걸 켜

        centerXDebug = centerX;
        float dx = centerX - 0.5f;
        dxDebug = dx;

        int laneCandidate;
        if (dx < -laneStrongThreshold) laneCandidate = -1;
        else if (dx > laneStrongThreshold) laneCandidate = 1;
        else if (Mathf.Abs(dx) < laneDeadZone) laneCandidate = 0;
        else laneCandidate = Lane; // 중간 영역에서는 기존 Lane 유지(떨림 방지)

        laneCandidateDebug = laneCandidate;

        // lane 확정까지 hold
        if (laneCandidate != _pendingLane)
        {
            _pendingLane = laneCandidate;
            _laneHold = 0f;
        }
        else
        {
            _laneHold += Time.deltaTime;
            if (_laneHold >= laneHoldSeconds)
                Lane = _pendingLane;
        }

        /* ================= MOVE LEVEL (Shoulder Y Energy) ================= */
        if (!_hasPrevShY)
        {
            _prevShY = shY;
            _hasPrevShY = true;
        }
        float dy = Mathf.Abs(shY - _prevShY);
        _prevShY = shY;

        if (dy < shoulderDeltaDeadzone) dy = 0f;

        float t = 1f - Mathf.Exp(-energySmoothing * Time.deltaTime);
        _energyEma = Mathf.Lerp(_energyEma, dy, t);
        energyDebug = _energyEma;

        int targetState;
        if (_energyEma < walkThreshold) targetState = 0;      // STOP
        else if (_energyEma < runThreshold) targetState = 1;  // WALK
        else targetState = 2;                                  // RUN

        if (targetState != _pendingState)
        {
            _pendingState = targetState;
            _stateHold = 0f;
        }
        else
        {
            _stateHold += Time.deltaTime;
            if (_stateHold >= stateHoldSeconds)
            {
                _state = _pendingState;
            }
        }

        moveStateDebug = _state;
        MoveLevel = (_state == 0) ? 0f : (_state == 1 ? 0.5f : 1f);

        /* ================= JUMP ================= */
        bool handsUp =
            (lWr.y < lSh.y - handsUpMargin) &&
            (rWr.y < rSh.y - handsUpMargin);

        if (handsUp) _handsUpFrames++;
        else _handsUpFrames = 0;

        if (_handsUpFrames >= handsUpFramesRequired && _jumpCd <= 0f)
        {
            _jumpHold = jumpHoldSeconds;
            _jumpCd = jumpCooldown;
            _handsUpFrames = 0;
            Debug.Log("[PoseInput] JUMP");
        }

        /* ================= ROLL (BEND + HANDS BELOW HIP) ================= */
        bool wristsBelowHip =
            (lWr.y > hipY + wristBelowHipMargin) &&
            (rWr.y > hipY + wristBelowHipMargin);

        float torsoGap = Mathf.Abs(shY - hipY);
        torsoGapDebug = torsoGap;

        bool torsoBent = torsoGap < torsoCloseThreshold;

        rollBendDebug = wristsBelowHip && torsoBent;

        if (rollBendDebug && _rollCd <= 0f) _rollFrames++;
        else _rollFrames = 0;

        if (_rollFrames >= rollFramesRequired && _rollCd <= 0f)
        {
            _rollHold = rollHoldSeconds;
            _rollCd = rollCooldown;
            _rollFrames = 0;
            Debug.Log("[PoseInput] ROLL (BEND+HANDS BELOW HIP)");
        }
    }
}
