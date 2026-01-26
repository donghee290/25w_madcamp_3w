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
    public string paramMoveLevel = "MoveLevel";     // Float (0=Idle, 0.5=Walk, 1=Run)
    public string paramIsGrounded = "IsGrounded";   // Bool
    public string trigJump = "Jump";                // Trigger
    public string trigRoll = "Roll";                // Trigger

    [Header("Lane")]
    public float laneWidth = 1.2f;
    public float laneMoveSpeed = 14f;

    [Header("Forward Speeds")]
    public float runSpeed = 10f;
    public float walkSpeed = 6f;
    public float stopSpeed = 0f;

    [Tooltip("forwardSpeed가 targetSpeed를 따라가는 속도(클수록 빨리 반응)")]
    public float speedLerp = 8f;

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
    private float currentX;
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

            // 시작 애니/파라미터를 확실히 Idle로
            if (!string.IsNullOrEmpty(paramMoveLevel)) anim.SetFloat(paramMoveLevel, 0f);
            if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, true);
        }
    }

    void ResolveInput()
    {
        if (inputSource != null && inputSource is IPlayerInput i) { input = i; return; }

        var monos = GetComponents<MonoBehaviour>();
        foreach (var m in monos)
        {
            if (m == null) continue;
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
        Debug.LogError("[PlayerMotor] Animator with controller not found.");
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
        if (GameManager.I != null && GameManager.I.State != GameState.Playing)
        {
            // 정지 상태면 파라미터만 Idle로 고정
            if (anim != null)
            {
                if (!string.IsNullOrEmpty(paramMoveLevel)) anim.SetFloat(paramMoveLevel, 0f);
                if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, true);
            }
            return;
        }

        if (input == null) return;
        if (anim != null && anim.applyRootMotion) anim.applyRootMotion = false;

        bool grounded = IsGrounded();
        isGroundedCached = grounded;

        // 중력/코요테
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

        // 점프(롤 중 점프 금지)
        if (coyoteTimer > 0f && input.JumpTriggered && !rollHeld)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            coyoteTimer = 0f;

            if (anim != null && !string.IsNullOrEmpty(trigJump))
                anim.SetTrigger(trigJump);
        }

        // 레인
        int lane = Mathf.Clamp(input.Lane, -1, 1);
        float targetX = lane * laneWidth;
        currentX = Mathf.Lerp(currentX, targetX, Time.deltaTime * laneMoveSpeed);

        // ===== 핵심: 입력 MoveLevel(0/0.5/1) 그대로 속도/애니에 사용 =====
        float r = Mathf.Clamp01(input.MoveLevel);

        float targetSpeed =
            (r >= 0.75f) ? runSpeed :
            (r >= 0.25f) ? walkSpeed :
            stopSpeed;

        forwardSpeed = Mathf.Lerp(forwardSpeed, targetSpeed, Time.deltaTime * speedLerp);

        // 슬라이드
        float desiredHeight = rollHeld ? slideHeight : normalHeight;
        cc.height = Mathf.Lerp(cc.height, desiredHeight, Time.deltaTime * slideLerp);
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        if (rollStarted)
        {
            if (anim != null && !string.IsNullOrEmpty(trigRoll))
                anim.SetTrigger(trigRoll);
        }

        // 이동
        Vector3 move = Vector3.zero;

        float dx = currentX - transform.position.x;
        move.x = Mathf.Clamp(dx, -laneMoveSpeed * Time.deltaTime, laneMoveSpeed * Time.deltaTime);

        move.z = forwardSpeed * Time.deltaTime;
        move.y = verticalVel * Time.deltaTime;

        cc.Move(move);

        if (GameManager.I != null && GameManager.I.State != GameState.Playing)
        {
            ForceStopToIdle();
            return;
        }

        // 애니 파라미터: 속도 기반 판정 삭제, r(입력) 그대로
        if (anim != null)
        {
            if (!string.IsNullOrEmpty(paramIsGrounded))
                anim.SetBool(paramIsGrounded, grounded);

            if (!string.IsNullOrEmpty(paramMoveLevel))
            {
                // r이 0/0.5/1이라면 바로 넣어도 되고,
                // 부드럽게 하고 싶으면 damping을 쓰세요.
                anim.SetFloat(paramMoveLevel, r);
                // 또는: anim.SetFloat(paramMoveLevel, r, 0.08f, Time.deltaTime);
            }
        }
    }

    void LateUpdate()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualLocalPos0;
            visualRoot.localRotation = visualLocalRot0;
        }
    }

    public void ForceStopToIdle()
    {
        forwardSpeed = 0f;
        verticalVel = 0f;

        cc.height = normalHeight;
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        if (cc != null)
            cc.Move(Vector3.zero);

        if (anim != null)
        {
            if (!string.IsNullOrEmpty(paramMoveLevel)) anim.SetFloat(paramMoveLevel, 0f);
            if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, true);

            if (!string.IsNullOrEmpty(trigJump)) anim.ResetTrigger(trigJump);
            if (!string.IsNullOrEmpty(trigRoll)) anim.ResetTrigger(trigRoll);

            anim.SetFloat(paramMoveLevel, 0f);
            anim.SetBool(paramIsGrounded, true);
        }
    }
}