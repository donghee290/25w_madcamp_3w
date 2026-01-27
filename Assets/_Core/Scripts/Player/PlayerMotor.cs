using System.Collections;
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
    public string paramMoveLevel = "MoveLevel";     // Float (0=Idle, 0.5=Walk, 1=Run)
    public string paramIsGrounded = "IsGrounded";   // Bool
    public string paramIsFlying = "IsFlying";       // Bool
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

    [Header("Fly")]
    [Tooltip("비행 목표 높이(지면 기준)")]
    public float flyLiftY = 2.4f;

    [Tooltip("올라갈 때 속도")]
    public float flyUpSpeed = 6f;

    [Tooltip("내려올 때 속도")]
    public float flyDownSpeed = 10f;

    [Tooltip("프레임당 최대 상승량(튐/천장박힘 완화)")]
    public float flyMaxUpPerFrame = 0.25f;

    [Tooltip("지면 탐색 거리(비행 높이 기준 계산에 사용)")]
    public float flyGroundProbe = 3.0f;

    [Header("Fly (Immediate Safety)")]
    [Tooltip("아이템을 먹는 순간, 같은 프레임에 위로 올려서 즉시 장애물 무력화")]
    public float flyImmediateStepY = 1.2f;

    [Tooltip("아이템 먹고 아주 짧은 시간 장애물 충돌 무시(죽는 판정 방지)")]
    public float flyGraceSec = 0.6f;

    [Tooltip("없어도 됨. 있으면 Obstacle 레이어 대신 이 마스크의 레이어들과만 충돌 무시를 적용할 수 있습니다.")]
    public LayerMask obstacleMask;

    [Header("StepOffset Patch (anti tiny lift)")]
    [Tooltip("비행 중 stepOffset을 낮춰 '턱 올라타기' 방지 (0.1 이하 추천)")]
    public float flyStepOffset = 0.05f;

    private CharacterController cc;
    private float currentX;
    private float verticalVel;
    private bool prevRollHeld = false;

    private bool isGroundedCached;

    // ===== Fly runtime =====
    private bool isFlying = false;

    // flyGroundY: 비행 시작 시점 지면 y(복귀용)
    private float flyGroundY = 0f;

    private Coroutine flyGraceCo;

    public bool IsFlying => isFlying;
    public bool IsRolling => input != null && input.RollHeld && !isFlying;
    public bool IsAirborne => !isGroundedCached;
    public float CurrentForwardSpeed => forwardSpeed;

    [Header("Debug")]
    public bool debugLogs = false;

    private Transform visualRoot;
    private Vector3 visualLocalPos0;
    private Quaternion visualLocalRot0;

    public static float SafeUntilTime = 0f;

    private bool obstacleIgnored = false;
    private float defaultStepOffset = 0f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        defaultStepOffset = cc.stepOffset; // ✅ 원본 저장

        ResolveInput();
        ResolveAnimator();

        // 초기값
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

            if (!string.IsNullOrEmpty(paramMoveLevel)) anim.SetFloat(paramMoveLevel, 0f);
            if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, true);
            if (!string.IsNullOrEmpty(paramIsFlying)) anim.SetBool(paramIsFlying, false);
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
        Vector3 origin = transform.position + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
        {
            cc.enabled = false;
            transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            cc.enabled = true;
        }
    }

    Vector3 GetFootWorldPos()
    {
        float half = cc.height * 0.5f;
        float footOffset = half - cc.radius;
        return transform.position + cc.center - Vector3.up * footOffset;
    }

    bool IsGroundedRay()
    {
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * groundRayStartUp;
        return Physics.Raycast(origin, Vector3.down, out RaycastHit _, groundRayLength, groundMask, QueryTriggerInteraction.Ignore);
    }

    float GetGroundY(out bool hitGround)
    {
        hitGround = false;

        Vector3 origin = transform.position + Vector3.up * 1.0f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, flyGroundProbe, groundMask, QueryTriggerInteraction.Ignore))
        {
            hitGround = true;
            return hit.point.y;
        }
        return transform.position.y;
    }

    float GetGroundYOrCurrent()
    {
        Vector3 origin = transform.position + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return transform.position.y;
    }

    bool HasCeiling(float up)
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(origin, Vector3.up, up + 0.2f, groundMask, QueryTriggerInteraction.Ignore);
    }

    void SetObstacleCollisionIgnored(bool ignore)
    {
        if (obstacleIgnored == ignore) return;
        obstacleIgnored = ignore;

        int playerLayer = gameObject.layer;

        // obstacleMask 지정 안 했으면 "Obstacle" 레이어만 처리
        if (obstacleMask.value == 0)
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer >= 0)
                Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, ignore);
            return;
        }

        // obstacleMask 지정했으면 해당 마스크의 레이어 전체 처리
        for (int layer = 0; layer < 32; layer++)
        {
            if ((obstacleMask.value & (1 << layer)) != 0)
                Physics.IgnoreLayerCollision(playerLayer, layer, ignore);
        }
    }

    IEnumerator ReenableObstacleCollisionAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        SetObstacleCollisionIgnored(false);
        flyGraceCo = null;
    }

    void Update()
    {
        if (GameManager.I != null && GameManager.I.State != GameState.Playing)
        {
            if (anim != null)
            {
                if (!string.IsNullOrEmpty(paramMoveLevel)) anim.SetFloat(paramMoveLevel, 0f);
                if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, true);
                if (!string.IsNullOrEmpty(paramIsFlying)) anim.SetBool(paramIsFlying, false);
            }
            isFlying = false;
            return;
        }

        if (input == null) return;
        if (anim != null && anim.applyRootMotion) anim.applyRootMotion = false;

        float dt = Time.deltaTime;

        // Ground는 Fly 중엔 false로 고정(애니 파라미터용)
        bool grounded = isFlying ? false : IsGroundedRay();
        isGroundedCached = grounded;

        // 중력/코요테
        if (!isFlying)
        {
            if (grounded)
            {
                coyoteTimer = coyoteTime;
                if (verticalVel < 0f) verticalVel = -2f;
            }
            else
            {
                coyoteTimer -= dt;
                verticalVel += gravity * dt;
            }
        }
        else
        {
            verticalVel = 0f;
            coyoteTimer = 0f;
        }

        // 입력 (Fly 중에는 Roll/Jump를 기능적으로 무시)
        bool rollHeld = (!isFlying) && input.RollHeld;
        bool rollStarted = rollHeld && !prevRollHeld;
        prevRollHeld = rollHeld;

        if (!isFlying && coyoteTimer > 0f && input.JumpTriggered && !rollHeld)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            coyoteTimer = 0f;

            if (anim != null && !string.IsNullOrEmpty(trigJump))
                anim.SetTrigger(trigJump);
        }

        // 레인 이동
        int lane = Mathf.Clamp(input.Lane, -1, 1);
        float targetX = lane * laneWidth;
        currentX = Mathf.Lerp(currentX, targetX, dt * laneMoveSpeed);

        // 전진 속도
        float r;

        // Fly 상태면 MoveLevel(런/워크) 무시하고, F키로만 전진
        if (isFlying)
        {
            r = input.FlyForward ? 1f : 0f;
        }
        else
        {
            r = Mathf.Clamp01(input.MoveLevel);
        }

        float targetSpeed =
            (r >= 0.75f) ? runSpeed :
            (r >= 0.25f) ? walkSpeed :
            stopSpeed;

        forwardSpeed = Mathf.Lerp(forwardSpeed, targetSpeed, dt * speedLerp);


        // ===== 충돌 캡슐 세팅 =====
        if (!isFlying)
        {
            float desiredHeight = rollHeld ? slideHeight : normalHeight;
            cc.height = Mathf.Lerp(cc.height, desiredHeight, dt * slideLerp);
            cc.center = new Vector3(0, cc.height * 0.5f, 0);

            if (rollStarted)
            {
                if (anim != null && !string.IsNullOrEmpty(trigRoll))
                    anim.SetTrigger(trigRoll);
            }
        }
        else
        {
            cc.height = normalHeight;
            cc.center = new Vector3(0, cc.height * 0.5f, 0);
        }

        // ===== 이동 =====
        Vector3 move = Vector3.zero;

        // x
        float dx = currentX - transform.position.x;
        move.x = Mathf.Clamp(dx, -laneMoveSpeed * dt, laneMoveSpeed * dt);

        // z
        move.z = forwardSpeed * dt;

        // y
        if (!isFlying)
        {
            move.y = verticalVel * dt;
        }
        else
        {
            float targetY = flyGroundY + flyLiftY;
            float diff = targetY - transform.position.y;

            float speed = diff > 0f ? flyUpSpeed : flyDownSpeed;
            float step = Mathf.Clamp(diff, -speed * dt, speed * dt);

            if (step > 0f) step = Mathf.Min(step, flyMaxUpPerFrame);
            if (step > 0f && HasCeiling(step)) step = 0f;

            move.y = step;
        }

        cc.Move(move);

        if (debugLogs && isFlying && Time.frameCount % 15 == 0)
        {
            bool hitGround;
            float gy = GetGroundY(out hitGround);
            float targetY = hitGround ? (gy + flyLiftY) : transform.position.y;
            Debug.Log($"[FLY] y={transform.position.y:F2} groundY={(hitGround ? gy : -999f):F2} lift={flyLiftY:F2} targetY={targetY:F2} ccCenterY={cc.center.y:F2} ccH={cc.height:F2}");
        }

        // 애니 파라미터
        if (anim != null)
        {
            if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, grounded);
            if (!string.IsNullOrEmpty(paramMoveLevel)) anim.SetFloat(paramMoveLevel, r);
            if (!string.IsNullOrEmpty(paramIsFlying)) anim.SetBool(paramIsFlying, isFlying);
        }
    }

    void LateUpdate()
    {
        if (visualRoot == null) return;
        visualRoot.localPosition = visualLocalPos0;
        visualRoot.localRotation = visualLocalRot0;
    }

    // ===== Public API for Wings =====

    public void SetFlying(bool on, float liftY = -1f)
    {
        if (on)
        {
            // 혹시 착지 후 복구 코루틴 돌고 있으면 끊기
            if (flyGraceCo != null) { StopCoroutine(flyGraceCo); flyGraceCo = null; }

            flyGroundY = GetGroundYOrCurrent();

            isFlying = true;
            verticalVel = 0f;
            coyoteTimer = 0f;

            if (liftY > 0f) flyLiftY = liftY;

            // ✅ 비행 중: 장애물 충돌 무시 + stepOffset 낮추기
            SetObstacleCollisionIgnored(true);
            if (cc != null) cc.stepOffset = Mathf.Min(flyStepOffset, 0.1f);

            SafeUntilTime = Mathf.Max(SafeUntilTime, Time.time + 0.1f);
        }
        else
        {
            isFlying = false;
            coyoteTimer = 0f;
            verticalVel = -2f;

            if (anim != null && !string.IsNullOrEmpty("Land")) anim.SetTrigger("Land");

            // ✅ 착지: stepOffset 원복
            if (cc != null) cc.stepOffset = defaultStepOffset;

            if (cc != null)
            {
                cc.enabled = false;
                transform.position = new Vector3(transform.position.x, flyGroundY, transform.position.z);
                cc.enabled = true;
            }
            SnapToGroundOnce();

            // 착지 직후 3초도 계속 무시 유지(바로 장애물 나오면 억까 방지)
            SafeUntilTime = Mathf.Max(SafeUntilTime, Time.time + 3.0f);

            if (flyGraceCo != null) StopCoroutine(flyGraceCo);
            flyGraceCo = StartCoroutine(ReenableObstacleCollisionAfter(3.0f));
        }
    }

    public void StartFlyingImmediate(float liftY = -1f, float immediateStepY = -1f, float graceSec = -1f)
    {
        // 기존 코루틴 정리
        if (flyGraceCo != null) { StopCoroutine(flyGraceCo); flyGraceCo = null; }

        if (liftY > 0f) flyLiftY = liftY;
        float stepY = immediateStepY > 0f ? immediateStepY : flyImmediateStepY;

        flyGroundY = GetGroundYOrCurrent();

        isFlying = true;
        verticalVel = 0f;
        coyoteTimer = 0f;

        // ✅ 즉시 비행도 동일하게: 장애물 무시 + stepOffset 낮추기
        SetObstacleCollisionIgnored(true);
        if (cc != null) cc.stepOffset = Mathf.Min(flyStepOffset, 0.1f);

        // 롤 상태든 뭐든 Fly 시작 순간 캡슐을 정상화(필수)
        cc.height = normalHeight;
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        // 같은 프레임 즉시 상승(천장 있으면 상승 안 함)
        if (stepY > 0f && !HasCeiling(stepY))
        {
            cc.enabled = false;
            transform.position += new Vector3(0f, stepY, 0f);
            cc.enabled = true;
        }

        if (anim != null && !string.IsNullOrEmpty(paramIsFlying))
            anim.SetBool(paramIsFlying, true);

        SafeUntilTime = Mathf.Max(SafeUntilTime, Time.time + 0.1f);
    }

    // ===== Utility =====

    public void ForceStopToIdle()
    {
        forwardSpeed = 0f;
        verticalVel = 0f;
        coyoteTimer = 0f;

        isFlying = false;

        // ✅ 혹시 남아있으면 원복
        SetObstacleCollisionIgnored(false);
        if (cc != null) cc.stepOffset = defaultStepOffset;

        cc.height = normalHeight;
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        if (cc != null)
            cc.Move(Vector3.zero);

        if (anim != null)
        {
            if (!string.IsNullOrEmpty(paramMoveLevel)) anim.SetFloat(paramMoveLevel, 0f);
            if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, true);
            if (!string.IsNullOrEmpty(paramIsFlying)) anim.SetBool(paramIsFlying, false);

            if (!string.IsNullOrEmpty(trigJump)) anim.ResetTrigger(trigJump);
            if (!string.IsNullOrEmpty(trigRoll)) anim.ResetTrigger(trigRoll);
        }
    }
}