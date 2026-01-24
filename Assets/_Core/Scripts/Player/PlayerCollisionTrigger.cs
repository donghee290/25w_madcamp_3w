using UnityEngine;

public class PlayerCollisionTrigger : MonoBehaviour
{
    [Header("Settings")]
    public string obstacleTag = "Obstacle";
    public PlayerMotor motor;   // 자동으로 찾음

    private bool dead = false;

    void Awake()
    {
        // Hitbox는 Player의 자식이므로 부모에서 PlayerMotor 찾기
        if (motor == null)
            motor = GetComponentInParent<PlayerMotor>();

        if (motor == null)
            Debug.LogError("[PlayerCollisionTrigger] PlayerMotor NOT found!");
    }

    void OnCollisionEnter(Collision other)
    {
        if (other.transform.root == transform.root) return;
        if (dead) return;

        // GameManager 체크 (임시로 State 체크 제거 → 원인 분리)
        if (GameManager.I == null)
        {
            Debug.LogError("[HIT] GameManager.I is NULL");
            return;
        }

        // Tag 확인
        if (!other.gameObject.CompareTag(obstacleTag))
        {
            Debug.Log("[HIT] But tag is not Obstacle");
            return;
        }

        // 타입 판정 (없어도 죽게 처리)
        var marker = other.gameObject.GetComponent<ObstacleMarker>();
        if (marker == null)
        {
            Debug.Log("[HIT] No ObstacleMarker → Die");
            DieHit();
            return;
        }

        bool rolling = motor != null && motor.IsRolling;
        bool airborne = motor != null && motor.IsAirborne;

        switch (marker.type)
        {
            case ObstacleType.DeskJump:
                Debug.Log("[HIT] DeskJump");
                if (!airborne) DieHit();
                break;

            case ObstacleType.BannerRoll:
                Debug.Log("[HIT] BannerRoll");
                if (!rolling) DieHit();
                break;

            case ObstacleType.LockerMove:
                Debug.Log("[HIT] LockerMove");
                DieHit();
                break;
        }
    }

    void DieHit()
    {
        if (dead) return;
        dead = true;

        Debug.Log("[GAME OVER] HitObstacle");
        GameManager.I.GameOver(GameOverReason.HitObstacle);
    }
}
