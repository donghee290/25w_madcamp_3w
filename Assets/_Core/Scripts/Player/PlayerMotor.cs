using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Input (required)")]
    [Tooltip("IPlayerInput을 구현한 컴포넌트(KeyboardInput / PoseInput 등). 비우면 같은 오브젝트에서 자동으로 찾습니다.")]
    public MonoBehaviour inputSource;
    private IPlayerInput input;

    [Header("Animator (required)")]
    [Tooltip("비우면 자식 포함 Animator 중 'Controller가 붙어있는 Animator'를 자동으로 찾아 연결합니다.")]
    public Animator anim;

    [Header("Animator Params (only existing ones)")]
    public string paramIsRunning = "IsRunning";
    public string paramIsGrounded = "IsGrounded";
    public string trigJump = "Jump";
    public string trigRoll = "Roll";

    [Header("Lane")]
    public float laneWidth = 1.2f;
    public float laneMoveSpeed = 14f;

    [Header("Forward Speeds (NEW LOOP)")]
    public float runSpeed = 10f;     // RUN 목표
    public float walkSpeed = 6f;     // WALK 목표
    public float stopSpeed = 0f;     // STOP 목표

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

    [Header("Ground Check (IMPORTANT)")]
    public float groundRayLength = 0.6f;
    public float groundRayStartUp = 0.05f;
    public LayerMask groundMask = ~0;
    public float coyoteTime = 0.12f;
    private float coyoteTimer = 0f;

    private CharacterController cc;
    private float currentX;
    private float verticalVel;
    private bool prevRollHeld = false;

    // 외부 참조용(충돌/판정)
    private bool isGroundedCached;
    public bool IsRolling => input != null && input.RollHeld;
    public bool IsAirborne => !isGroundedCached;
    public float CurrentForwardSpeed => forwardSpeed;

    [Header("Debug")]
    public bool debugLogs = false;

    // 애니메이션이 트랜스폼을 덮어쓰는 경우를 강제로 막기 위한 값들
    private Transform visualRoot;
    private Vector3 visualLocalPos0;
    private Quaternion visualLocalRot0;

    private Vector3 posAfterMove;     // cc.Move 직후 “정답 위치”
    private bool hasPosAfterMove;

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        ResolveInput();
        ResolveAnimator();

        // CharacterController 기본 세팅
        cc.height = normalHeight;
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        currentX = transform.position.x;
        verticalVel = 0f;

        SnapToGroundOnce();
        prevRollHeld = false;

        // Animator 강제 설정
        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.updateMode = AnimatorUpdateMode.Normal;
        }

        // 비주얼 로컬 고정값 저장
        if (anim != null)
        {
            visualRoot = anim.transform;
            visualLocalPos0 = visualRoot.localPosition;
            visualLocalRot0 = visualRoot.localRotation;
        }

        if (debugLogs)
        {
            Debug.Log(
                $"[PlayerMotor] input={(input == null ? "NULL" : input.GetType().Name)} " +
                $"anim={(anim == null ? "NULL" : anim.name)} " +
                $"animGO={(anim == null ? "NULL" : anim.gameObject.name)} " +
                $"applyRootMotion={(anim != null && anim.applyRootMotion)}"
            );
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
        Debug.LogError("[PlayerMotor] IPlayerInput not found. (Attach KeyboardInput or PoseInput on the same GameObject, or assign inputSource)");
    }

    void ResolveAnimator()
    {
        if (anim != null && anim.runtimeAnimatorController != null) return;

        var a0 = GetComponent<Animator>();
        if (a0 != null && a0.runtimeAnimatorController != null)
        {
            anim = a0;
            return;
        }

        var anims = GetComponentsInChildren<Animator>(true);
        foreach (var a in anims)
        {
            if (a != null && a.runtimeAnimatorController != null)
            {
                anim = a;
                return;
            }
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

        // =========================
        // 전진 속도 (NEW LOOP)
        // MoveLevel(0~1) → RunState → targetSpeed → forwardSpeed Lerp
        // =========================
        float r = Mathf.Clamp01(input.MoveLevel);

        // 히스테리시스까지 굳이 필요 없으면 아래 3단 분기만으로 충분(키는 0/0.5/1이라 안정적)
        // 히스테리시스가 꼭 필요하면 RunState를 멤버로 빼서 상태 유지형으로 확장 가능.
        float targetSpeed;
        bool isMoving;

        if (r >= 0.7f) { targetSpeed = runSpeed; isMoving = true; }
        else if (r >= 0.3f) { targetSpeed = walkSpeed; isMoving = true; }
        else { targetSpeed = stopSpeed; isMoving = false; }

        // 부드럽게 따라가기
        forwardSpeed = Mathf.Lerp(forwardSpeed, targetSpeed, Time.deltaTime * speedLerp);

        // 슬라이드(캐릭터컨트롤러 높이)
        float desiredHeight = rollHeld ? slideHeight : normalHeight;
        cc.height = Mathf.Lerp(cc.height, desiredHeight, Time.deltaTime * slideLerp);
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        if (rollStarted)
        {
            if (anim != null && !string.IsNullOrEmpty(trigRoll))
                anim.SetTrigger(trigRoll);
        }

        // 이동 벡터
        Vector3 move = Vector3.zero;

        // x: 레인 보정
        float dx = currentX - transform.position.x;
        move.x = Mathf.Clamp(dx, -laneMoveSpeed * Time.deltaTime, laneMoveSpeed * Time.deltaTime);

        // z: 전진
        move.z = forwardSpeed * Time.deltaTime;

        // y: 중력/점프
        move.y = verticalVel * Time.deltaTime;

        cc.Move(move);

        posAfterMove = transform.position;
        hasPosAfterMove = true;

        // 애니메이터 파라미터
        if (anim != null)
        {
            if (!string.IsNullOrEmpty(paramIsRunning))
                anim.SetBool(paramIsRunning, isMoving);  // RUN 상태일 때만 러닝 애니

            if (!string.IsNullOrEmpty(paramIsGrounded))
                anim.SetBool(paramIsGrounded, grounded);
        }

        if (debugLogs && Time.frameCount % 30 == 0)
        {
            Debug.Log(
                $"[PlayerMotor] MoveLevel={r:0.00} target={targetSpeed:0.0} fwd={forwardSpeed:0.00} grounded={grounded} " +
                $"jumpTrig={input.JumpTriggered} roll={rollHeld} lane={lane}"
            );
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
                {
                    Debug.LogWarning(
                        $"[PlayerMotor] Transform was overwritten after Move(). restoring. " +
                        $"now=({now.x:0.00},{now.y:0.00},{now.z:0.00}) expected=({expected.x:0.00},{expected.y:0.00},{expected.z:0.00})"
                    );
                }

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
