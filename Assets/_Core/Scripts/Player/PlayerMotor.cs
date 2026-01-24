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

    [Header("Roll / Slide")]
    public float normalHeight = 1.8f;
    public float slideHeight = 0.9f;
    public float slideLerp = 20f;
    public float rollDuration = 1.2f;

    [Header("Ground Check")]
    public float groundRayLength = 1.0f;
    public float groundRayStartUp = 0.2f;
    public LayerMask groundMask = ~0;
    public float coyoteTime = 0.12f;

    private float coyoteTimer;

    [Header("Visual")]
    public Transform visual;

    private CharacterController cc;
    private float currentX;
    private float verticalVel;
    public float CurrentForwardSpeed => forwardSpeed;
    public bool IsRollingNow => rollActive;

    // 상태 제어
    private bool jumpLock;
    private bool rollActive;
    private float rollTimer;
    private bool prevRollHeld;

    public bool IsRolling => rollActive;
    public bool IsAirborne => !IsGrounded();

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        if (inputSource != null) input = inputSource as IPlayerInput;
        if (input == null) input = GetComponent<KeyboardInput>();

        cc.height = normalHeight;
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        currentX = transform.position.x;
        verticalVel = 0f;

        SnapToGroundOnce();

        Debug.Log($"[PlayerMotor] input connected = {input != null}");
    }

    void SnapToGroundOnce()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down,
            out RaycastHit hit, 10f, groundMask))
        {
            transform.position = new Vector3(
                transform.position.x,
                hit.point.y,
                transform.position.z
            );
        }
    }

    Vector3 GetFootWorldPos()
    {
        return transform.position + cc.center - Vector3.up * (cc.height * 0.5f);
    }

    bool IsGrounded()
    {
        Vector3 foot = GetFootWorldPos();
        Vector3 origin = foot + Vector3.up * groundRayStartUp;

        return Physics.Raycast(
            origin,
            Vector3.down,
            groundRayLength,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    void Update()
    {
        if (input == null) return;

        // ───────────────── Forward
        forwardSpeed = Mathf.Min(
            maxForwardSpeed,
            forwardSpeed + accelPerSec * Time.deltaTime
        );

        // ───────────────── Lane
        float targetX = input.Lane * laneWidth;
        currentX = Mathf.Lerp(currentX, targetX, laneMoveSpeed * Time.deltaTime);

        // ───────────────── Ground / Gravity
        bool grounded = IsGrounded();

        if (grounded)
        {
            coyoteTimer = coyoteTime;
            jumpLock = false;

            if (verticalVel < 0f)
                verticalVel = -2f;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
            verticalVel += gravity * Time.deltaTime;
        }



        // ───────────────── Roll (edge-trigger)
        // input.RollHeld 대신 "키보드 눌림"을 직접 사용 (MVP 안정화용)
        bool rollPressed = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);

        if (!rollActive)
        {
            if (rollPressed && (grounded || coyoteTimer > 0f) && !jumpLock)
            {
                rollActive = true;
                rollTimer = rollDuration;
                Debug.Log("ROLL START");
            }
        }
        else
        {
            rollTimer -= Time.deltaTime;
            if (rollTimer <= 0f)
            {
                rollActive = false;
                Debug.Log("ROLL END");
            }
        }
        if (rollActive && Time.frameCount % 10 == 0)
            Debug.Log($"rolling... height={cc.height:0.00} centerY={cc.center.y:0.00}");





        // ───────────────── Jump
        if (!rollActive && !jumpLock && coyoteTimer > 0f && input.JumpTriggered)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            coyoteTimer = 0f;
            jumpLock = true;
        }

        // ───────────────── Capsule height
        float desiredHeight = rollActive ? slideHeight : normalHeight;
        cc.height = Mathf.Lerp(cc.height, desiredHeight, slideLerp * Time.deltaTime);
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        // ✅✅✅ 여기 바로 아래에 붙이면 됨 (연출용)
        if (visual != null)
        {
            float targetYScale = rollActive ? 0.5f : 1f;
            Vector3 s = visual.localScale;
            s.y = Mathf.Lerp(s.y, targetYScale, Time.deltaTime * 12f);
            visual.localScale = s;

            // 시각적으로 바닥에 붙게(스케일 줄이면 중심도 내려가니까)
            Vector3 p = visual.localPosition;
            float baseY = cc.height * 0.5f; // 보통 Visual 기본 Y
            float rolledY = baseY * targetYScale;
            p.y = Mathf.Lerp(p.y, rollActive ? rolledY : baseY, Time.deltaTime * 12f);
            visual.localPosition = p;
        }

        // ───────────────── Move
        Vector3 move;
        move.x = currentX - transform.position.x;
        move.z = forwardSpeed * Time.deltaTime;
        move.y = verticalVel * Time.deltaTime;

        cc.Move(move);
    }
}
