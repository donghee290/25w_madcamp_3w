using System.Collections;
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

    private Coroutine countdownCo;
    public System.Action<GameOverReason> OnGameOverEvent;

    private Coroutine reportPopupCo;

    // PlayerMotor 재탐색 제어
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
        EnsureRefs();
        StartCountdown();
        EnsurePlayerMotor(forceLog: false);
        EnsureUIRefs();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureRefs();
        StartCountdown(); // ✅ 씬 다시 로드되어도 항상 카운트다운부터
    }

    void EnsureRefs(bool forceLog = false)
    {
        if (playerMotor == null)
        {
            playerMotor = FindFirstObjectByType<PlayerMotor>();
        }

        if (playerMotor == null)
        {
            // 경고 스팸 방지
            if (!warnedNoPlayer || forceLog)
            {
                warnedNoPlayer = true;
                Debug.LogWarning("[GameManager] PlayerMotor not found in scene.");
            }
        }
        else
        {
            warnedNoPlayer = false;
            Debug.Log($"[GameManager] PlayerMotor bound: {playerMotor.name}");
        }
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
        if (countdownUI != null)
            countdownUI.Hide();

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
            if (countdownUI != null)
                countdownUI.SetNumber(t);

            yield return new WaitForSeconds(1f);
            t--;
        }

        if (countdownUI != null)
            countdownUI.SetGo();

        yield return new WaitForSeconds(0.3f);

        if (countdownUI != null)
            countdownUI.Hide();

        state = GameState.Playing;
        countdownCo = null;
    }

    void EnsureUIRefs()
    {
        // 이름이 다르면 인스펙터에 직접 연결하세요.
        if (topBarRoot == null)
        {
            var go = GameObject.Find("TopBarRoot");
            if (go != null)
                topBarRoot = go;
        }

        if (reportPopupRoot == null)
        {
            var go = GameObject.Find("ReportPopupRoot");
            if (go != null)
                reportPopupRoot = go;
        }
    }

   
    void Update()
    {
        if (state != GameState.Playing) return;

        // playerMotor가 없는 씬에서도 DontDestroyOnLoad로 Update는 계속 돌 수 있음
        // -> 매 프레임 찾지 말고 findCooldown 주기로만 찾기
        if (playerMotor == null)
        {
            if (Time.time >= nextFindTime)
            {
                nextFindTime = Time.time + findCooldown;
                EnsurePlayerMotor(forceLog: false);
            }
            return;
        }

        distanceMeters += playerMotor.CurrentForwardSpeed * Time.deltaTime;
    }

    public void GameOver(GameOverReason reason)
    {
        if (state == GameState.GameOver) return;

        Debug.Log("[GameManager] GameOver CALLED");

        state = GameState.GameOver;
        gameOverReason = reason;

        // 1) 즉시 TopBar 숨김 (reportPopup과 동시에 작동하면 안됨)
        EnsureUIRefs();
        if (topBarRoot != null) topBarRoot.SetActive(false);

        // 플레이어 정지
        if (playerMotor != null)
        {
            playerMotor.ForceStopToIdle();
            playerMotor.enabled = false;
        }

        // 2) ReportPopup은 2초 뒤
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

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ResetRun()
    {
        CancelReportPopupCo();

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        EnsurePlayerMotor(forceLog: false);
        if (playerMotor != null)
        {
            playerMotor.enabled = true;

        EnsureUIRefs();
        if (topBarRoot != null) topBarRoot.SetActive(true);
        if (reportPopupRoot != null) reportPopupRoot.SetActive(false);
    }

    public void RestartMainScene()
    {
        CancelReportPopupCo();

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        SceneManager.LoadScene("Main");
    }

    public void GoToStartScene()
    {
        CancelReportPopupCo();

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        playerMotor = null;

        SceneManager.LoadScene("StartScene");
    }
}