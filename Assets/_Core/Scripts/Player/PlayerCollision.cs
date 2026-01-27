using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerCollision : MonoBehaviour
{
    public string obstacleTag = "Obstacle";
    public bool debugLogs = true;

    [Header("Ceiling ignore while flying")]
    [Tooltip("hit.normal.y가 이 값보다 작으면(대개 -1 근처) 천장으로 보고 무시합니다.")]
    public float ceilingNormalThreshold = -0.5f;

    [Tooltip("콜라이더 이름에 이 문자열이 포함되면 천장으로 보고 무시합니다.")]
    public string ceilingNameContains = "Ceiling";

    private bool dead = false;
    private PlayerMotor motor;
    private CharacterController cc;

    private bool wasFlying = false;
    private readonly HashSet<Collider> ignoredWhileFlying = new HashSet<Collider>();

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        motor = GetComponent<PlayerMotor>();
        if (motor == null) motor = GetComponentInParent<PlayerMotor>();

        if (debugLogs)
            Debug.Log($"[PlayerCollision] on={name} motor={(motor ? motor.name : "NULL")}");
    }

    void Update()
    {
        bool flying = (motor != null && motor.IsFlying);

        // 비행이 끝났으면, 비행 중 무시했던 충돌을 전부 원복
        if (wasFlying && !flying)
        {
            RestoreIgnoredCollisions();
        }

        wasFlying = flying;
    }

    void OnDisable()
    {
        // 씬 전환/비활성화 시에도 원복 안전장치
        RestoreIgnoredCollisions();
    }

    void RestoreIgnoredCollisions()
    {
        if (cc == null) return;

        foreach (var col in ignoredWhileFlying)
        {
            if (col != null)
                Physics.IgnoreCollision(cc, col, false);
        }
        ignoredWhileFlying.Clear();
    }

    bool IsCeilingHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.collider == null) return false;

        // 1) 법선으로 판정: 아래에서 위를 치면 normal.y가 -1 쪽으로 떨어짐
        if (hit.normal.y <= ceilingNormalThreshold) return true;

        // 2) 이름 포함으로 보조 판정
        if (!string.IsNullOrEmpty(ceilingNameContains) &&
            hit.collider.name.Contains(ceilingNameContains))
            return true;

        return false;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (dead) return;
        if (GameManager.I == null) return;
        if (GameManager.I.State != GameState.Playing) return;

        bool flying = (motor != null && motor.IsFlying);

        if (debugLogs && hit != null && hit.collider != null)
        {
            Debug.Log($"[Hit] flying={flying} hit={hit.collider.name} tag={hit.collider.tag} layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} normal={hit.normal}");
        }

        // Fly 중이면:
        // - "천장"으로 판정되는 충돌은 아예 IgnoreCollision 걸어서 스팸 히트/밀림을 끊는다.
        // - 장애물 판정은 기존대로 무시(flying이면 죽지 않게)
        if (flying)
        {
            if (cc != null && hit != null && hit.collider != null && IsCeilingHit(hit))
            {
                if (!ignoredWhileFlying.Contains(hit.collider))
                {
                    Physics.IgnoreCollision(cc, hit.collider, true);
                    ignoredWhileFlying.Add(hit.collider);

                    if (debugLogs)
                        Debug.Log($"[FlyIgnore] IgnoreCollision ON -> {hit.collider.name}");
                }
            }
            return;
        }

        // Fly 아닐 때만 장애물 태그로 사망 처리
        if (hit.collider != null && hit.collider.CompareTag(obstacleTag))
        {
            dead = true;
            GameManager.I.GameOver(GameOverReason.HitObstacle);
        }
    }
}