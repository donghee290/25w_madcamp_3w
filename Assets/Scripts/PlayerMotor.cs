using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    public PlayerPoseInput input;

    [Header("Lane")]
    public float laneWidth = 1.2f;
    public float laneMoveSpeed = 14f;

    [Header("Forward")]
    public float forwardSpeed = 6f;
    public float accelPerSec = 0.25f;
    public float maxForwardSpeed = 14f;

    [Header("Jump")]
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Roll/Slide")]
    public float normalHeight = 1.8f;
    public float slideHeight = 0.9f;
    public float slideLerp = 20f;

    private CharacterController cc;
    private Vector3 velocity;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!input) input = GetComponent<PlayerPoseInput>();
    }

    void Update()
    {
        if (!input) return;

        // 0) 전진 속도 가속
        forwardSpeed = Mathf.Min(maxForwardSpeed, forwardSpeed + accelPerSec * Time.deltaTime);

        // 1) 레인 이동 (스냅 목표 + 부드럽게)
        float targetX = input.Lane * laneWidth;
        Vector3 pos = transform.position;
        pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * laneMoveSpeed);

        // 2) 점프/중력
        if (cc.isGrounded && velocity.y < 0) velocity.y = -1f;

        if (cc.isGrounded && input.JumpTriggered && !input.SlideHeld)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;

        // 3) 롤/슬라이드 (키 낮추기)
        float desiredHeight = input.SlideHeld ? slideHeight : normalHeight;
        cc.height = Mathf.Lerp(cc.height, desiredHeight, Time.deltaTime * slideLerp);
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        // 4) 이동 적용
        Vector3 laneDelta = new Vector3(pos.x - transform.position.x, 0, 0);
        Vector3 forward = Vector3.forward * forwardSpeed;
        Vector3 vertical = Vector3.up * velocity.y;

        cc.Move((laneDelta + forward + vertical) * Time.deltaTime);

        // pos.x를 확정(시각적으로 안정)
        transform.position = new Vector3(pos.x, transform.position.y, transform.position.z);
    }
}
