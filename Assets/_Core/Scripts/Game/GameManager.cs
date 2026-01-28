using System.Collections;
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
    public PoseInput poseInput;

    [Header("UI (optional)")]
    [Tooltip("게임오버 즉시 사라져야 하는 TopBar 루트")]
    [SerializeField] private GameObject topBarRoot;

    [Tooltip("게임오버 후 2초 뒤에 뜨는 ReportPopup 루트")]
    [SerializeField] private GameObject reportPopupRoot;

    [Tooltip("ReportPopup 지연 시간(초)")]
    [SerializeField] private float reportPopupDelay = 2f;

    [Header("Find throttling")]
    [Tooltip("PlayerMotor를 못 찾았을 때 재탐색 쿨다운(초)")]
    [SerializeField] private float findCooldown = 1.0f;

    public GameState State => state;
    public GameOverReason Reason => gameOverReason;
    public float DistanceMeters => distanceMeters;

    public System.Action<GameOverReason> OnGameOverEvent;

    private Coroutine reportPopupCo;
    private float nextFindTime = 0f;
    private bool warnedNoPlayer = false;

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
        EnsureRefs(false);
        EnsureUIRefs();
        StartRunImmediate();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureRefs(false);
        EnsureUIRefs();
        StartRunImmediate();
    }

    void EnsureRefs(bool forceLog = false)
    {
        if (playerMotor == null)
            playerMotor = FindFirstObjectByType<PlayerMotor>();

        if (poseInput == null)
            poseInput = FindFirstObjectByType<PoseInput>();

        if (playerMotor == null)
        {
            if (!warnedNoPlayer || forceLog)
            {
                warnedNoPlayer = true;
                Debug.LogWarning("[GameManager] PlayerMotor not found in scene.");
            }
        }
        else
        {
            warnedNoPlayer = false;
            if (forceLog) Debug.Log($"[GameManager] PlayerMotor bound: {playerMotor.name}");
        }
    }

    void EnsureUIRefs()
    {
        if (topBarRoot == null)
        {
            var go = GameObject.Find("TopBarRoot");
            if (go != null) topBarRoot = go;
        }

        if (reportPopupRoot == null)
        {
            var go = GameObject.Find("ReportPopupRoot");
            if (go != null) reportPopupRoot = go;
        }
    }

    public void StartRunImmediate()
    {
        CancelReportPopupCo();

        // ✅ 상태 초기화
        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        EnsureRefs(false);
        EnsureUIRefs();

        // ✅ UI 초기 상태
        if (topBarRoot != null) topBarRoot.SetActive(true);
        if (reportPopupRoot != null) reportPopupRoot.SetActive(false);

        // ✅ 플레이어 활성화
        if (playerMotor != null)
        {
            playerMotor.enabled = true;
            playerMotor.ForceStopToIdle(); // 시작 속도/상태 리셋
            // 바닥에 붙이기 원하면 아래 줄 유지
            playerMotor.StartOnGroundForCountdown();
        }
    }

    void Update()
    {
        if (state != GameState.Playing) return;

        if (playerMotor == null)
        {
            if (Time.time >= nextFindTime)
            {
                nextFindTime = Time.time + findCooldown;
                EnsureRefs(false);
            }
            return;
        }

        distanceMeters += playerMotor.CurrentForwardSpeed * Time.deltaTime;
    }

    public void GameOver(GameOverReason reason)
    {
        if (state == GameState.GameOver) return;

        state = GameState.GameOver;
        gameOverReason = reason;

        EnsureUIRefs();

        // TopBar 숨김
        if (topBarRoot != null) topBarRoot.SetActive(false);

        // 플레이어 정지
        if (playerMotor != null)
        {
            playerMotor.ForceStopToIdle();
            playerMotor.enabled = false;
        }

        if (reportPopupCo != null) StopCoroutine(reportPopupCo);
        reportPopupCo = StartCoroutine(ShowReportPopupAfterDelay());

        OnGameOverEvent?.Invoke(reason);
    }

    private IEnumerator ShowReportPopupAfterDelay()
    {
        yield return new WaitForSeconds(reportPopupDelay);
        EnsureUIRefs();
        if (reportPopupRoot != null) reportPopupRoot.SetActive(true);
    }

    void CancelReportPopupCo()
    {
        if (reportPopupCo != null)
        {
            StopCoroutine(reportPopupCo);
            reportPopupCo = null;
        }
    }

    public void RestartSceneSimple()
    {
        CancelReportPopupCo();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ResetRun()
    {
        CancelReportPopupCo();
        StartRunImmediate();
    }

    public void RestartMainScene()
    {
        CancelReportPopupCo();
        SceneManager.LoadScene("Main");
    }

    public void GoToStartScene()
    {
        CancelReportPopupCo();
        playerMotor = null;
        SceneManager.LoadScene("StartScene");
    }
}
