using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 1.3f, -10.5f);
    public float followLerp = 10f;

    [Header("Lock Y")]
    public bool lockY = true;
    public float fixedY = 1.3f;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desired = target.position + offset;

        if (lockY)
            desired.y = fixedY; // ✅ 점프해도 카메라 높이 고정

        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * followLerp);
    }
}