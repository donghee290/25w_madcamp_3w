using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Runtime")]
    [SerializeField] private GameState state = GameState.Playing;

    // 기존 enum 기반(기존 UI/로직 호환용)
    [SerializeField] private GameOverReason gameOverReason = GameOverReason.HitObstacle;

    // ✅ 새 이유(연출 분기용)
    [SerializeField] private CatchReason lastCatchReason = CatchReason.HitObstacle;

    [Header("Distance (meters)")]
    [SerializeField] private float distanceMeters = 0f;

    [Header("Refs")]
    [Tooltip("PlayerMotor를 여기에 넣으면, GameOver 때 자동으로 멈춥니다.")]
    public PlayerMotor playerMotor;

    [Header("Result UI (TMP)")]
    [Tooltip("Canvas 아래 ResultPanel 오브젝트")]
    public GameObject resultPanel;

    [Tooltip("ResultPanel 안 타이틀 TMP")]
    public TextMeshProUGUI resultTitleTMP;

    [Tooltip("ResultPanel 안 설명 TMP")]
    public TextMeshProUGUI resultDescTMP;

    public GameState State => state;
    public GameOverReason Reason => gameOverReason;
    public CatchReason LastCatchReason => lastCatchReason;   // ✅ 연출 분기용
    public float DistanceMeters => distanceMeters;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // 시작 시 결과 UI는 꺼두기
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    void Update()
    {
        if (state != GameState.Playing) return;
        if (playerMotor == null) return;

        // "m" 누적: 현재 전진 속도(m/s) * dt
        distanceMeters += playerMotor.CurrentForwardSpeed * Time.deltaTime;
    }

    // ✅ 새 엔트리: 이유별 연출을 위한 GameOver
    public void GameOver(CatchReason reason)
    {
        if (state == GameState.GameOver) return;

        lastCatchReason = reason;

        // 기존 enum 기반으로도 저장 (호환용)
        // 지금은 enum에 Caught가 없을 수 있으니 HitObstacle로 통일해도 MVP OK
        GameOverReason mapped = GameOverReason.HitObstacle;

        // (나중에 GameOverReason에 Caught 추가되면 아래처럼 분기 가능)
        // if (reason == CatchReason.CaughtByDistance) mapped = GameOverReason.Caught;
        // else mapped = GameOverReason.HitObstacle;

        // 기존 엔트리 호출(상태 변경/플레이어 정지/로그)
        GameOver(mapped);

        // ✅ 결과 UI 표시 + 이유별 문구
        ShowResultUI(reason);

        // (선택) 콘솔 로그
        if (reason == CatchReason.CaughtByDistance)
            Debug.Log("[GameManager] 연출: 학주에게 붙잡힘(거리 0)");
        else
            Debug.Log("[GameManager] 연출: 장애물에 부딪힘");
    }

    // 기존 엔트리 유지 (기존 UI/코드 호환)
    public void GameOver(GameOverReason reason)
    {
        if (state == GameState.GameOver) return;

        state = GameState.GameOver;
        gameOverReason = reason;

        if (playerMotor != null)
        {
            // 가장 간단: PlayerMotor 꺼버리기
            playerMotor.enabled = false;
        }

        Debug.Log($"[GameManager] GAME OVER: {reason}, catch={lastCatchReason}, distance={distanceMeters:0.0}m");
    }

    void ShowResultUI(CatchReason reason)
    {
        if (resultPanel == null) return;

        resultPanel.SetActive(true);

        if (reason == CatchReason.CaughtByDistance)
        {
            if (resultTitleTMP) resultTitleTMP.text = "📢 학주에게 붙잡힘!";
            if (resultDescTMP)
                resultDescTMP.text =
                    $"기록: {distanceMeters:0.0}m\n" +
                    "멈추면 끝이다… 다시 뛰어!";
        }
        else // CatchReason.HitObstacle
        {
            if (resultTitleTMP) resultTitleTMP.text = "💥 장애물에 부딪힘!";
            if (resultDescTMP)
                resultDescTMP.text =
                    $"기록: {distanceMeters:0.0}m\n" +
                    "넘어졌다… 학주가 다가온다!";
        }
    }

    // 재시작(버튼 OnClick에 연결)
    public void RestartSceneSimple()
    {
        // 씬 리로드
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );

        // 싱글톤 유지되므로 값 리셋
        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        lastCatchReason = CatchReason.HitObstacle;
        distanceMeters = 0f;

        // 결과 UI 숨김
        if (resultPanel != null)
            resultPanel.SetActive(false);

        // 안전: 타임스케일 원복
        Time.timeScale = 1f;
    }

    public void ResetRun()
    {
        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        lastCatchReason = CatchReason.HitObstacle;
        distanceMeters = 0f;

        if (playerMotor != null)
            playerMotor.enabled = true;

        if (resultPanel != null)
            resultPanel.SetActive(false);

        Time.timeScale = 1f;
    }
}
