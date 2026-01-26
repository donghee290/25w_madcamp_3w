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
    [Tooltip("비행 목표 높이 = flyBaseY + flyLiftY")]
    public float flyLiftY = 0f;
    public float flyLerp = 10f;
    public float flyMaxUpSpeed = 6f;

    [Header("Fly Collision")]
    [Tooltip("비행 중에는 슬라이드/센터 로직을 끄고, 이 센터를 유지합니다.")]
    public float flyColliderCenterY = 2.6f;

    [Header("Fly (Immediate Safety)")]
    [Tooltip("아이템을 먹는 순간, 같은 프레임에 위로 올려서 즉시 장애물 무력화")]
    public float flyImmediateStepY = 2.2f;

    [Tooltip("아이템 먹고 아주 짧은 시간 장애물 충돌 무시(죽는 판정 방지)")]
    public float flyGraceSec = 0.25f;

    [Tooltip("없어도 됨. 있으면 Obstacle 레이어 대신 이 마스크의 레이어들과만 충돌 무시를 적용할 수 있습니다(미구현).")]
    public LayerMask obstacleMask;

    private CharacterController cc;
    private float currentX;
    private float verticalVel;
    private bool prevRollHeld = false;

    private bool isGroundedCached;

    // ===== Fly runtime =====
    private bool isFlying = false;
    private float flyBaseY = 0f;
    private Coroutine flyGraceCo;

    public bool IsFlying => isFlying;
    public bool IsRolling => input != null && input.RollHeld && !isFlying; // Fly 중 롤 입력은 기능적으로 무시
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

    bool IsGroundedRay()
    {
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * groundRayStartUp;
        return Physics.Raycast(origin, Vector3.down, out RaycastHit _, groundRayLength, groundMask, QueryTriggerInteraction.Ignore);
    }

    void Update()
    {
        if (GameManager.I != null && GameManager.I.State != GameState.Playing)
        {
            // 정지 상태면 파라미터만 Idle로 고정 + Fly 해제
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

        // Ground는 Fly 중엔 false로 고정
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
                coyoteTimer -= Time.deltaTime;
                verticalVel += gravity * Time.deltaTime;
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

        // 점프(롤 중 점프 금지) - Fly 중 금지
        if (!isFlying && coyoteTimer > 0f && input.JumpTriggered && !rollHeld)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            coyoteTimer = 0f;

            if (anim != null && !string.IsNullOrEmpty(trigJump))
                anim.SetTrigger(trigJump);
        }

        // 레인 이동( Fly 중에도 가능 )
        int lane = Mathf.Clamp(input.Lane, -1, 1);
        float targetX = lane * laneWidth;
        currentX = Mathf.Lerp(currentX, targetX, Time.deltaTime * laneMoveSpeed);

        // 전진 속도 (WingsBuff에서 runSpeed/walkSpeed 올리면 자동 반영)
        float r = Mathf.Clamp01(input.MoveLevel);
        float targetSpeed =
            (r >= 0.75f) ? runSpeed :
            (r >= 0.25f) ? walkSpeed :
            stopSpeed;

        forwardSpeed = Mathf.Lerp(forwardSpeed, targetSpeed, Time.deltaTime * speedLerp);

        // ===== 충돌 캡슐 세팅 =====
        // (중요) Fly 중에는 슬라이드 로직이 cc.center를 덮어쓰지 못하도록 분기
        if (!isFlying)
        {
            float desiredHeight = rollHeld ? slideHeight : normalHeight;
            cc.height = Mathf.Lerp(cc.height, desiredHeight, Time.deltaTime * slideLerp);
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
            cc.center = new Vector3(0, flyColliderCenterY, 0);
        }

        // 이동 벡터
        Vector3 move = Vector3.zero;

        float dx = currentX - transform.position.x;
        move.x = Mathf.Clamp(dx, -laneMoveSpeed * Time.deltaTime, laneMoveSpeed * Time.deltaTime);

        move.z = forwardSpeed * Time.deltaTime;

        if (!isFlying)
        {
            move.y = verticalVel * Time.deltaTime;
        }
        else
        {
            float targetY = flyBaseY + flyLiftY;
            float dy = targetY - transform.position.y;

            float step = Mathf.Clamp(
                dy * flyLerp * Time.deltaTime,
                -flyMaxUpSpeed * Time.deltaTime,
                flyMaxUpSpeed * Time.deltaTime
            );
            move.y = step;
        }

        cc.Move(move);

        if (GameManager.I != null && GameManager.I.State != GameState.Playing)
        {
            ForceStopToIdle();
            return;
        }

        // 애니 파라미터
        if (anim != null)
        {
            if (!string.IsNullOrEmpty(paramIsGrounded))
                anim.SetBool(paramIsGrounded, grounded);

            if (!string.IsNullOrEmpty(paramMoveLevel))
                anim.SetFloat(paramMoveLevel, r);

            if (!string.IsNullOrEmpty(paramIsFlying))
                anim.SetBool(paramIsFlying, isFlying);
        }
    }

    void LateUpdate()
    {
        if (visualRoot == null) return;

        // Fly 중에는 비주얼 포지션 고정하지 말기 (뜬 연출이 안 보일 수 있음)
        if (isFlying) return;

        visualRoot.localPosition = visualLocalPos0;
        visualRoot.localRotation = visualLocalRot0;
    }

    // ===== Public API for Wings =====

    /// <summary>
    /// 일반적인 Fly 토글(즉시 상승/무적 윈도우 없음)
    /// </summary>
    public void SetFlying(bool on, float liftY = -1f)
    {
        if (on)
        {
            flyBaseY = transform.position.y;
            isFlying = true;
            verticalVel = 0f;
            coyoteTimer = 0f;

            if (liftY > 0f) flyLiftY = liftY;
        }
        else
        {
            isFlying = false;
            verticalVel = 0f;
            coyoteTimer = 0f;

            // 캡슐 원복은 Update에서 !isFlying 분기에서 자동 처리됨
        }

        if (anim != null && !string.IsNullOrEmpty(paramIsFlying))
            anim.SetBool(paramIsFlying, isFlying);
    }

    public void StartFlyingImmediate(float liftY = -1f, float immediateStepY = -1f, float graceSec = -1f)
    {
        // 값 기본 처리
        if (liftY > 0f) flyLiftY = liftY;
        float stepY = immediateStepY > 0f ? immediateStepY : flyImmediateStepY;
        float gSec = graceSec > 0f ? graceSec : flyGraceSec;

        // Fly ON
        flyBaseY = transform.position.y;
        isFlying = true;
        verticalVel = 0f;
        coyoteTimer = 0f;

        Debug.Log($"[FlyImmediate] BEFORE rootY={transform.position.y} stepY={stepY} liftY={flyLiftY}");

        // 슬라이드 상태였다면 즉시 정상화
        cc.height = normalHeight;
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        // 1) 같은 프레임에 즉시 위로 이동(장애물 바로 앞에서 먹어도 살아남게)
        cc.enabled = false;
        transform.position += new Vector3(0f, stepY, 0f);
        cc.enabled = true;

        Debug.Log($"[FlyImmediate] AFTER  rootY={transform.position.y}");

        // 즉시 상승이 반영된 위치를 Fly 기준으로 다시 설정 (중요)
        flyBaseY = transform.position.y;

        // 2) 애니 전환(이동 이후)
        if (anim != null && !string.IsNullOrEmpty(paramIsFlying))
            anim.SetBool(paramIsFlying, true);

        // 3) 짧은 무력화(레이어 충돌 무시)
        if (gSec > 0f)
        {
            if (flyGraceCo != null) StopCoroutine(flyGraceCo);
            flyGraceCo = StartCoroutine(FlyGraceCoroutine(gSec));
        }
    }

    IEnumerator FlyGraceCoroutine(float sec)
    {
        int playerLayer = gameObject.layer;
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");

        if (obstacleLayer >= 0)
        {
            Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, true);
            yield return new WaitForSeconds(sec);
            Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, false);
        }
        else
        {
            yield return new WaitForSeconds(sec);
        }

        flyGraceCo = null;
    }

    float GetGroundYOrCurrent()
    {
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * 2f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return transform.position.y;
    }

    // ===== Utility =====

    public void ForceStopToIdle()
    {
        forwardSpeed = 0f;
        verticalVel = 0f;
        coyoteTimer = 0f;

        isFlying = false;

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