using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Input (KeyboardInput or PoseInput)")]
    public MonoBehaviour inputSource;
    private IPlayerInput input;

    [Header("Lane")]
    public float laneWidth = 1.2f;
    public float laneMoveSpeed = 14f;

    [Header("Forward")]
    public float forwardSpeed = 6f;
    public float accelPerSec = 0.25f;
    public float maxForwardSpeed = 14f;

    [Header("Jump")]
    public float jumpHeight = 2.5f;
    public float gravity = -15f;

    [Header("Roll/Slide")]
    public float normalHeight = 1.8f;
    public float slideHeight = 0.9f;
    public float slideLerp = 20f;

    [Header("Ground Check (IMPORTANT)")]
    [Tooltip("발 밑에서 아래로 쏘는 레이 길이(지면이 안 잡히면 0.4~1.0으로 올려)")]
    public float groundRayLength = 0.6f;

    [Tooltip("레이 시작점을 발 밑에서 위로 살짝 올리는 값")]
    public float groundRayStartUp = 0.05f;

    [Tooltip("바닥으로 인식할 레이어. 기본 Everything")]
    public LayerMask groundMask = ~0;

    [Tooltip("지면 판정이 튀어도 점프되는 유예 시간(코요테 타임)")]
    public float coyoteTime = 0.12f;

    private float coyoteTimer = 0f;

    private CharacterController cc;
    private float currentX;
    private float verticalVel; // y 속도(m/s)

    public float CurrentForwardSpeed => forwardSpeed;

    public bool IsRolling => input != null && input.RollHeld;
    public bool IsAirborne => !IsGrounded();

    public bool debugLogs = true;

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        // input 연결
        if (inputSource != null) input = inputSource as IPlayerInput;
        if (input == null)
        {
            var kb = GetComponent<KeyboardInput>();
            if (kb != null) input = kb;
        }

        // CC 기본 세팅
        cc.height = normalHeight;
        cc.center = new Vector3(0, 0, 0);

        currentX = transform.position.x;
        verticalVel = 0f;

        // 시작 위치를 바닥에 한 번 붙임(선택)
        SnapToGroundOnce();
        Debug.Log($"[PlayerMotor] inputSource={inputSource} inputIsNull={input == null}");

    }

    void SnapToGroundOnce()
    {
        // "발 위치"를 계산해서 거기서 아래로 쏴서 바닥에 맞춘다
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * 2f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
        {
            // 발이 바닥에 닿게 하려면 transform.y를 hit에 맞춰야 함
            // foot.y 가 transform.position.y에 의해 결정되므로, delta만큼 올림/내림
            float delta = hit.point.y - foot.y;
            transform.position += new Vector3(0, delta, 0);
        }
    }

    Vector3 GetFootWorldPos()
    {
        // CharacterController 발 위치(월드) 계산:
        // transform.position + center - up*(height/2 - radius)
        float half = cc.height * 0.5f;
        float footOffset = half - cc.radius;

        return transform.position + cc.center - Vector3.up * footOffset;
    }

    bool IsGrounded()
    {
        // cc.isGrounded는 환경에 따라 튈 수 있으니 레이캐스트로 확정
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * groundRayStartUp;

        bool hit = Physics.Raycast(origin, Vector3.down, out RaycastHit _, groundRayLength, groundMask, QueryTriggerInteraction.Ignore);

        // (디버그) Scene 뷰에서 확인하고 싶으면 Gizmos로도 그릴 수 있음
        return hit;
    }

    void Update()
    {
        if (input == null) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            verticalVel = 10f;
            Debug.Log("SPACE TEST JUMP");
        }


        // 속도 가속
        forwardSpeed = Mathf.Min(maxForwardSpeed, forwardSpeed + accelPerSec * Time.deltaTime);

        // 레인 이동 목표 x
        float targetX = input.Lane * laneWidth;
        currentX = Mathf.Lerp(currentX, targetX, Time.deltaTime * laneMoveSpeed);

        // grounded + 코요테 타임
        bool grounded = IsGrounded();

        if (grounded)
        {
            coyoteTimer = coyoteTime;

            // 지면에 붙어있게 약간 음수로 유지 (중요)
            if (verticalVel < 0f) verticalVel = -2f;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
            verticalVel += gravity * Time.deltaTime;
        }

        // 점프 (코요테 타임 포함)
        if (coyoteTimer > 0f && input.JumpTriggered && !input.RollHeld)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            coyoteTimer = 0f;
        }

        // 슬라이드 높이/센터
        float desiredHeight = input.RollHeld ? slideHeight : normalHeight;
        cc.height = Mathf.Lerp(cc.height, desiredHeight, Time.deltaTime * slideLerp);
        cc.center = new Vector3(0, 0, 0);

        // Move는 "이번 프레임 이동 거리"
        Vector3 move = Vector3.zero;

        // x는 목표 x로 보정(거리)
        move.x = (currentX - transform.position.x);

        // z 전진(거리)
        move.z = forwardSpeed * Time.deltaTime;

        // y 수직(거리)
        move.y = verticalVel * Time.deltaTime;

        cc.Move(move);

        if (debugLogs && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[PlayerMotor] grounded={grounded} jumpTrig={input.JumpTriggered} roll={input.RollHeld} y={transform.position.y:0.00} vY={verticalVel:0.00}");
        }
    }
}
