using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Runtime")]
    [SerializeField] private GameState state = GameState.Playing;
    [SerializeField] private GameOverReason gameOverReason = GameOverReason.HitObstacle;

    [Header("Distance (meters)")]
    [SerializeField] private float distanceMeters = 0f;

    [Header("Refs")]
    [Tooltip("PlayerMotor를 여기에 넣으면, GameOver 때 자동으로 멈춥니다.")]
    public PlayerMotor playerMotor;

    public GameState State => state;
    public GameOverReason Reason => gameOverReason;
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

    void Update()
    {
        if (state != GameState.Playing) return;
        if (playerMotor == null) return;

        // "m" 누적: 현재 전진 속도(m/s) * dt
        distanceMeters += playerMotor.CurrentForwardSpeed * Time.deltaTime;
    }

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

        Debug.Log($"[GameManager] GAME OVER: {reason}, distance={distanceMeters:0.0}m");
    }

    // 재시작(나중에 UI 버튼에 연결)
    public void RestartSceneSimple()
    {
        // 가장 단순하게: 현재 씬 다시 로드
        // (나중에 SceneManager로 교체)
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );

        // 싱글톤은 유지되므로 값 리셋
        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;
    }

    public void ResetRun()
    {
        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        if (playerMotor != null)
            playerMotor.enabled = true;
    }
}
