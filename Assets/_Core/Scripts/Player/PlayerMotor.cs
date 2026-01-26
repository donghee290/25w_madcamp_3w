using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Input (required)")]
    public MonoBehaviour inputSource;
    private IPlayerInput input;

    [Header("Animator (required)")]
    public Animator anim;

    [Header("Animator Params (only existing ones)")]
    public string paramIsRunning = "IsRunning";
    public string paramIsGrounded = "IsGrounded";
    public string trigJump = "Jump";
    public string trigRoll = "Roll";

    [Header("Lane")]
    public float laneWidth = 1.2f;
    public float laneSmoothTime = 0.08f; // 0.06~0.12
    public float laneMaxSpeed = 18f;     // 12~20

    [Header("Forward (Continuous)")]
    public float maxForwardSpeed = 14f;
    public float accelTime = 0.25f;     // 가속 빠르게
    public float decelTime = 0.55f;     // 감속 부드럽게

    [Tooltip("MoveLevel이 이 값 이하 + 지면이면 forwardSpeed를 즉시 0으로 스냅")]
    public float stopSnapThreshold = 0.03f;

    [Tooltip("완전 STOP이 어색하면 0.05~0.12 (완전 멈춤 원하면 0)")]
    public float minMoveLevel = 0.00f;

    [Tooltip("현재 전진 속도(디버그/표시용)")]
    public float forwardSpeed = 0f;

    [Header("Jump")]
    public float jumpHeight = 2.5f;
    public float gravity = -15f;

    [Header("Roll/Slide")]
    public float normalHeight = 1.8f;
    public float slideHeight = 0.9f;
    public float slideLerp = 20f;

    [Header("Ground Check")]
    public float groundRayLength = 0.6f;
    public float groundRayStartUp = 0.05f;
    public LayerMask groundMask = ~0;
    public float coyoteTime = 0.12f;
    private float coyoteTimer = 0f;

    private CharacterController cc;

    // lane smoothing
    private float currentX;
    private float laneVel;

    // vertical
    private float verticalVel;
    private bool prevRollHeld = false;

    private bool isGroundedCached;
    public bool IsRolling => input != null && input.RollHeld;
    public bool IsAirborne => !isGroundedCached;
    public float CurrentForwardSpeed => forwardSpeed;

    [Header("Debug")]
    public bool debugLogs = false;

    private Transform visualRoot;
    private Vector3 visualLocalPos0;
    private Quaternion visualLocalRot0;

    private Vector3 posAfterMove;
    private bool hasPosAfterMove;

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        ResolveInput();
        ResolveAnimator();

        cc.height = normalHeight;
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        currentX = transform.position.x;
        verticalVel = 0f;

        SnapToGroundOnce();
        prevRollHeld = false;

        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.updateMode = AnimatorUpdateMode.Normal;

            visualRoot = anim.transform;
            visualLocalPos0 = visualRoot.localPosition;
            visualLocalRot0 = visualRoot.localRotation;
        }

        if (debugLogs)
        {
            Debug.Log($"[PlayerMotor] input={(input == null ? "NULL" : input.GetType().Name)} anim={(anim == null ? "NULL" : anim.name)}");
        }
    }

    void ResolveInput()
    {
        if (inputSource != null && inputSource is IPlayerInput i)
        {
            input = i;
            return;
        }

        var monos = GetComponents<MonoBehaviour>();
        foreach (var m in monos)
        {
            if (m == null) continue;
            if (m is IPlayerInput ii)
            {
                input = ii;
                return;
            }
        }

        input = null;
        Debug.LogError("[PlayerMotor] IPlayerInput not found. Attach PoseInput/KeyboardInput or assign inputSource.");
    }

    void ResolveAnimator()
    {
        if (anim != null && anim.runtimeAnimatorController != null) return;

        var a0 = GetComponent<Animator>();
        if (a0 != null && a0.runtimeAnimatorController != null) { anim = a0; return; }

        var anims = GetComponentsInChildren<Animator>(true);
        foreach (var a in anims)
        {
            if (a != null && a.runtimeAnimatorController != null) { anim = a; return; }
        }

        anim = null;
        Debug.LogError("[PlayerMotor] Animator with controller not found in self/children.");
    }

    void SnapToGroundOnce()
    {
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * 2f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
        {
            float delta = hit.point.y - foot.y;
            transform.position += new Vector3(0, delta, 0);
        }
    }

    Vector3 GetFootWorldPos()
    {
        float half = cc.height * 0.5f;
        float footOffset = half - cc.radius;
        return transform.position + cc.center - Vector3.up * footOffset;
    }

    bool IsGrounded()
    {
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * groundRayStartUp;
        return Physics.Raycast(origin, Vector3.down, out RaycastHit _, groundRayLength, groundMask, QueryTriggerInteraction.Ignore);
    }

    void Update()
    {
        if (input == null) return;
        if (anim != null && anim.applyRootMotion) anim.applyRootMotion = false;

        bool grounded = IsGrounded();
        isGroundedCached = grounded;

        // gravity / coyote
        if (grounded)
        {
            coyoteTimer = coyoteTime;
            if (verticalVel < 0f) verticalVel = -2f;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
            verticalVel += gravity * Time.deltaTime;
        }

        bool rollHeld = input.RollHeld;
        bool rollStarted = rollHeld && !prevRollHeld;
        prevRollHeld = rollHeld;

        // Jump (달리는 중에도 가능) / Roll 중 점프는 금지(원하면 이 조건 제거 가능)
        if (coyoteTimer > 0f && input.JumpTriggered && !rollHeld)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            coyoteTimer = 0f;

            if (anim != null && !string.IsNullOrEmpty(trigJump))
                anim.SetTrigger(trigJump);
        }

        // Roll trigger (달리는 중에도 항상 가능)
        if (rollStarted)
        {
            if (anim != null && !string.IsNullOrEmpty(trigRoll))
                anim.SetTrigger(trigRoll);
        }

        // Lane SmoothDamp
        int lane = Mathf.Clamp(input.Lane, -1, 1);
        float targetX = lane * laneWidth;

        currentX = Mathf.SmoothDamp(
            currentX,
            targetX,
            ref laneVel,
            Mathf.Max(0.0001f, laneSmoothTime),
            laneMaxSpeed,
            Time.deltaTime
        );

        // Forward speed: MoveLevel(0~1) continuous + stop snap
        float ml = Mathf.Clamp01(input.MoveLevel);

        if (ml <= stopSnapThreshold && grounded)
        {
            // 완전 멈춤 (스냅)
            forwardSpeed = 0f;
        }
        else
        {
            if (ml > 0f) ml = Mathf.Max(ml, minMoveLevel);

            float targetSpeed = maxForwardSpeed * ml;

            float tau = (forwardSpeed < targetSpeed) ? accelTime : decelTime;
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, tau));
            forwardSpeed = Mathf.Lerp(forwardSpeed, targetSpeed, k);
        }

        // 슬라이드 높이
        float desiredHeight = rollHeld ? slideHeight : normalHeight;
        cc.height = Mathf.Lerp(cc.height, desiredHeight, Time.deltaTime * slideLerp);
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        // Move vector
        Vector3 move = Vector3.zero;

        float dx = currentX - transform.position.x;
        float maxDx = laneMaxSpeed * Time.deltaTime;
        move.x = Mathf.Clamp(dx, -maxDx, maxDx);

        move.z = forwardSpeed * Time.deltaTime;
        move.y = verticalVel * Time.deltaTime;

        cc.Move(move);

        posAfterMove = transform.position;
        hasPosAfterMove = true;

        // Animator params
        if (anim != null)
        {
            // ✅ 핵심: 롤 중에는 러닝 bool을 내려서 롤 애니가 눌리지 않게
            bool isRunningForAnim = (forwardSpeed > 0.1f) && !rollHeld;

            if (!string.IsNullOrEmpty(paramIsRunning))
                anim.SetBool(paramIsRunning, isRunningForAnim);

            if (!string.IsNullOrEmpty(paramIsGrounded))
                anim.SetBool(paramIsGrounded, grounded);
        }

        if (debugLogs && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[PlayerMotor] ML={ml:0.00} fwd={forwardSpeed:0.00} grounded={grounded} jump={input.JumpTriggered} roll={rollHeld} lane={lane}");
        }
    }

    void LateUpdate()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualLocalPos0;
            visualRoot.localRotation = visualLocalRot0;
        }

        if (hasPosAfterMove)
        {
            const float eps = 0.0005f;

            Vector3 now = transform.position;
            Vector3 expected = posAfterMove;

            if (Mathf.Abs(now.x - expected.x) > eps ||
                Mathf.Abs(now.y - expected.y) > eps ||
                Mathf.Abs(now.z - expected.z) > eps)
            {
                if (debugLogs)
                    Debug.LogWarning("[PlayerMotor] Transform overwritten after Move(). restoring.");

                transform.position = expected;
                currentX = transform.position.x;
            }
        }
    }

    public void ForceStopToIdle()
    {
        forwardSpeed = 0f;
        verticalVel = 0f;

        if (cc != null)
            cc.Move(Vector3.zero);

        if (anim != null)
        {
            if (!string.IsNullOrEmpty(paramIsRunning)) anim.SetBool(paramIsRunning, false);
            if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, true);
            anim.Rebind();
            anim.Update(0f);
        }
    }
}
