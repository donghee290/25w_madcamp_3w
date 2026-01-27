using UnityEngine;

public class ChaserSystem : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMotor playerMotor;         // �ʼ� (PlayerRoot�� ���� PlayerMotor)
    public MonoBehaviour inputSource;       // KeyboardInput or PoseInput (IPlayerInput ����ü)

    private IPlayerInput input;

    [Header("Distance (meters)")]
    public float maxDistance = 10f;
    public float initialDistance = 7f;      // 0 �ƴ�!
    public float chaserDistance = 7f;       // ��Ÿ�� ǥ�ÿ�

    [Header("Distance change per second")]
    public float gainPerSec_Run = 0.2f;     // RUN�̸� ȸ��(+)
    public float losePerSec_Walk = 0.8f;    // WALK�̸� ����(-)
    public float losePerSec_Stop = 1.6f;    // STOP�̸� �ް�(-)

    [Header("MoveLevel thresholds")]
    [Tooltip("�� �� �̻��̸� RUN")]
    public float runThreshold = 0.7f;

    [Tooltip("�� �� �̸��̸� STOP")]
    public float stopThreshold = 0.3f;      // ���� ���� ���� ����: 0.3

    [Header("Runtime")]
    public ChaserState chaserState = ChaserState.Far;

    [Header("Debug")]
    public bool debugLogs = false;

    void Awake()
    {
        // ���� �Ÿ� ����
        chaserDistance = Mathf.Clamp(initialDistance, 0f, maxDistance);

        // player �ڵ� ���ε�
        if (playerMotor == null)
            playerMotor = FindFirstObjectByType<PlayerMotor>();

        // input ���ε�: �켱 inputSource, ������ playerMotor���� ã��
        if (inputSource != null && inputSource is IPlayerInput ii)
            input = ii;

        if (input == null && playerMotor != null)
        {
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

        if (playerMotor != null && playerMotor.IsFlying)
        {
            // 따라오기 불가: 거리 회복 or 고정
            chaserDistance = Mathf.Min(maxDistance, chaserDistance + 2.0f * Time.deltaTime);
            chaserState = ChaserState.Far;
            return;
        }

        // 착지 직후 안전시간엔 추격 완화(거리 회복 or 고정)
        if (Time.time < PlayerMotor.SafeUntilTime)
        {
            chaserDistance = Mathf.Min(maxDistance, chaserDistance + 2.0f * Time.deltaTime);
            chaserState = ChaserState.Far;
            return;
        }

        float move = Mathf.Clamp01(input.MoveLevel);

        // ���� ����
        bool isRun = move >= runThreshold;
        bool isStop = move < stopThreshold;
        bool isWalk = !isRun && !isStop;

        // �Ÿ� ��ȭ�� ����
        float deltaPerSec = 0f;
        if (isRun) deltaPerSec = +gainPerSec_Run;
        else if (isWalk) deltaPerSec = -losePerSec_Walk;
        else deltaPerSec = -losePerSec_Stop;

        // �Ÿ� ������Ʈ
        chaserDistance += deltaPerSec * Time.deltaTime;
        chaserDistance = Mathf.Clamp(chaserDistance, 0f, maxDistance);

        // ����(�����)
        // Close ������ ���� UI�� ���ϸ� ��. �ϴ� ������ 30% �̸��̸� Close��.
        if (chaserDistance <= maxDistance * 0.3f) chaserState = ChaserState.Close;
        else chaserState = ChaserState.Far;

        // �й� ����: �Ÿ� 0�̸� ����
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

    // (����) �ܺο��� ���� ȣ�� ����
    public void ResetDistance()
    {
        chaserDistance = Mathf.Clamp(initialDistance, 0f, maxDistance);
        chaserState = ChaserState.Far;
    }
}
