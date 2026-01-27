using UnityEngine;
using TMPro;

public class DistanceHUD : MonoBehaviour
{
    [Header("Bind in Inspector")]
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private TextMeshProUGUI stateText;

    void Update()
    {
        if (GameManager.I == null) return;

        if (distanceText != null)
        {
            float shown = GameManager.I.DistanceMeters * 0.1f;
            distanceText.text = $"{Mathf.FloorToInt(shown)} m";
        }

        if (stateText != null)
        {
            if (GameManager.I.State == GameState.Playing) stateText.text = "";
            else stateText.text = $"GAME OVER\n({GameManager.I.Reason})";
        }
    }
}
