using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerCollision : MonoBehaviour
{
    [Tooltip("장애물에 붙일 Tag. 기본 Obstacle")]
    public string obstacleTag = "Obstacle";

    private bool dead = false;

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (dead) return;
        if (GameManager.I == null) return;
        if (GameManager.I.State != GameState.Playing) return;

        if (hit.collider != null && hit.collider.CompareTag(obstacleTag))
        {
            dead = true;
            GameManager.I.GameOver(GameOverReason.HitObstacle);
        }
    }
}
