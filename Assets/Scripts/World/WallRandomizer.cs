using UnityEngine;

public class WallRandomizer : MonoBehaviour
{
    [Header("Slots (empty transforms inside the chunk)")]
    public Transform leftSlot;
    public Transform rightSlot;

    [Header("Wall variants (prefabs)")]
    public GameObject[] wallVariants;

    private GameObject _leftInstance;
    private GameObject _rightInstance;

    void OnEnable()
    {
        // Fix: Disable the mesh of the slot itself so we don't see the placeholder wall (Pink Wall fix)
        // But keep the GameObject active so children (spawned walls) are visible.
        if (leftSlot != null && leftSlot.GetComponent<MeshRenderer>()) 
            leftSlot.GetComponent<MeshRenderer>().enabled = false;
        
        if (rightSlot != null && rightSlot.GetComponent<MeshRenderer>()) 
            rightSlot.GetComponent<MeshRenderer>().enabled = false;

        ApplyRandomWalls();
    }

    public void ApplyRandomWalls()
    {
        if (leftSlot == null || rightSlot == null) return;
        if (wallVariants == null || wallVariants.Length == 0) return;

        // Cleanup previous
        if (_leftInstance != null) Destroy(_leftInstance);
        if (_rightInstance != null) Destroy(_rightInstance);

        // Disable existing slots to prevent "Pink Wall" or other artifacts
        leftSlot.gameObject.SetActive(false);
        rightSlot.gameObject.SetActive(false);

        // Symmetric Selection
        var randomIndex = Random.Range(0, wallVariants.Length);
        var selectedPrefab = wallVariants[randomIndex];

        // Spawn LEFT (Manual Position/Rotation to bypass Slot issues)
        // Position: -3, Rotation: 0
        _leftInstance = Instantiate(selectedPrefab, transform);
        _leftInstance.transform.localPosition = new Vector3(-3, 0, 0);
        _leftInstance.transform.localRotation = Quaternion.identity;
        _leftInstance.transform.localScale = Vector3.one;

        // Spawn RIGHT (Manual Position/Rotation)
        // Position: 3, Rotation: 180
        _rightInstance = Instantiate(selectedPrefab, transform);
        _rightInstance.transform.localPosition = new Vector3(3, 0, 0);
        _rightInstance.transform.localRotation = Quaternion.Euler(0, 180, 0);
        _rightInstance.transform.localScale = Vector3.one;
    }
}