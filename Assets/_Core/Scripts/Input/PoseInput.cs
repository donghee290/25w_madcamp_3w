using UnityEngine;

public class PoseInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }            // -1,0,1
    public bool JumpTriggered { get; private set; }  // 트리거(짧게 유지)
    public bool RollHeld { get; private set; }       // 트리거(짧게 유지)
    public float MoveLevel { get; private set; }     // 0~1 (연속)

    [Header("Lane (Body Left/Right)")]
    public float laneDeadZone = 0.06f;
    public float laneStrongThreshold = 0.16f;
    public float laneHoldSeconds = 0.10f;
    public bool mirrorX = false;

    [Header("Jump (Hands Up)")]
    public float handsUpMargin = 0.03f;
    public float jumpCooldown = 0.6f;
    public int handsUpFramesRequired = 2;
    public float jumpHoldSeconds = 0.12f;

    [Header("MoveLevel 0~1 (Shoulder Y energy)")]
    public float shoulderDeltaDeadzone = 0.0008f;
    public float walkThreshold = 0.001f;
    public float runThreshold = 0.0020f;
    public float energySmoothing = 25f;
    public float moveLevelSmoothing = 12f;
    public float moveCurve = 1.35f;

    [Header("Roll (RUN-Friendly)")]
    [Tooltip("손목 y가 힙 y보다 아래(더 큰 y)로 내려가야 롤 인정")]
    public float wristBelowHipMargin = 0.06f;

    [Tooltip("숙임 판정: |shoulderY-hipY| < threshold 이면 숙임. 달릴수록 자동 완화됨")]
    public float torsoCloseThreshold = 0.28f;

    [Tooltip("rollCandidate가 이 시간(초) 유지되면 롤 트리거")]
    public float rollDetectSeconds = 0.10f;

    [Tooltip("트리거를 놓치지 않게 RollHeld 유지 시간")]
    public float rollHoldSeconds = 0.35f;

    public float rollCooldown = 0.25f;

    [Tooltip("달릴수록( MoveLevel 높을수록 ) 롤이 더 쉽게 되도록 threshold를 얼마나 완화할지")]
    public float runRollEase = 0.08f; // 0.05~0.12 추천

    [Header("Debug")]
    public bool hasLandmarksDebug;
    public float shYDebug, hipYDebug;
    public float energyDebug, rawMoveDebug;
    public float centerXDebug, dxDebug;
    public bool handsUpDebug, rollCandidateDebug;
    public float rollDetectTimerDebug;

    private readonly Vector3[] _lm = new Vector3[33];
    private bool _hasLm;

    private float _jumpCd;
    private int _handsUpFrames;
    private float _jumpHold;

    private float _rollCd;
    private float _rollHold;
    private float _rollDetectTimer;

    private int _pendingLane = 0;
    private float _laneHold = 0f;

    private float _prevShY;
    private bool _hasPrevShY = false;
    private float _energyEma = 0f;

    public void SetLandmarks(Vector3[] src)
    {
        if (src == null || src.Length < 33) return;
        for (int i = 0; i < 33; i++) _lm[i] = src[i];
        _hasLm = true;
        hasLandmarksDebug = true;
    }

    void Update()
    {
        // hold triggers
        JumpTriggered = false;
        RollHeld = false;

        if (_jumpHold > 0f) { _jumpHold -= Time.deltaTime; JumpTriggered = true; }
        if (_rollHold > 0f) { _rollHold -= Time.deltaTime; RollHeld = true; }

        if (_jumpCd > 0f) _jumpCd -= Time.deltaTime;
        if (_rollCd > 0f) _rollCd -= Time.deltaTime;

        if (!_hasLm)
        {
            Lane = 0;
            MoveLevel = 0f;
            _pendingLane = 0;
            _laneHold = 0f;
            _hasPrevShY = false;
            _energyEma = 0f;
            _rollDetectTimer = 0f;
            rollDetectTimerDebug = 0f;
            return;
        }

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

        // ================= LANE =================
        float centerX = (lSh.x + rSh.x) * 0.5f;
        if (mirrorX) centerX = 1f - centerX;
        centerXDebug = centerX;

        float dx = centerX - 0.5f;
        dxDebug = dx;

        int laneCandidate;
        if (dx < -laneStrongThreshold) laneCandidate = -1;
        else if (dx > laneStrongThreshold) laneCandidate = 1;
        else if (Mathf.Abs(dx) < laneDeadZone) laneCandidate = 0;
        else laneCandidate = Lane;

        if (laneCandidate != _pendingLane)
        {
            _pendingLane = laneCandidate;
            _laneHold = 0f;
        }
        else
        {
            _laneHold += Time.deltaTime;
            if (_laneHold >= laneHoldSeconds) Lane = _pendingLane;
        }

        // ================= MOVELEVEL (continuous) =================
        if (!_hasPrevShY)
        {
            _prevShY = shY;
            _hasPrevShY = true;
        }

        float dy = Mathf.Abs(shY - _prevShY);
        _prevShY = shY;

        if (dy < shoulderDeltaDeadzone) dy = 0f;

        float tE = 1f - Mathf.Exp(-energySmoothing * Time.deltaTime);
        _energyEma = Mathf.Lerp(_energyEma, dy, tE);
        energyDebug = _energyEma;

        float raw;
        if (_energyEma <= walkThreshold) raw = 0f;
        else if (_energyEma >= runThreshold) raw = 1f;
        else raw = (_energyEma - walkThreshold) / (runThreshold - walkThreshold);

        raw = Mathf.Clamp01(raw);
        raw = Mathf.Pow(raw, Mathf.Max(0.2f, moveCurve));
        raw = 1f - Mathf.Pow(1f - raw, 1.4f); // 상단 부스팅(달리기 쉽게)
        rawMoveDebug = raw;

        float tM = 1f - Mathf.Exp(-moveLevelSmoothing * Time.deltaTime);
        MoveLevel = Mathf.Lerp(MoveLevel, raw, tM);

        // ================= JUMP =================
        bool handsUp =
            (lWr.y < lSh.y - handsUpMargin) &&
            (rWr.y < rSh.y - handsUpMargin);

        handsUpDebug = handsUp;

        if (handsUp) _handsUpFrames++;
        else _handsUpFrames = 0;

        if (_handsUpFrames >= handsUpFramesRequired && _jumpCd <= 0f)
        {
            _jumpHold = jumpHoldSeconds;
            _jumpCd = jumpCooldown;
            _handsUpFrames = 0;
            Debug.Log("[PoseInput] JUMP");
        }

        // ================= ROLL (RUN-Friendly) =================
        // 달릴수록 숙임 판정 완화(롤 더 잘 됨)
        float easedTorsoThreshold = torsoCloseThreshold + MoveLevel * runRollEase;

        bool wristsBelowHip =
            (lWr.y > hipY + wristBelowHipMargin) &&
            (rWr.y > hipY + wristBelowHipMargin);

        float torsoGap = Mathf.Abs(shY - hipY);
        bool torsoBent = torsoGap < easedTorsoThreshold;

        bool rollCandidate = wristsBelowHip && torsoBent;
        rollCandidateDebug = rollCandidate;

        if (rollCandidate && _rollCd <= 0f) _rollDetectTimer += Time.deltaTime;
        else _rollDetectTimer = 0f;

        rollDetectTimerDebug = _rollDetectTimer;

        if (_rollDetectTimer >= rollDetectSeconds && _rollCd <= 0f)
        {
            _rollHold = rollHoldSeconds; // 트리거 유지
            _rollCd = rollCooldown;
            _rollDetectTimer = 0f;

            Debug.Log("[PoseInput] ROLL TRIGGERED");
        }
    }
}
