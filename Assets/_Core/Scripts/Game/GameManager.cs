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

    public System.Action<GameOverReason> OnGameOverEvent;

/*
    public void BeginRun()
    {
        state = GameState.Playing;
    }
*/
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

        Debug.Log("[GameManager] GameOver CALLED");

        state = GameState.GameOver;
        gameOverReason = reason;

        if (playerMotor != null)
        {
            playerMotor.ForceStopToIdle();
            playerMotor.enabled = false;
        }

        OnGameOverEvent?.Invoke(reason);
    }

    public void RestartSceneSimple()
    {
        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

    public void RestartMainScene()
    {
        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        SceneManager.LoadScene("Main");
    }

    public void GoToStartScene()
    {
        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        // 혹시 메인에서 죽고 playerMotor disabled 상태로 남아도,
        // StartScene에서는 보통 플레이어가 없으니 그냥 씬만 전환해도 OK.
        playerMotor = null;

        SceneManager.LoadScene("StartScene");
    }

}
