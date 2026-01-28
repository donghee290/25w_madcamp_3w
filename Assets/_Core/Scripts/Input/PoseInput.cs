using UnityEngine;

public class PoseInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }            // -1,0,1
    public bool JumpTriggered { get; private set; }  // 트리거
    public bool RollHeld { get; private set; }       // 홀드
    public float MoveLevel { get; private set; }     // 0~1
    public bool FlyForward { get; private set; }     // ✅ 추가 유지

    /* ================= LANE ================= */
    [Header("Lane (Body Left/Right)")]
    public float laneDeadZone = 0.06f;
    public float laneStrongThreshold = 0.16f;
    public float laneHoldSeconds = 0.10f;
    public bool mirrorX = false;

    /* ================= JUMP ================= */
    [Header("Jump (Hands Up)")]
    public float handsUpMargin = 0.03f;
    public float jumpCooldown = 0.6f;
    public int handsUpFramesRequired = 2;
    public float jumpHoldSeconds = 0.12f;

    /* ================= ROLL ================= */
    [Header("Roll (Bend + Hands Below Hip)")]
    public float wristBelowHipMargin = 0.08f;
    public float torsoCloseThreshold = 0.22f;
    public int rollFramesRequired = 2;
    public float rollHoldSeconds = 0.22f;
    public float rollCooldown = 0.45f;

    /* ================= MOVE (Shoulder Y Energy) ================= */
    [Header("MoveLevel 0~1 (Shoulder Y energy)")]
    public float shoulderDeltaDeadzone = 0.0008f;
    public float walkThreshold = 0.001f;
    public float runThreshold = 0.0020f;
    public float energySmoothing = 25f;
    public float moveLevelSmoothing = 12f;
    public float moveCurve = 1.35f;

    /* ================= FLY (Arms Out) ================= */
    [Header("Fly (Arms Out Hold)")]
    public float armsOutMinX = 0.18f;        // 어깨 중심 기준 좌우 벌어짐
    public float wristNearShoulderY = 0.12f;

    /* ================= Debug ================= */
    [Header("Debug")]
    public bool hasLandmarksDebug;
    public float shYDebug, hipYDebug;
    public float energyDebug, rawMoveDebug;
    public float centerXDebug, dxDebug;
    public bool handsUpDebug, rollBendDebug, flyDebug;

    /* ================= Internal ================= */
    private readonly Vector3[] _lm = new Vector3[33];
    private bool _hasLm;

    private float _jumpCd;
    private int _handsUpFrames;
    private float _jumpHold;

    private float _rollCd;
    private int _rollFrames;
    private float _rollHold;

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
        // ===== Trigger 유지 =====
        JumpTriggered = _jumpHold > 0f;
        RollHeld = _rollHold > 0f;

        if (_jumpHold > 0f) _jumpHold -= Time.deltaTime;
        if (_rollHold > 0f) _rollHold -= Time.deltaTime;
        if (_jumpCd > 0f) _jumpCd -= Time.deltaTime;
        if (_rollCd > 0f) _rollCd -= Time.deltaTime;

        if (!_hasLm)
        {
            Lane = 0;
            MoveLevel = 0f;
            FlyForward = false;
            _hasPrevShY = false;
            _energyEma = 0f;
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

        /* ================= LANE ================= */
        float centerX = (lSh.x + rSh.x) * 0.5f;
        if (mirrorX) centerX = 1f - centerX;
        centerXDebug = centerX;

        float dx = centerX - 0.5f;
        dxDebug = dx;

        int laneCandidate =
            dx < -laneStrongThreshold ? -1 :
            dx > laneStrongThreshold ? 1 :
            Mathf.Abs(dx) < laneDeadZone ? 0 : Lane;

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

        /* ================= MOVE ================= */
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

        float raw =
            _energyEma <= walkThreshold ? 0f :
            _energyEma >= runThreshold ? 1f :
            (_energyEma - walkThreshold) / (runThreshold - walkThreshold);

        raw = Mathf.Clamp01(raw);
        raw = Mathf.Pow(raw, Mathf.Max(0.2f, moveCurve));
        raw = 1f - Mathf.Pow(1f - raw, 1.5f);

        rawMoveDebug = raw;

        float tM = 1f - Mathf.Exp(-moveLevelSmoothing * Time.deltaTime);
        MoveLevel = Mathf.Lerp(MoveLevel, raw, tM);

        /* ================= JUMP ================= */
        bool handsUp =
            (lWr.y < lSh.y - handsUpMargin) &&
            (rWr.y < rSh.y - handsUpMargin);
        handsUpDebug = handsUp;

        _handsUpFrames = handsUp ? _handsUpFrames + 1 : 0;

        if (_handsUpFrames >= handsUpFramesRequired && _jumpCd <= 0f)
        {
            _jumpHold = jumpHoldSeconds;
            _jumpCd = jumpCooldown;
            _handsUpFrames = 0;
        }

        /* ================= ROLL ================= */
        bool wristsBelowHip =
            (lWr.y > hipY + wristBelowHipMargin) &&
            (rWr.y > hipY + wristBelowHipMargin);

        bool torsoBent = Mathf.Abs(shY - hipY) < torsoCloseThreshold;
        rollBendDebug = wristsBelowHip && torsoBent;

        _rollFrames = (rollBendDebug && _rollCd <= 0f) ? _rollFrames + 1 : 0;

        if (_rollFrames >= rollFramesRequired && _rollCd <= 0f)
        {
            _rollHold = rollHoldSeconds;
            _rollCd = rollCooldown;
            _rollFrames = 0;
        }

        /* ================= FLY ================= */
        float shCenterX = (lSh.x + rSh.x) * 0.5f;
        if (mirrorX) shCenterX = 1f - shCenterX;

        FlyForward =
            (lWr.x < shCenterX - armsOutMinX) &&
            (rWr.x > shCenterX + armsOutMinX) &&
            (Mathf.Abs(lWr.y - lSh.y) < wristNearShoulderY) &&
            (Mathf.Abs(rWr.y - rSh.y) < wristNearShoulderY);

        flyDebug = FlyForward;
    }
}
