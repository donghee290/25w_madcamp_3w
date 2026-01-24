using UnityEngine;
using TMPro;

public class DistanceHUD : MonoBehaviour
{
    [Header("Bind in Inspector")]
    [SerializeField] private TextMeshProUGUI distanceText; // <-- ÀÌ°Ô ½½·Ô
    [SerializeField] private TextMeshProUGUI stateText;    // <-- ÀÌ°Ô ½½·Ô

    void Update()
    {
        if (GameManager.I == null) return;

        if (distanceText != null)
            distanceText.text = $"{GameManager.I.DistanceMeters:0} m";

        if (stateText != null)
        {
            if (GameManager.I.State == GameState.Playing) stateText.text = "";
            else stateText.text = $"GAME OVER\n({GameManager.I.Reason})";
        }
    }
}
