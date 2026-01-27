using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Input (required)")]
    public MonoBehaviour inputSource;
    private IPlayerInput input;

    [Header("Animator (required)")]
    public Animator anim;

    [Header("Animator Params")]
    public string paramIsRunning = "IsRunning";
    public string paramIsGrounded = "IsGrounded";
    public string trigJump = "Jump";
    public string trigRoll = "Roll";

    [Header("Lane")]
    public float laneWidth = 1.2f;
    public float laneSmoothTime = 0.08f;
    public float laneMaxSpeed = 18f;

    [Header("Forward (Continuous)")]
    public float maxForwardSpeed = 14f;
    public float accelTime = 0.25f;
    public float decelTime = 0.55f;

    [Tooltip("MoveLevel이 이 값 이하 + 지면이면 forwardSpeed를 즉시 0으로 스냅")]
    public float stopSnapThreshold = 0.03f;

    [Tooltip("완전 STOP이 어색하면 0.05~0.12 (완전 멈춤 원하면 0)")]
    public float minMoveLevel = 0.00f;

    [Tooltip("현재 전진 속도(디버그/표시용)")]
    public float forwardSpeed = 0f;

    [Header("Roll Slide (keep speed)")]
    public float rollSpeedFactor = 0.75f; // 조금 더 유지감 있게
    public float rollDecelTime = 0.18f;
    public float rollMinSpeed = 4.0f;     // 롤 중 최소 전진속도(슬라이드 느낌)
    private float rollEntrySpeed = 0f;

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
    private float currentX;
    private float laneVel;
    private float verticalVel;
    private bool prevRollHeld = false;

    private bool isGroundedCached;
    public bool IsRolling => input != null && input.RollHeld;
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

        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.updateMode = AnimatorUpdateMode.Normal;
            visualRoot = anim.transform;
            visualLocalPos0 = visualRoot.localPosition;
            visualLocalRot0 = visualRoot.localRotation;
        }
    }

    void ResolveInput()
    {
        if (inputSource != null && inputSource is IPlayerInput i)
        {
            input = i;
            return;
        }

        foreach (var m in GetComponents<MonoBehaviour>())
        {
            if (m is IPlayerInput ii) { input = ii; return; }
        }

        input = null;
        Debug.LogError("[PlayerMotor] IPlayerInput not found.");
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
        Debug.LogError("[PlayerMotor] Animator not found.");
    }

    void SnapToGroundOnce()
    {
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
        {
            transform.position += new Vector3(0, hit.point.y - foot.y, 0);
        }
    }

    Vector3 GetFootWorldPos()
    {
        float footOffset = cc.height * 0.5f - cc.radius;
        return transform.position + cc.center - Vector3.up * footOffset;
    }

    bool IsGrounded()
    {
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * groundRayStartUp;
        return Physics.Raycast(origin, Vector3.down, groundRayLength, groundMask, QueryTriggerInteraction.Ignore);
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

        // ✅ Jump: 달리면서 가능
        if (coyoteTimer > 0f && input.JumpTriggered && !rollHeld)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            coyoteTimer = 0f;
            if (anim != null && !string.IsNullOrEmpty(trigJump)) anim.SetTrigger(trigJump);
        }

        // ✅ Roll: 달리면서 가능 + 속도 저장
        if (rollStarted)
        {
            rollEntrySpeed = forwardSpeed;
            if (anim != null && !string.IsNullOrEmpty(trigRoll)) anim.SetTrigger(trigRoll);
        }

        // Lane smooth
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

        // Forward speed
        float ml = Mathf.Clamp01(input.MoveLevel);

        if (rollHeld)
        {
            // ✅ 롤 중 속도 유지(슬라이드)
            float targetRollSpeed = Mathf.Max(rollMinSpeed, rollEntrySpeed * rollSpeedFactor);
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, rollDecelTime));
            forwardSpeed = Mathf.Lerp(forwardSpeed, targetRollSpeed, k);
        }
        else if (ml <= stopSnapThreshold && grounded)
        {
            forwardSpeed = 0f; // 완전 정지
        }
        else
        {
            if (ml > 0f) ml = Mathf.Max(ml, minMoveLevel);

            float targetSpeed = maxForwardSpeed * ml;
            float tau = (forwardSpeed < targetSpeed) ? accelTime : decelTime;
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, tau));
            forwardSpeed = Mathf.Lerp(forwardSpeed, targetSpeed, k);
        }

        // Slide height
        float desiredHeight = rollHeld ? slideHeight : normalHeight;
        cc.height = Mathf.Lerp(cc.height, desiredHeight, Time.deltaTime * slideLerp);
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        // Move
        Vector3 move = Vector3.zero;

        float dx = currentX - transform.position.x;
        float maxDx = laneMaxSpeed * Time.deltaTime;
        move.x = Mathf.Clamp(dx, -maxDx, maxDx);

        move.z = forwardSpeed * Time.deltaTime;
        move.y = verticalVel * Time.deltaTime;

        cc.Move(move);

        posAfterMove = transform.position;
        hasPosAfterMove = true;

        // Animator params (롤 우선)
        if (anim != null)
        {
            bool isRunningForAnim = (forwardSpeed > 0.1f) && !rollHeld;
            if (!string.IsNullOrEmpty(paramIsRunning)) anim.SetBool(paramIsRunning, isRunningForAnim);
            if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, grounded);
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
            transform.position = posAfterMove;
    }

    // GameManager에서 호출하는 비상 정지
    public void ForceStopToIdle()
    {
        forwardSpeed = 0f;
        verticalVel = 0f;
        currentX = transform.position.x;
        laneVel = 0f;

        if (cc != null) cc.Move(Vector3.zero);

        if (anim != null)
        {
            if (!string.IsNullOrEmpty(paramIsRunning)) anim.SetBool(paramIsRunning, false);
            if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, true);
            anim.Rebind();
            anim.Update(0f);
        }
    }
}
