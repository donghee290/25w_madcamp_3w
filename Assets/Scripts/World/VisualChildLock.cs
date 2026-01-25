using UnityEngine;

public class VisualChildLock : MonoBehaviour
{
    [Header("Assign")]
    public Transform aj; // Aj 드래그

    [Header("Lock Options")]
    public bool lockLocalX = true;
    public bool lockLocalZ = true;
    public bool lockLocalY = false; // 점프 애니메이션이 Y를 움직인다면 false 유지

    Vector3 _baseLocalPos;
    Quaternion _baseLocalRot;

    void Awake()
    {
        if (aj == null)
        {
            Debug.LogError("[VisualChildLock] aj is NULL. Assign Aj Transform.");
            enabled = false;
            return;
        }

        _baseLocalPos = aj.localPosition;
        _baseLocalRot = aj.localRotation;
    }

    void LateUpdate()
    {
        // 애니메이션이 aj.localPosition을 건드려도, 여기서 다시 되돌려서 “되감기” 제거
        var p = aj.localPosition;

        if (lockLocalX) p.x = _baseLocalPos.x;
        if (lockLocalZ) p.z = _baseLocalPos.z;
        if (lockLocalY) p.y = _baseLocalPos.y;

        aj.localPosition = p;

        // 회전까지 튄다면 아래도 켜세요(보통은 필요 없음)
        // aj.localRotation = _baseLocalRot;
    }
}