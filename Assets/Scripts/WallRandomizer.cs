using UnityEngine;

public class WallRandomizer : MonoBehaviour
{
    [Header("Slots (empty transforms inside the chunk)")]
    public Transform leftSlot;
    public Transform rightSlot;

    [Header("Wall variants (prefabs)")]
    public GameObject[] wallVariants;

    [Header("Legacy cleanup")]
    public bool purgeLegacyChildrenOnAwake = true; // 옛날 Wall_Varient_* 자식만 제거

    private GameObject _leftInstance;
    private GameObject _rightInstance;

    void Awake()
    {
        if (purgeLegacyChildrenOnAwake)
            PurgeLegacyChildren();

        // 슬롯 메쉬만 숨기기 (슬롯 GameObject는 끄지 마세요: 자식/기준점으로 쓰일 수 있음)
        var lr = leftSlot ? leftSlot.GetComponent<MeshRenderer>() : null;
        if (lr) lr.enabled = false;

        var rr = rightSlot ? rightSlot.GetComponent<MeshRenderer>() : null;
        if (rr) rr.enabled = false;
    }

    void OnEnable()
    {
        ApplyRandomWalls();
    }

    void PurgeLegacyChildren()
    {
        var trs = GetComponentsInChildren<Transform>(true);
        for (int i = trs.Length - 1; i >= 0; i--)
        {
            var t = trs[i];
            if (t == null || t == transform) continue;

            if (t.name.Contains("Wall_Varient_"))
                Destroy(t.gameObject);
        }
    }

    public void ApplyRandomWalls()
    {
        if (wallVariants == null || wallVariants.Length == 0) return;

        if (_leftInstance) Destroy(_leftInstance);
        if (_rightInstance) Destroy(_rightInstance);

        var selectedPrefab = wallVariants[Random.Range(0, wallVariants.Length)];
        if (selectedPrefab == null) return;

        _leftInstance = Instantiate(selectedPrefab, transform);
        _leftInstance.transform.localPosition = new Vector3(-3, 0, 0);
        _leftInstance.transform.localRotation = Quaternion.identity;
        _leftInstance.transform.localScale = Vector3.one;

        _rightInstance = Instantiate(selectedPrefab, transform);
        _rightInstance.transform.localPosition = new Vector3(3, 0, 0);
        _rightInstance.transform.localRotation = Quaternion.Euler(0, 180, 0);
        _rightInstance.transform.localScale = Vector3.one;
    }
}