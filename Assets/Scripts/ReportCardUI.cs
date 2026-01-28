using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ReportCardUI : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject popupRoot;      // ReportCardPopup (루트)
    public RectTransform popupRect;   // popupRoot의 RectTransform (없으면 popupRoot에서 자동 획득)
    public Button retryButton;        // retry
    public Button homeButton;         // home

    [Header("Timing")]
    public float showDelay = 2f;

    [Header("Popup Intro Anim (Float Up)")]
    public float introDuration = 0.34f;  // 애니 길이(조금 더)
    public float introOffsetY = 30f;     // 위로 붕 뜨는 정도(조금 더)
    public float introScaleFrom = 0.92f; // 시작 스케일(조금 더 작게)
    public float introScaleTo = 1.08f;   // 끝 스케일(살짝 오버)

    Coroutine showCo;
    Coroutine introCo;

    Vector2 baseAnchoredPos;
    Vector3 baseScale;

    void Awake()
    {
        // popupRoot는 기본적으로 꺼둠
        if (popupRoot != null) popupRoot.SetActive(false);

        // popupRect 자동 획득
        if (popupRect == null && popupRoot != null)
            popupRect = popupRoot.GetComponent<RectTransform>();

        // 기준값 저장
        if (popupRect != null)
        {
            baseAnchoredPos = popupRect.anchoredPosition;
            baseScale = popupRect.localScale;
        }

        // 버튼 리스너 연결
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryClicked);
            retryButton.onClick.AddListener(OnRetryClicked);
        }

        if (homeButton != null)
        {
            homeButton.onClick.RemoveListener(OnHomeClicked);
            homeButton.onClick.AddListener(OnHomeClicked);
        }
    }

    void Start()
    {
        // GameManager 이벤트 구독 (OnEnable 중복 구독 방지 위해 Start에서만 처리)
        if (GameManager.I == null)
        {
            Debug.LogError("[ReportCardUI] GameManager.I is null in Start()");
            return;
        }

        GameManager.I.OnGameOverEvent -= HandleGameOver; // 중복 방지
        GameManager.I.OnGameOverEvent += HandleGameOver;

        Debug.Log("[ReportCardUI] Subscribed to GameOverEvent");

        // 씬 로드 이벤트 (재시작/씬 이동 시 팝업 초기화)
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        // 구독 해제
        if (GameManager.I != null)
            GameManager.I.OnGameOverEvent -= HandleGameOver;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllRunningCoroutines();
        HidePopupImmediate();
    }

    void HandleGameOver(GameOverReason reason)
    {
        // 이미 떠있으면 중복 방지
        StopAllRunningCoroutines();
        showCo = StartCoroutine(ShowAfterDelay());
    }

    IEnumerator ShowAfterDelay()
    {
        // timeScale=0이어도 딜레이 동작하게
        yield return new WaitForSecondsRealtime(showDelay);

        if (popupRoot != null) popupRoot.SetActive(true);

        // 등장 애니
        if (popupRect != null)
            introCo = StartCoroutine(PopupIntroAnim());

        showCo = null;
    }

    IEnumerator PopupIntroAnim()
    {
        if (popupRect == null) yield break;

        // 기준값 갱신(레이아웃/스케일 변경 대응)
        baseAnchoredPos = popupRect.anchoredPosition;
        baseScale = popupRect.localScale;

        Vector2 fromPos = baseAnchoredPos + new Vector2(0f, introOffsetY);
        Vector2 toPos = baseAnchoredPos;

        Vector3 fromScale = new Vector3(introScaleFrom, introScaleFrom, 1f);
        Vector3 toScale = new Vector3(introScaleTo, introScaleTo, 1f);

        popupRect.anchoredPosition = fromPos;
        popupRect.localScale = fromScale;

        float t = 0f;
        while (t < introDuration)
        {
            t += Time.unscaledDeltaTime; // 게임 멈춰도 UI 애니는 돌게
            float u = Mathf.Clamp01(t / introDuration);

            // “붕” 튕김 느낌(오버슈트)
            float eased = EaseOutBack(u);

            popupRect.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, eased);
            popupRect.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);

            yield return null;
        }

        popupRect.anchoredPosition = toPos;
        popupRect.localScale = toScale;
        introCo = null;
    }

    // 더 “붕” 뜨는 오버슈트 이징
    float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void OnRetryClicked()
    {
        HidePopupImmediate();
        if (GameManager.I != null) GameManager.I.RestartMainScene();
        else SceneManager.LoadScene("Main");
    }

    void OnHomeClicked()
    {
        HidePopupImmediate();
        if (GameManager.I != null) GameManager.I.GoToStartScene();
        else SceneManager.LoadScene("StartScene");
    }

    void HidePopupImmediate()
    {
        if (popupRoot != null) popupRoot.SetActive(false);

        if (popupRect != null)
        {
            popupRect.anchoredPosition = baseAnchoredPos;
            popupRect.localScale = baseScale;
        }
    }

    void StopAllRunningCoroutines()
    {
        if (showCo != null) { StopCoroutine(showCo); showCo = null; }
        if (introCo != null) { StopCoroutine(introCo); introCo = null; }
    }
}