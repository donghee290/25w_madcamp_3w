using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Input (required)")]
    public MonoBehaviour inputSource;
    private IPlayerInput input;

    [Header("Sprint Boost (when running hard)")]
    public float sprintBonusSpeed = 10f;      // 추가 최고속 (runSpeed에 더해짐)
    public float sprintBuildPerSec = 2.5f;    // 빨리 달릴 때 차는 속도
    public float sprintDecayPerSec = 3.5f;    // 멈추면 빠지는 속도

    private float sprint01 = 0f;              // 0~1

    [Header("Animator (required)")]
    public Animator anim;

    [Header("UI Debug")]
    public TextMeshProUGUI debugText;

    [Header("Animator Params (only existing ones)")]
    public string paramMoveLevel = "MoveLevel";
    public string paramIsGrounded = "IsGrounded";
    public string paramIsFlying = "IsFlying";
    public string trigJump = "Jump";
    public string trigRoll = "Roll";
    public string trigLand = "Land";   // Animator에 Land 트리거가 있으면 이 이름 그대로
    private float _savedAnimSpeed = 1f;
    private Coroutine _freezeCo;

    [Header("Lane")]
    public float laneWidth = 1.2f;
    public float laneMoveSpeed = 14f;

    [Header("Forward Acceleration (Physical feel)")]
    public float minAccel = 6f;      // r=0 근처 가속 (느리게)
    public float maxAccel = 22f;     // r=1 근처 가속 (빠르게)
    public float brakeAccel = 30f;   // 감속 속도 (멈출 때)


    [Header("Forward Speeds")]
    public float runSpeed = 10f;
    public float walkSpeed = 6f;
    public float stopSpeed = 0f;

    [Tooltip("forwardSpeed가 targetSpeed를 따라가는 속도")]
    public float speedLerp = 8f;

    [Tooltip("현재 전진 속도(디버그/표시용)")]
    public float forwardSpeed = 0f;

    [Header("Jump")]
    public float jumpHeight = 2.5f;
    public float gravity = -15f;

    [Header("Jump Feel")]
    [Tooltip("하강 중 중력 배수(체공 줄이고 쫀득하게). 오래 떠있게 하고 싶으면 1.0~1.2")]
    public float fallGravityMultiplier = 1.0f;

    [Header("Roll/Slide")]
    public float normalHeight = 1.8f;
    public float slideHeight = 0.9f;
    public float slideLerp = 20f;

    [Header("Ground Check (Ray debug only)")]
    public float groundRayLength = 0.6f;
    public float groundRayStartUp = 0.05f;

    [Header("Coyote Time")]
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
    public float flyGroundProbe = 10.0f;

    [Header("Fly Ceiling Clamp")]
    [Tooltip("천장에 붙지 않게 여유값(미터)")]
    public float flyCeilingMargin = 0.35f;

    [Header("Fly (Immediate Safety)")]
    public float flyGraceSec = 0.6f;

    [Tooltip("없어도 됨. 있으면 Obstacle 레이어 대신 이 마스크의 레이어들과만 충돌 무시를 적용할 수 있습니다.")]
    public LayerMask obstacleMask;

    [Header("StepOffset Patch (anti tiny lift)")]
    public float flyStepOffset = 0.05f;

    [Header("Masks (IMPORTANT)")]
    [Tooltip("바닥만 포함 (예: Default 또는 Ground). 천장/장애물 레이어 절대 포함 X")]
    public LayerMask groundProbeMask;



    [Tooltip("천장/상단 충돌용 (예: Ceiling 레이어만)")]
    public LayerMask ceilingMask;

    private CharacterController cc;
    private float currentX;
    private float verticalVel;
    private bool prevRollHeld = false;

    private bool isGroundedCached;

    // ===== Fly runtime =====
    private bool isFlying = false;
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
    private bool _snappedOnCountdown = false;


    void Awake()
    {
        cc = GetComponent<CharacterController>();
        defaultStepOffset = cc.stepOffset;

        ResolveInput();
        ResolveAnimator();

        cc.height = normalHeight;
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        currentX = transform.position.x;
        verticalVel = 0f;

        // groundProbeMask가 비어있으면 Default로 자동 보정(응급)
        if (groundProbeMask.value == 0)
        {
            int defaultLayer = LayerMask.NameToLayer("Default");
            groundProbeMask = (1 << defaultLayer);
            Debug.LogWarning("[PlayerMotor] groundProbeMask was Nothing. Auto-set to Default.");
        }

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

    // ===== Ground helpers =====

    void SnapToGroundOnce()
    {
        if (cc == null) cc = GetComponent<CharacterController>();

        float curY = transform.position.y;

        // 위에서 아래로 쏴서 "진짜 바닥"만 찾기
        Vector3 origin = transform.position + Vector3.up * 2f;

        // RaycastAll로 여러 개 맞춰서, 그 중 "바닥 조건"을 만족하는 것만 고름
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 30f, groundProbeMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        // 가까운 순으로 정렬
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];

            // ✅ 1) 위를 향한 면만 바닥으로 인정
            if (hit.normal.y < 0.6f) continue;

            // ✅ 2) "바닥"이 현재보다 위로 나오면 무시(천장/벽 오인 방지)
            if (hit.point.y > curY + 0.5f) continue;

            // 여기까지 통과하면 진짜 바닥 후보
            cc.enabled = false;
            transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            cc.enabled = true;
            return;
        }

        // 통과하는 바닥이 없으면 아무 것도 안 함
    }


    Vector3 GetFootWorldPos()
    {
        float half = cc.height * 0.5f;
        float footOffset = half - cc.radius;
        return transform.position + cc.center - Vector3.up * footOffset;
    }

    // 디버그용
    bool IsGroundedRay()
    {
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * groundRayStartUp;
        return Physics.Raycast(origin, Vector3.down, out _, groundRayLength, groundProbeMask, QueryTriggerInteraction.Ignore);
    }

    float GetGroundYOrCurrent()
    {
        // ✅ "바닥"은 현재 y보다 위일 수 없다고 가정(천장/벽 오인 방지용 보정)
        float curY = transform.position.y;

        Vector3 origin = transform.position + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f, groundProbeMask, QueryTriggerInteraction.Ignore))
        {
            // 바닥이 현재보다 위로 나오면(이상) 무시하고 현재 y 사용
            if (hit.point.y > curY + 0.5f)
                return curY;

            return hit.point.y;
        }
        return curY;
    }

    float FindGroundYForLanding()
    {
        // 1) groundProbeMask로 먼저 시도 (정석)
        {
            Vector3 origin = transform.position + Vector3.up * 2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f,
                                 groundProbeMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }
        }

        // 2) 그래도 못 찾으면: 전체 레이어에서 바닥 후보 탐색
        {
            Vector3 origin = transform.position + Vector3.up * 2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f,
                                 ~0, QueryTriggerInteraction.Ignore))
            {
                int hitLayerMask = 1 << hit.collider.gameObject.layer;
                bool isCeiling =
                    (ceilingMask.value != 0) &&
                    ((ceilingMask.value & hitLayerMask) != 0);

                // 위를 향한 면만 바닥으로 인정
                if (!isCeiling && hit.normal.y > 0.6f)
                    return hit.point.y;
            }
        }

        // 3) 최후 안전장치: 현재 위치보다 아래로 강제
        return transform.position.y - 2f;
    }


    bool HasCeiling(float up)
    {
        if (ceilingMask.value == 0) return false;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(origin, Vector3.up, up + 0.2f, ceilingMask, QueryTriggerInteraction.Ignore);
    }

    float ClampTargetYByCeiling(float targetY)
    {
        if (ceilingMask.value == 0) return targetY;

        // 머리 위쪽에서 천장까지 거리 재서 상한선 잡기
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(origin, Vector3.up, out RaycastHit ceilHit, 50f, ceilingMask, QueryTriggerInteraction.Ignore))
        {
            float ceilingY = ceilHit.point.y;
            targetY = Mathf.Min(targetY, ceilingY - flyCeilingMargin);
        }
        return targetY;
    }

    // ===== Obstacle collision ignore =====

    void SetObstacleCollisionIgnored(bool ignore)
    {
        if (obstacleIgnored == ignore) return;
        obstacleIgnored = ignore;

        int playerLayer = gameObject.layer;

        if (obstacleMask.value == 0)
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer >= 0)
                Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, ignore);
            return;
        }

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

    void OnValidate()
    {
        if (jumpHeight < 0.1f) jumpHeight = 0.1f;
        if (gravity > -0.1f) gravity = -0.1f;

        if (groundProbeMask.value == 0)
        {
            int defaultLayer = LayerMask.NameToLayer("Default");
            groundProbeMask = (1 << defaultLayer);
        }
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

        bool grounded = isFlying ? false : cc.isGrounded;
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
                float g = gravity;
                if (verticalVel < 0f) g *= Mathf.Max(1f, fallGravityMultiplier);
                verticalVel += g * dt;
            }
        }
        else
        {
            verticalVel = 0f;
            coyoteTimer = 0f;
        }

        // 입력
        bool rollHeld = (!isFlying) && input.RollHeld;
        bool rollStarted = rollHeld && !prevRollHeld;
        prevRollHeld = rollHeld;

        // 점프
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

        float r = isFlying ? (input.FlyForward ? 1f : 0f) : Mathf.Clamp01(input.MoveLevel);

        // ✅ 목표 속도: 연속값 (걷기~달리기)
        float targetSpeed = Mathf.Lerp(walkSpeed, runSpeed, r);

        // ✅ "거의 가속 없음" : 18~25 사이면 거의 즉각
        forwardSpeed = Mathf.Lerp(forwardSpeed, targetSpeed, 20f * dt);

        // 스프린트 기능은 일단 끄기(이상한 가속의 주범)
        sprint01 = 0f;





        // ===== 애니용 MoveLevel (RUN 훨씬 쉽게) =====

        // 1️⃣ 속도 기반 (기존 로직)
        float speedBased01 = 0f;
        if (runSpeed > 0.01f)
            speedBased01 = Mathf.InverseLerp(walkSpeed, runSpeed, forwardSpeed);
        speedBased01 = Mathf.Clamp01(speedBased01);

        // 2️⃣ 입력 기반 (PoseInput에서 온 r)
        float inputBased01 = Mathf.Clamp01(r);

        // 3️⃣ "뛰고 있으면 무조건 뛰어라"
        float animMove = Mathf.Max(speedBased01, inputBased01);

        // 4️⃣ 스프린트 중이면 Run 애니 고정 (체감 핵심)
        if (sprint01 > 0.2f)
            animMove = Mathf.Max(animMove, 0.85f);

        animMove = Mathf.Clamp01(animMove);



        // 캡슐 세팅
        if (!isFlying)
        {
            float desiredHeight = rollHeld ? slideHeight : normalHeight;
            cc.height = Mathf.Lerp(cc.height, desiredHeight, dt * slideLerp);
            cc.center = new Vector3(0, cc.height * 0.5f, 0);

            if (rollStarted && anim != null && !string.IsNullOrEmpty(trigRoll))
                anim.SetTrigger(trigRoll);
        }
        else
        {
            cc.height = normalHeight;
            cc.center = new Vector3(0, cc.height * 0.5f, 0);
        }

        // 이동 벡터
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
            targetY = ClampTargetYByCeiling(targetY); // ✅ 천장 아래로 강제 제한

            float diff = targetY - transform.position.y;

            float speed = diff > 0f ? flyUpSpeed : flyDownSpeed;
            float step = Mathf.Clamp(diff, -speed * dt, speed * dt);

            if (step > 0f) step = Mathf.Min(step, flyMaxUpPerFrame);
            if (step > 0f && HasCeiling(step)) step = 0f;

            move.y = step;
        }

        cc.Move(move);

        // 애니 파라미터
        if (anim != null)
        {
            if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, grounded);
            if (!string.IsNullOrEmpty(paramMoveLevel)) anim.SetFloat(paramMoveLevel, animMove);
            if (!string.IsNullOrEmpty(paramIsFlying)) anim.SetBool(paramIsFlying, isFlying);
        }

        if (debugLogs && Time.frameCount % 30 == 0)
        {
            bool rayG = IsGroundedRay();
            Debug.Log($"[Motor] grounded(cc)={grounded} groundedRay={rayG} y={transform.position.y:F2} vY={verticalVel:F2} fly={isFlying} flyGroundY={flyGroundY:F2}");
        }


        if (debugText != null)
        {
            debugText.text =
                $"r(MoveLevel): {r:0.00}\n" +
                $"speed: {forwardSpeed:0.00}\n" +
                $"lane: {input.Lane}\n" +
                $"grounded: {isGroundedCached}";
        }

    }

    void LateUpdate()
    {
        if (visualRoot == null) return;
        visualRoot.localPosition = visualLocalPos0;
        visualRoot.localRotation = visualLocalRot0;
    }

    // ✅ 기존 호출부(인수 3개)를 살리기 위한 오버로드
    public void StartFlyingImmediate(float liftY = -1f, float immediateStepY = -1f, float graceSec = -1f)
    {
        if (graceSec > 0f) flyGraceSec = graceSec;
        StartFlyingImmediate(liftY);
    }

    // ===== Public API for Wings =====

    public void SetFlying(bool on, float liftY = -1f)
    {
        if (on)
        {
            if (flyGraceCo != null) { StopCoroutine(flyGraceCo); flyGraceCo = null; }

            flyGroundY = GetGroundYOrCurrent();

            isFlying = true;
            verticalVel = 0f;
            coyoteTimer = 0f;

            if (liftY > 0f) flyLiftY = liftY;

            SetObstacleCollisionIgnored(true);
            if (cc != null) cc.stepOffset = Mathf.Min(flyStepOffset, 0.1f);

            SafeUntilTime = Mathf.Max(SafeUntilTime, Time.time + 0.1f);
        }
        else
        {
            isFlying = false;
            coyoteTimer = 0f;
            verticalVel = -2f;

            if (anim != null) anim.SetTrigger("Land");

            if (cc != null) cc.stepOffset = defaultStepOffset;




            // ✅ 착지: 현재 위치 기준으로 바닥 재측정

            float groundY = FindGroundYForLanding();
            Debug.Log($"[LANDDBG] yBefore={transform.position.y:F2}, groundY={groundY:F2}");
            if (groundY > transform.position.y)
                groundY = transform.position.y - 2f;

            if (cc != null)
            {
                cc.enabled = false;
                transform.position = new Vector3(transform.position.x, groundY + 0.02f, transform.position.z);
                cc.enabled = true;
            }

            SnapToGroundOnce();

            SafeUntilTime = Mathf.Max(SafeUntilTime, Time.time + 3.0f);

            if (flyGraceCo != null) StopCoroutine(flyGraceCo);
            flyGraceCo = StartCoroutine(ReenableObstacleCollisionAfter(3.0f));
        }
    }

    public void StartFlyingImmediate(float liftY = -1f)
    {
        if (flyGraceCo != null) { StopCoroutine(flyGraceCo); flyGraceCo = null; }

        if (liftY > 0f) flyLiftY = liftY;

        flyGroundY = GetGroundYOrCurrent();

        isFlying = true;
        verticalVel = 0f;
        coyoteTimer = 0f;

        SetObstacleCollisionIgnored(true);
        if (cc != null) cc.stepOffset = Mathf.Min(flyStepOffset, 0.1f);

        cc.height = normalHeight;
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        if (anim != null && !string.IsNullOrEmpty(paramIsFlying))
            anim.SetBool(paramIsFlying, true);

        SafeUntilTime = Mathf.Max(SafeUntilTime, Time.time + 0.1f);
    }

    IEnumerator CoFreezeAnimatorAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        if (anim != null) anim.speed = 0f;   // ✅ 포즈 고정
        _freezeCo = null;
    }


    public void StartOnGroundForCountdown()
    {
        // 상태/속도 리셋
        forwardSpeed = 0f;
        verticalVel = 0f;
        coyoteTimer = 0f;

        isFlying = false;

        // 충돌무시/stepOffset 원복
        SetObstacleCollisionIgnored(false);
        if (cc != null) cc.stepOffset = defaultStepOffset;

        // ✅ 핵심: 시작부터 바닥에 딱 붙이기
        SnapToGroundOnce();

        // CharacterController가 첫 프레임 grounded를 놓치는 경우가 있어서
        // 아주 살짝 아래로 눌러서 안정화 (값 너무 크면 계단 내려가는 느낌 나니 0.02 권장)
        if (cc != null)
            cc.Move(Vector3.down * 0.02f);


        if (debugLogs)
            Debug.Log($"[StartOnGround] y={transform.position.y:F2} grounded(cc)={cc.isGrounded}");

        // 애니 파라미터도 "정지 상태"로
        if (anim != null)
        {
            if (!string.IsNullOrEmpty(paramMoveLevel)) anim.SetFloat(paramMoveLevel, 0f);
            if (!string.IsNullOrEmpty(paramIsGrounded)) anim.SetBool(paramIsGrounded, true);
            if (!string.IsNullOrEmpty(paramIsFlying)) anim.SetBool(paramIsFlying, false);
        }
    }


    // ===== Utility =====

    public void ForceStopToIdle()
    {
        forwardSpeed = 0f;
        verticalVel = 0f;
        coyoteTimer = 0f;

        isFlying = false;

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
