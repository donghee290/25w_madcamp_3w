using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Countdown")]
    public float countdownSeconds = 3f;
    public CountdownUI countdownUI;
    public PoseInput poseInput;

    [Header("Runtime")]
    [SerializeField] private GameState state = GameState.Countdown;
    [SerializeField] private GameOverReason gameOverReason = GameOverReason.HitObstacle;

    [Header("Distance (meters)")]
    [SerializeField] private float distanceMeters = 0f;

    [Header("Refs")]
    public PlayerMotor playerMotor;

    public GameState State => state;
    public GameOverReason Reason => gameOverReason;
    public float DistanceMeters => distanceMeters;

    private Coroutine countdownCo;

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
        if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        EnsureRefs();
        StartCountdown();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureRefs();
        StartCountdown(); // ✅ 씬 다시 로드되어도 항상 카운트다운부터
    }

    void EnsureRefs()
    {
        if (playerMotor == null)
            playerMotor = FindFirstObjectByType<PlayerMotor>();

        if (poseInput == null)
            poseInput = FindFirstObjectByType<PoseInput>();

        if (countdownUI == null)
            countdownUI = FindFirstObjectByType<CountdownUI>();

        if (playerMotor == null)
            Debug.LogWarning("[GameManager] PlayerMotor not found in scene.");
    }

    public void StartCountdown()
    {
        // 게임오버/이전 코루틴 정리
        if (countdownCo != null)
        {
            StopCoroutine(countdownCo);
            countdownCo = null;
        }

        state = GameState.Countdown;
        distanceMeters = 0f;
        gameOverReason = GameOverReason.HitObstacle;

        EnsureRefs();

        // 안전: 카운트다운 UI 초기화
        if (countdownUI != null) countdownUI.Hide();

        countdownCo = StartCoroutine(CoCountdownAndStart());
    }

    IEnumerator CoCountdownAndStart()
    {
        EnsureRefs();

        // ✅ 시작부터 바닥에 고정 + 완전 정지
        if (playerMotor != null)
            playerMotor.StartOnGroundForCountdown();

        // ✅ 3초 캘리브레이션 (입력 잠금)
        if (poseInput != null)
            poseInput.BeginCalibration(countdownSeconds);

        int t = Mathf.CeilToInt(countdownSeconds);
        while (t > 0)
        {
            if (countdownUI != null) countdownUI.SetNumber(t);
            yield return new WaitForSeconds(1f);
            t--;
        }

        if (countdownUI != null) countdownUI.SetGo();
        yield return new WaitForSeconds(0.3f);
        if (countdownUI != null) countdownUI.Hide();

        state = GameState.Playing;
        countdownCo = null;
    }

    void Update()
    {
        if (state != GameState.Playing) return;

        EnsureRefs();
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
        // ✅ 씬 로드 후 StartCountdown가 자동으로 돌게 되어있음
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ResetRun()
    {
        // ✅ 씬을 재로드하지 않고도 다시 카운트다운부터
        if (playerMotor != null)
        {
            playerMotor.enabled = true;
            playerMotor.ForceStopToIdle();
        }

        StartCountdown();
    }
}
