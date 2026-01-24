using UnityEngine;

// ✅ 잡힘 이유 enum
public enum CatchReason
{
    CaughtByDistance,  // 학주가 따라잡음 (거리 0)
    HitObstacle        // 장애물 충돌
}

// 달리기 상태
public enum RunState
{
    RUN,
    WALK,
    STOP
}

public class RunChaserSystem : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMotor motor;   // 자동으로 Player에서 찾음

    [Header("Chaser Distance")]
    public float chaserDistance = 5f;
    public float maxDistance = 8f;

    [Header("Distance Change Per Second")]
    public float gainRun = 0.2f;    // RUN일 때 벌어짐
    public float lossWalk = 0.8f;   // WALK일 때 줄어듦
    public float lossStop = 2.2f;   // STOP일 때 빠르게 줄어듦

    [Header("RunIntensity Thresholds")]
    public float runThreshold = 0.7f;
    public float stopThreshold = 0.3f;

    public RunState State { get; private set; } = RunState.RUN;
    public bool IsCaught { get; private set; } = false;

    void Awake()
    {
        if (motor == null)
            motor = GetComponentInParent<PlayerMotor>();

        if (motor == null)
            Debug.LogError("[RunChaserSystem] PlayerMotor NOT found!");
    }

    void Update()
    {
        if (IsCaught) return;
        if (motor == null) return;

        float r = motor.CurrentRunIntensity;

        // 1️⃣ 상태 결정
        if (r >= runThreshold)
            State = RunState.RUN;
        else if (r >= stopThreshold)
            State = RunState.WALK;
        else
            State = RunState.STOP;

        // 2️⃣ 거리 업데이트 (STOP 타이머 ❌)
        switch (State)
        {
            case RunState.RUN:
                chaserDistance += gainRun * Time.deltaTime;
                break;

            case RunState.WALK:
                chaserDistance -= lossWalk * Time.deltaTime;
                break;

            case RunState.STOP:
                chaserDistance -= lossStop * Time.deltaTime;
                break;
        }

        chaserDistance = Mathf.Clamp(chaserDistance, 0f, maxDistance);

        // 3️⃣ 거리 0이면 잡힘 (유일한 RUN 루프 패배 조건)
        if (chaserDistance <= 0f)
        {
            ForceCatch(CatchReason.CaughtByDistance);
        }
    }

    // ✅ 외부(PlayerCollisionTrigger)에서도 호출
    public void ForceCatch(CatchReason reason)
    {
        if (IsCaught) return;
        IsCaught = true;

        Debug.Log($"[CAUGHT] Reason = {reason}");

        // Player 멈추고 싶으면 (선택)
        // motor.enabled = false;

        // ✅ GameOver는 여기서만
        if (GameManager.I != null)
        {
            // GameManager가 이유별 연출 처리
            GameManager.I.GameOver(reason);
        }
        else
        {
            Debug.LogError("[RunChaserSystem] GameManager.I is NULL");
        }
    }
}
