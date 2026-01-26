using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Runtime")]
    [SerializeField] private GameState state = GameState.Playing;
    [SerializeField] private GameOverReason gameOverReason = GameOverReason.HitObstacle;

    [Header("Distance (meters)")]
    [SerializeField] private float distanceMeters = 0f;

    [Header("Refs")]
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

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (I == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        EnsurePlayerMotor();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 다시 로드되면 기존 레퍼런스가 끊길 수 있어서 재탐색
        EnsurePlayerMotor();
    }

    void EnsurePlayerMotor()
    {
        if (playerMotor != null) return;

        playerMotor = FindFirstObjectByType<PlayerMotor>();
        if (playerMotor == null)
            Debug.LogWarning("[GameManager] PlayerMotor not found in scene.");
        else
            Debug.Log($"[GameManager] PlayerMotor bound: {playerMotor.name}");
    }

    void Update()
    {
        if (state != GameState.Playing) return;

        EnsurePlayerMotor();
        if (playerMotor == null) return;

        distanceMeters += playerMotor.CurrentForwardSpeed * Time.deltaTime;
    }

    public void GameOver(GameOverReason reason)
    {
        if (state == GameState.GameOver) return;

        state = GameState.GameOver;
        gameOverReason = reason;

        if (playerMotor != null)
        {
            playerMotor.ForceStopToIdle();
            playerMotor.enabled = false;
        }

        Debug.Log($"[GameManager] GAME OVER: {reason}, distance={distanceMeters:0.0}m");
    }

    public void RestartSceneSimple()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;
    }

    public void ResetRun()
    {
        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        EnsurePlayerMotor();
        if (playerMotor != null)
            playerMotor.enabled = true;
    }
}
