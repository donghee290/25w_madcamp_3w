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

    [Header("Comic Outro (optional)")]
    [Tooltip("Canvas/ComicOutroRoot에 붙은 ComicSlideOutro")]
    [SerializeField] private ComicSlideOutro comicOutro;

    [Header("Find throttling")]
    [Tooltip("PlayerMotor를 못 찾았을 때 재탐색 쿨다운(초)")]
    [SerializeField] private float findCooldown = 1.0f;

    public GameState State => state;
    public GameOverReason Reason => gameOverReason;
    public float DistanceMeters => distanceMeters;

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
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        EnsurePlayerMotor(forceLog: false);
        EnsureUIRefs();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 전환 시 레퍼런스 재탐색
        playerMotor = null;
        EnsurePlayerMotor(forceLog: false);

        // UI도 씬마다 새로 잡는 게 안전
        topBarRoot = null;
        reportPopupRoot = null;
        comicOutro = null; // ✅ 추가
        EnsureUIRefs();

        // 씬 로드 시 기본 UI 상태 정리(재시작/씬전환 시 꼬임 방지)
        if (topBarRoot != null) topBarRoot.SetActive(true);
        if (reportPopupRoot != null) reportPopupRoot.SetActive(false);

        // 씬 로드시 경고 플래그 초기화(스팸 방지)
        warnedNoPlayer = false;
        nextFindTime = 0f;
    }

    void EnsurePlayerMotor(bool forceLog)
    {
        if (playerMotor != null) return;

        playerMotor = FindFirstObjectByType<PlayerMotor>();

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
            Debug.Log($"[GameManager] PlayerMotor bound: {playerMotor.name}");
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
            // ⚠️ 하이러키 실제 이름이 ReportCardPopup이면 여기 문자열만 바꿔주세요.
            var go = GameObject.Find("ReportPopupRoot");
            if (go != null) reportPopupRoot = go;
        }

        // ✅ ComicOutroRoot에서 스크립트 찾아오기(최소 추가)
        if (comicOutro == null)
        {
            var go = GameObject.Find("ComicOutroRoot");
            if (go != null) comicOutro = go.GetComponent<ComicSlideOutro>();
            if (comicOutro == null) comicOutro = FindFirstObjectByType<ComicSlideOutro>(); // 보험
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

        // 1) 즉시 TopBar 숨김
        EnsureUIRefs();
        if (topBarRoot != null) topBarRoot.SetActive(false);

        // ✅ 1.5) reportPopup 뜨기 전까지 comicOutro 재생 (whiteBG 없음은 outro 스크립트에서 처리)
        if (comicOutro != null) comicOutro.Play();

        // 플레이어 정지
        if (playerMotor != null)
        {
            playerMotor.ForceStopToIdle();
            playerMotor.enabled = false;
        }

        // 2) ReportPopup은 기존대로 2초 뒤
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