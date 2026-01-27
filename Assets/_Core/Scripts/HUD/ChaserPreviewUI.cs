using UnityEngine;
using UnityEngine.UI;

public class ChaserPreviewUI : MonoBehaviour
{
    [Header("Refs")]
    public ChaserSystem chaser;
    public Camera chaserCam;
    public RawImage rawImage;

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
        if (!rawImage) rawImage = GetComponentInChildren<RawImage>(true);

        Apply(false);
    }

    void Update()
    {
        if (!chaser || !rawImage) return;

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
        else if (_visible && ratio > hideAtOrAbove) Apply(false); // 여기만 >= -> > 로 변경
    }

    void Apply(bool on)
    {
        _visible = on;

        // RawImage는 끄지 말고 투명도만 제어
        rawImage.enabled = on;

        // 또는 알파로 제어하고 싶으면
        // var c = rawImage.color;
        // c.a = on ? 1f : 0f;
        // rawImage.color = c;

        if (alsoToggleCamera && chaserCam != null)
            chaserCam.enabled = on;
    }
}