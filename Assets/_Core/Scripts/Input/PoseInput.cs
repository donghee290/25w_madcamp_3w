using UnityEngine;

public class PoseInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }            // -1,0,1
    public bool JumpTriggered { get; private set; }  // 트리거(짧게 유지)
    public bool RollHeld { get; private set; }       // 트리거(짧게 유지)
    public float MoveLevel { get; private set; }     // 0~1 (연속)
    public bool FlyForward { get; private set; }


    /* ================= LANE (Body Left/Right) ================= */
    [Header("Lane (Body Left/Right)")]
    public float laneDeadZone = 0.06f;
    public float laneStrongThreshold = 0.16f;
    public float laneHoldSeconds = 0.10f;
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
    public float rollHoldSeconds = 0.22f;   // 달릴 때도 확실히 잡히게 약간 늘림
    public float rollCooldown = 0.45f;      // 너무 길면 답답해서 줄임

    /* ================= MOVE (Shoulder Y Motion Energy) ================= */
    [Header("MoveLevel 0~1 (Shoulder Y energy)")]
    public float shoulderDeltaDeadzone = 0.0008f;
    public float walkThreshold = 0.001f;      // 0 근처
    public float runThreshold = 0.0020f;     // RUN 쉽게(낮을수록 쉬움)
    public float energySmoothing = 25f;
    public float moveLevelSmoothing = 12f;
    public float moveCurve = 1.35f;           // 작을수록 상단(달리기) 빨리 붙음

    /* ================= Debug ================= */
    [Header("Debug")]
    public bool hasLandmarksDebug;
    public float shYDebug, hipYDebug;
    public float energyDebug, rawMoveDebug;
    public float centerXDebug, dxDebug;
    public bool handsUpDebug, rollBendDebug;

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
        // Jump/Roll hold (트리거처럼)
        if (_jumpHold > 0f) { _jumpHold -= Time.deltaTime; JumpTriggered = true; }
        else JumpTriggered = false;

        if (_rollHold > 0f) { _rollHold -= Time.deltaTime; RollHeld = true; }
        else RollHeld = false;

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
            if (_laneHold >= laneHoldSeconds)
                Lane = _pendingLane;
        }

        /* ================= MOVELEVEL (continuous) ================= */
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
        // 달리기 쉽게 상단 부스팅(원치 않으면 이 줄 삭제해도 됨)
        raw = 1f - Mathf.Pow(1f - raw, 1.5f);

        rawMoveDebug = raw;

        float tM = 1f - Mathf.Exp(-moveLevelSmoothing * Time.deltaTime);
        MoveLevel = Mathf.Lerp(MoveLevel, raw, tM);

        /* ================= JUMP ================= */
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

        /* ================= ROLL ================= */
        bool wristsBelowHip =
            (lWr.y > hipY + wristBelowHipMargin) &&
            (rWr.y > hipY + wristBelowHipMargin);

        float torsoGap = Mathf.Abs(shY - hipY);
        bool torsoBent = torsoGap < torsoCloseThreshold;

        rollBendDebug = wristsBelowHip && torsoBent;

        if (rollBendDebug && _rollCd <= 0f) _rollFrames++;
        else _rollFrames = 0;

        if (_rollFrames >= rollFramesRequired && _rollCd <= 0f)
        {
            _rollHold = rollHoldSeconds;
            _rollCd = rollCooldown;
            _rollFrames = 0;
            Debug.Log("[PoseInput] ROLL");
        }
    }
}
