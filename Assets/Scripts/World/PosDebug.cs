using UnityEngine;

public class PosDebug : MonoBehaviour
{
    public Transform aj; // Aj 드래그

    void Update()
    {
        if (Time.frameCount % 30 != 0) return;
        Debug.Log($"[POS] PlayerRoot z={transform.position.z:0.00} | Aj localZ={(aj ? aj.localPosition.z : 0):0.00} | Aj worldZ={(aj ? aj.position.z : 0):0.00}");
    }
}