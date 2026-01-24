using UnityEngine;

public class PlayerCollisionTrigger : MonoBehaviour
{
    [Header("Settings")]
    public string obstacleTag = "Obstacle";

    [Header("Refs (Auto-find if null)")]
    public PlayerMotor motor;            // 부모에서 찾음
    public RunChaserSystem chaserSystem; // 부모에서 찾음 (최종 판사)

    private bool dead = false;

    void Awake()
    {
        if (motor == null)
            motor = GetComponentInParent<PlayerMotor>();

        if (chaserSystem == null)
            chaserSystem = GetComponentInParent<RunChaserSystem>();

        if (motor == null)
            Debug.LogError("[PlayerCollisionTrigger] PlayerMotor NOT found!");

        if (chaserSystem == null)
            Debug.LogError("[PlayerCollisionTrigger] RunChaserSystem NOT found!");
    }

    void OnCollisionEnter(Collision other)
    {
        if (dead) return;

        // 자기 자신/같은 루트 충돌 무시
        if (other.transform.root == transform.root) return;

        // Tag 확인
        if (!other.gameObject.CompareTag(obstacleTag))
            return;

        // 장애물 타입 가져오기 (없으면 기본: 죽음)
        var marker = other.gameObject.GetComponent<ObstacleMarker>();
        if (marker == null)
        {
            ForceDieHit("HitObstacle(NoMarker)");
            return;
        }

        bool rolling = motor != null && motor.IsRolling;
        bool airborne = motor != null && motor.IsAirborne;

        // 타입별 판정
        switch (marker.type)
        {
            case ObstacleType.DeskJump:
                // 점프 중이면 통과, 아니면 사망
                if (!airborne) ForceDieHit("HitObstacle(DeskJump)");
                break;

            case ObstacleType.BannerRoll:
                // 롤 중이면 통과, 아니면 사망
                if (!rolling) ForceDieHit("HitObstacle(BannerRoll)");
                break;

            case ObstacleType.LockerMove:
                // 레인 이동으로 피해야 하는 벽: 닿으면 무조건 사망
                ForceDieHit("HitObstacle(LockerMove)");
                break;

            default:
                ForceDieHit("HitObstacle(UnknownType)");
                break;
        }
    }

    void ForceDieHit(string debugReason)
    {
        if (dead) return;
        dead = true;

        Debug.Log($"[PLAYER HIT] {debugReason}");

        // ✅ 이제 enum으로 이유 전달
        if (chaserSystem != null)
            chaserSystem.ForceCatch(CatchReason.HitObstacle);
        else
            Debug.LogError("[PlayerCollisionTrigger] chaserSystem is NULL. Add RunChaserSystem to Player.");
    }
}
