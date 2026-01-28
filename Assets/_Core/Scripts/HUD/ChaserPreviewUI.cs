using UnityEngine;
using UnityEngine.UI;

public class ChaserPreviewUI : MonoBehaviour
{
    [Header("Refs")]
    public ChaserSystem chaser;
    public Camera chaserCam;
    public GameObject camRoot;   // CamRoot 오브젝트
    public RawImage rawImage;    // (선택) null이어도 됨

    [Header("Thresholds (ratio = dist/maxDist)")]
    [Range(0f, 1f)] public float showAtOrBelow = 0.5f;
    [Range(0f, 1f)] public float hideAtOrAbove = 0.6f;

    [Header("Behavior")]
    public bool alsoToggleCamera = true;
    public bool onlyDuringPlaying = true;

    bool _visible;

    void Awake()
    {
        if (!chaser) chaser = FindFirstObjectByType<ChaserSystem>();

        // CamRoot를 인스펙터에 직접 넣는 게 베스트.
        // 자동으로 잡아야 하면 현재 스크립트가 CamRoot 아래/위 어디 붙어있는지에 따라 조정.
        if (!camRoot)
        {
            // 스크립트가 CamRoot에 붙어있다면:
            camRoot = gameObject;

            // 스크립트가 HUDController에 붙어있고 CamRoot가 자식이라면:
            // var t = transform.Find("CamRoot");
            // if (t) camRoot = t.gameObject;
        }

        if (!rawImage) rawImage = GetComponentInChildren<RawImage>(true);

        Apply(false);
    }

    void Update()
    {
        if (!chaser || camRoot == null) return;

        if (onlyDuringPlaying)
        {
            if (GameManager.I == null || GameManager.I.State != GameState.Playing)
            {
                Apply(false);
                return;
            }
        }

        float ratio = 1f;
        if (chaser.maxDistance > 0.0001f)
            ratio = Mathf.Clamp01(chaser.chaserDistance / chaser.maxDistance);

        if (!_visible && ratio <= showAtOrBelow) Apply(true);
        else if (_visible && ratio > hideAtOrAbove) Apply(false);
    }

    void Apply(bool on)
    {
        _visible = on;

        // CamRoot 전체 숨김/표시 (RawImage + Frame 포함)
        camRoot.SetActive(on);

        // 카메라도 같이 끄고 싶으면
        if (alsoToggleCamera && chaserCam != null)
            chaserCam.enabled = on;
    }
}