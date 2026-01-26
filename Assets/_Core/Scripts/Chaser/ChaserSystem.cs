using UnityEngine;

public class ChaserSystem : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMotor playerMotor;         // 필수 (PlayerRoot에 붙은 PlayerMotor)
    public MonoBehaviour inputSource;       // KeyboardInput or PoseInput (IPlayerInput 구현체)

    private IPlayerInput input;

    [Header("Distance (meters)")]
    public float maxDistance = 10f;
    public float initialDistance = 7f;      // 0 아님!
    public float chaserDistance = 7f;       // 런타임 표시용

    [Header("Distance change per second")]
    public float gainPerSec_Run = 0.2f;     // RUN이면 회복(+)
    public float losePerSec_Walk = 0.8f;    // WALK이면 감소(-)
    public float losePerSec_Stop = 1.6f;    // STOP이면 급감(-)

    [Header("MoveLevel thresholds")]
    [Tooltip("이 값 이상이면 RUN")]
    public float runThreshold = 0.7f;

    [Tooltip("이 값 미만이면 STOP")]
    public float stopThreshold = 0.3f;      // 너희 최종 문서 기준: 0.3

    [Header("Runtime")]
    public ChaserState chaserState = ChaserState.Far;

    [Header("Debug")]
    public bool debugLogs = false;

    void Awake()
    {
        // 시작 거리 세팅
        chaserDistance = Mathf.Clamp(initialDistance, 0f, maxDistance);

        // player 자동 바인딩
        if (playerMotor == null)
            playerMotor = FindFirstObjectByType<PlayerMotor>();

        // input 바인딩: 우선 inputSource, 없으면 playerMotor에서 찾기
        if (inputSource != null && inputSource is IPlayerInput ii)
            input = ii;

        if (input == null && playerMotor != null)
        {
            // PlayerMotor가 붙어있는 오브젝트에 KeyboardInput/PoseInput이 같이 붙어있어야 함
            input = playerMotor.GetComponent<MonoBehaviour>() as IPlayerInput;
            // 위 한 줄은 잘 못 찾을 수 있어서 아래처럼 확실하게 한번 더 검색
            var monos = playerMotor.GetComponents<MonoBehaviour>();
            foreach (var m in monos)
            {
                if (m is IPlayerInput inp) { input = inp; break; }
            }
        }

        if (playerMotor == null)
            Debug.LogError("[ChaserSystem] PlayerMotor not found. Assign it in Inspector.");

        if (input == null)
            Debug.LogError("[ChaserSystem] IPlayerInput not found. Assign inputSource or attach KeyboardInput/PoseInput to PlayerMotor object.");
    }

    void Update()
    {
        if (GameManager.I == null) return;
        if (GameManager.I.State != GameState.Playing) return;
        if (playerMotor == null || input == null) return;

        float move = Mathf.Clamp01(input.MoveLevel);

        // 상태 판정
        bool isRun = move >= runThreshold;
        bool isStop = move < stopThreshold;
        bool isWalk = !isRun && !isStop;

        // 거리 변화량 선택
        float deltaPerSec = 0f;
        if (isRun) deltaPerSec = +gainPerSec_Run;
        else if (isWalk) deltaPerSec = -losePerSec_Walk;
        else deltaPerSec = -losePerSec_Stop;

        // 거리 업데이트
        chaserDistance += deltaPerSec * Time.deltaTime;
        chaserDistance = Mathf.Clamp(chaserDistance, 0f, maxDistance);

        // 상태(연출용)
        // Close 기준은 너희가 UI로 정하면 됨. 일단 간단히 30% 미만이면 Close로.
        if (chaserDistance <= maxDistance * 0.3f) chaserState = ChaserState.Close;
        else chaserState = ChaserState.Far;

        // 패배 조건: 거리 0이면 잡힘
        if (chaserDistance <= 0f)
        {
            chaserState = ChaserState.Caught;
            GameManager.I.GameOver(GameOverReason.CaughtByChaser);
            return;
        }

        if (debugLogs && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[Chaser] move={move:0.00} run={isRun} walk={isWalk} stop={isStop} dist={chaserDistance:0.00}/{maxDistance}");
        }
    }

    // (선택) 외부에서 리셋 호출 가능
    public void ResetDistance()
    {
        chaserDistance = Mathf.Clamp(initialDistance, 0f, maxDistance);
        chaserState = ChaserState.Far;
    }
}
