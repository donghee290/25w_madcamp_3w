using UnityEngine;

public class ChaserSystem : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMotor playerMotor;   // 필수
    public MonoBehaviour inputSource; // KeyboardInput or PoseInput (IPlayerInput)

    private IPlayerInput input;

    [Header("Rules (seconds)")]
    public float closeAfter = 3f;
    public float caughtAfter = 5f;

    [Header("MoveLevel thresholds")]
    [Tooltip("이 값 이상이면 달리기(안전)")]
    public float runThreshold = 0.7f;

    [Tooltip("이 값 미만이면 정지로 간주(위험)")]
    public float stopThreshold = 0.2f;

    [Header("Runtime")]
    public float slowOrStopTimer = 0f;
    public ChaserState chaserState = ChaserState.Far;

    void Awake()
    {
        // input 잡기
        if (inputSource != null)
        {
            input = inputSource as IPlayerInput;
        }

        if (input == null)
        {
            // Player에 KeyboardInput이 붙어있다면 자동 발견
            if (playerMotor != null)
                input = playerMotor.GetComponent<KeyboardInput>();
        }
    }

    void Update()
    {
        if (GameManager.I == null) return;
        if (GameManager.I.State != GameState.Playing) return;
        if (playerMotor == null || input == null) return;

        float move = Mathf.Clamp01(input.MoveLevel);

        bool isRunning = move >= runThreshold;
        bool isSlowOrStop = move < runThreshold; // "걷기 or 정지" 전부 위험 누적

        if (isSlowOrStop)
        {
            slowOrStopTimer += Time.deltaTime;
        }
        else
        {
            // 달리면 타이머 서서히 회복(0으로 바로 리셋보다 자연스러움)
            slowOrStopTimer = Mathf.Max(0f, slowOrStopTimer - Time.deltaTime * 2.0f);
        }

        // 상태 계산
        if (slowOrStopTimer >= caughtAfter)
        {
            chaserState = ChaserState.Caught;
            GameManager.I.GameOver(GameOverReason.CaughtByChaser);
            return;
        }
        else if (slowOrStopTimer >= closeAfter)
        {
            chaserState = ChaserState.Close;
        }
        else
        {
            chaserState = ChaserState.Far;
        }

        // 디버그 확인(원하면 끄기)
        // Debug.Log($"[Chaser] move={move:0.00}, t={slowOrStopTimer:0.00}, state={chaserState}");
    }
}
