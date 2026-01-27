using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ComicSlideIntro : MonoBehaviour
{
    [Header("Start UI (숨길 것들)")]
    public GameObject startUIRoot;

    [Header("White Background (처음엔 OFF)")]
    public GameObject whiteBG;

    [Header("Tap Button")]
    public Button fullscreenButton;

    [Header("Panels")]
    public RectTransform panel1;
    public RectTransform panel2;
    public RectTransform panel3;

    [Header("Final positions (anchoredPosition)")]
    public Vector2 finalPos1;
    public Vector2 finalPos2;
    public Vector2 finalPos3;

    [Header("Slide")]
    public float slideDuration = 0.55f;      // 더 길게 해서 '움직임'이 잘 보이게
    public float gapDelay = 0.12f;
    public AnimationCurve slideEase = null;

    [Header("Cartoon Push (Overshoot)")]
    public float overshootPx = 90f;     // 목표를 얼마나 넘길지(px)
    public float settleDuration = 0.10f; // 넘긴 후 제자리로 돌아오는 시간

    [Header("Squash & Stretch")]
    public float stretchAmount = 0.12f; // 0.1~0.18 추천 (과하면 이상함)
    public float squashDuration = 0.12f;


    [Tooltip("화면 밖으로 숨길 여유 마진(px)")]
    public float offscreenMargin = 150f;

    [Header("Pop (Scale 1.05 -> 1.0)")]
    public float popScale = 1.05f;
    public float popDuration = 0.12f;

    [Header("Shake")]
    public RectTransform shakeTarget;        // PanelsRoot 또는 Canvas
    public float shakeDuration = 0.12f;
    public float shakeStrength = 12f;

    [Header("SFX")]
    public AudioSource audioSource;
    public AudioClip pageFlip;
    public AudioClip punch;

    [Header("Next Scene")]
    public string mainSceneName = "Main";

    bool started = false;
    float offscreenX; // 자동 계산
    Vector2 shakeOrigin;

    void Awake()
    {
        if (slideEase == null) slideEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // 시작 시 흰배경 OFF
        if (whiteBG != null) whiteBG.SetActive(false);

        // ✅ 시작 시 패널은 아예 꺼두기 (플래시 방지)
        if (panel1 != null) panel1.gameObject.SetActive(false);
        if (panel2 != null) panel2.gameObject.SetActive(false);
        if (panel3 != null) panel3.gameObject.SetActive(false);

        // 흔들림 원점 저장
        if (shakeTarget != null) shakeOrigin = shakeTarget.anchoredPosition;

        // offscreenX 자동 계산 (현재 루트 폭 기준)
        RectTransform root = GetRootRect(panel1) ?? GetRootRect(panel2) ?? GetRootRect(panel3);
        float rootWidth = root != null ? root.rect.width : 1080f;
        offscreenX = (rootWidth * 0.55f) + offscreenMargin;
    }

    RectTransform GetRootRect(RectTransform rt)
    {
        if (rt == null) return null;
        Transform t = rt.parent;
        while (t != null)
        {
            if (t is RectTransform r) return r;
            t = t.parent;
        }
        return null;
    }

    void HidePanels()
    {
        // ✅ 켠 직후 바로 화면 밖으로 보내기
        if (panel1 != null) panel1.anchoredPosition = new Vector2(+offscreenX, finalPos1.y);
        if (panel2 != null) panel2.anchoredPosition = new Vector2(-offscreenX, finalPos2.y);
        if (panel3 != null) panel3.anchoredPosition = new Vector2(+offscreenX, finalPos3.y);

        if (panel1 != null) panel1.localScale = Vector3.one;
        if (panel2 != null) panel2.localScale = Vector3.one;
        if (panel3 != null) panel3.localScale = Vector3.one;
    }

    public void Play()
    {
        if (started) return;
        started = true;

        // 시작 UI 숨김
        if (startUIRoot != null) startUIRoot.SetActive(false);

        // 흰 배경 켬 (패널 나오기 직전)
        if (whiteBG != null) whiteBG.SetActive(true);

        // 중복 탭 방지
        if (fullscreenButton != null) fullscreenButton.interactable = false;

        // ✅ 이제 여기서 패널을 켠다
        if (panel1 != null) panel1.gameObject.SetActive(true);
        if (panel2 != null) panel2.gameObject.SetActive(true);
        if (panel3 != null) panel3.gameObject.SetActive(true);

        // ✅ 켜자마자 오프스크린 위치로 강제 세팅 (플래시 방지)
        HidePanels();

        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        yield return SlideInWithFX(panel1, finalPos1, isPunch: false);
        yield return new WaitForSeconds(gapDelay);

        yield return SlideInWithFX(panel2, finalPos2, isPunch: true);
        yield return new WaitForSeconds(gapDelay);

        yield return SlideInWithFX(panel3, finalPos3, isPunch: false);

        SceneManager.LoadScene(mainSceneName);
    }

    IEnumerator SlideInWithFX(RectTransform panel, Vector2 finalPos, bool isPunch)
    {
        if (panel == null) yield break;

        // 들어올 때 page flip
        PlaySfx(pageFlip);

        // 슬라이드
        yield return SlideTo(panel, finalPos);

        // 도착 순간: 툭! (shake + punch)
        if (isPunch) PlaySfx(punch);
        yield return StartCoroutine(Shake());

        // 살짝 팝(1.05 -> 1.0)
        yield return StartCoroutine(Pop(panel));
    }

    IEnumerator SlideTo(RectTransform rt, Vector2 target)
    {
        Vector2 start = rt.anchoredPosition;

        // 들어오는 방향(+1 오른쪽->왼쪽, -1 왼쪽->오른쪽)
        float dir = Mathf.Sign(target.x - start.x);
        if (dir == 0) dir = 1f;

        // 1) 목표보다 살짝 "넘어가는" 위치
        Vector2 overshootTarget = new Vector2(target.x + (overshootPx * dir), target.y);

        // --- Phase A: 빠르게 밀고 들어오며(오버슈트까지) 스쿼시/스트레치 ---
        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;

            float u = Mathf.Clamp01(t / slideDuration);

            // 카툰 느낌: 초반이 더 빠르게(가속 강하게) -> 끝에서 확 멈춤
            // EaseInOut보다 "EaseOutExpo" 같은 느낌을 직접 만듦
            float k = 1f - Mathf.Pow(1f - u, 4f);

            rt.anchoredPosition = Vector2.Lerp(start, overshootTarget, k);

            // 스쿼시 & 스트레치 (이동 방향으로 길쭉/높이 납작)
            // u 초반~중반에만 적용되게 사인 곡선으로
            float s = Mathf.Sin(u * Mathf.PI);
            float stretch = 1f + (stretchAmount * s);
            float squash = 1f - (stretchAmount * 0.6f * s);

            // x축 이동이면 X 늘리고 Y 줄임, 반대면 그대로지만 느낌 유지
            rt.localScale = new Vector3(stretch, squash, 1f);

            yield return null;
        }

        // --- Phase B: overshoot에서 target으로 '툭' 돌아오기 (settle) ---
        t = 0f;
        Vector2 from = rt.anchoredPosition;
        Vector3 scaleFrom = rt.localScale;

        while (t < settleDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / settleDuration);
            float k = 1f - Mathf.Pow(1f - u, 3f);

            rt.anchoredPosition = Vector2.Lerp(from, target, k);
            rt.localScale = Vector3.Lerp(scaleFrom, Vector3.one, k);

            yield return null;
        }

        rt.anchoredPosition = target;
        rt.localScale = Vector3.one;
    }









    IEnumerator Pop(RectTransform rt)
    {
        rt.localScale = Vector3.one * popScale;

        float t = 0f;
        Vector3 start = rt.localScale;
        Vector3 end = Vector3.one;

        while (t < popDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / popDuration);
            rt.localScale = Vector3.Lerp(start, end, u);
            yield return null;
        }
        rt.localScale = end;
    }

    IEnumerator Shake()
    {
        if (shakeTarget == null) yield break;

        float t = 0f;
        Vector2 origin = shakeOrigin;

        while (t < shakeDuration)
        {
            t += Time.deltaTime;

            float x = Random.Range(-shakeStrength, shakeStrength);
            float y = Random.Range(-shakeStrength, shakeStrength);

            shakeTarget.anchoredPosition = origin + new Vector2(x, y);
            yield return null;
        }

        shakeTarget.anchoredPosition = origin;
    }

    void PlaySfx(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }
}
