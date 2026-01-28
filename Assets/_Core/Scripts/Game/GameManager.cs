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

    [Header("UI (optional)")]
    [Tooltip("게임오버 즉시 사라져야 하는 TopBar 루트")]
    [SerializeField] private GameObject topBarRoot;

    [Tooltip("게임오버 후 2초 뒤에 뜨는 ReportPopup 루트")]
    [SerializeField] private GameObject reportPopupRoot;

    [Tooltip("ReportPopup 지연 시간(초)")]
    [SerializeField] private float reportPopupDelay = 2f;

    public GameState State => state;
    public GameOverReason Reason => gameOverReason;
    public float DistanceMeters => distanceMeters;

    public System.Action<GameOverReason> OnGameOverEvent;

    private Coroutine reportPopupCo;

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
        EnsureUIRefs(); // 씬 시작 시 UI 참조도 잡아둠
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 다시 로드되면 기존 레퍼런스가 끊길 수 있어서 재탐색
        EnsurePlayerMotor();
        EnsureUIRefs();

        // 씬 로드 시 기본 UI 상태 정리(재시작/씬전환 시 꼬임 방지)
        if (topBarRoot != null) topBarRoot.SetActive(true);
        if (reportPopupRoot != null) reportPopupRoot.SetActive(false);
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

    void EnsureUIRefs()
    {
        // 인스펙터에 연결돼 있으면 그대로 사용
        // DontDestroyOnLoad라 씬 바뀌면 null이 되거나(파괴), 이전 씬 오브젝트를 잡고 있을 수 있어서
        // 매 씬 로드시 새로 찾아주는 게 안전함.
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

        // 1) 즉시 TopBar 숨김 (reportPopup과 동시에 뜨면 안 되니까 제일 먼저)
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

    public void RestartSceneSimple()
    {
        // 코루틴 정리
        if (reportPopupCo != null) { StopCoroutine(reportPopupCo); reportPopupCo = null; }

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ResetRun()
    {
        // 코루틴 정리
        if (reportPopupCo != null) { StopCoroutine(reportPopupCo); reportPopupCo = null; }

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        EnsurePlayerMotor();
        if (playerMotor != null)
            playerMotor.enabled = true;

        EnsureUIRefs();
        if (topBarRoot != null) topBarRoot.SetActive(true);
        if (reportPopupRoot != null) reportPopupRoot.SetActive(false);
    }

    public void RestartMainScene()
    {
        // 코루틴 정리
        if (reportPopupCo != null) { StopCoroutine(reportPopupCo); reportPopupCo = null; }

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        SceneManager.LoadScene("Main");
    }

    public void GoToStartScene()
    {
        // 코루틴 정리
        if (reportPopupCo != null) { StopCoroutine(reportPopupCo); reportPopupCo = null; }

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        // 혹시 메인에서 죽고 playerMotor disabled 상태로 남아도,
        // StartScene에서는 보통 플레이어가 없으니 그냥 씬만 전환해도 OK.
        playerMotor = null;

        SceneManager.LoadScene("StartScene");
    }
}