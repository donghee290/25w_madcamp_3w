using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.transform.root.CompareTag("Player")) return;

        Debug.Log("[CoinPickup] picked!");
        CoinCounter.Add(1);
        Destroy(gameObject);
    }
}
