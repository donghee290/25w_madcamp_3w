using UnityEngine;
using UnityEngine.UI;

public class ChaserGaugeUI : MonoBehaviour
{
    [Header("Refs")]
    public ChaserSystem chaser;   // 씬에 있는 ChaserSystem
    public Slider slider;         // UI Slider

    [Header("Invert")]
    [Tooltip("true면 가까울수록 게이지가 줄어듦(추천)")]
    public bool invert = false;

    void Awake()
    {
        if (slider == null)
            slider = GetComponent<Slider>();

        if (chaser == null)
            chaser = FindFirstObjectByType<ChaserSystem>();

        if (slider == null)
            Debug.LogError("[ChaserGaugeUI] Slider is null. Attach this script to the Slider object or assign it.");

        if (chaser == null)
            Debug.LogError("[ChaserGaugeUI] ChaserSystem not found in scene.");
    }

    void Update()
    {
        if (slider == null || chaser == null) return;

        float ratio = 0f;
        if (chaser.maxDistance > 0.0001f)
            ratio = Mathf.Clamp01(chaser.chaserDistance / chaser.maxDistance);

        // ratio=1이면 멀다, ratio=0이면 잡힘 직전
        slider.value = invert ? (1f - ratio) : ratio;
    }
}
