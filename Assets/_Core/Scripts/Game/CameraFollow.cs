using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 3f, -7.5f);
    public float followLerp = 10f;

    [Header("Framing")]
    public float lookAtHeight = 0.6f;

    [Header("Rotation (recommended)")]
    public bool lockRotation = true;
    public Vector3 fixedEuler = new Vector3(25f, 0f, 0f);

    void LateUpdate()
    {
        if (target == null) return;

        // 월드 기준 오프셋 (타겟 회전에 영향 안 받음)
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * followLerp);

        if (lockRotation)
        {
            transform.rotation = Quaternion.Euler(fixedEuler);
        }
        else
        {
            transform.LookAt(target.position + Vector3.up * lookAtHeight);
        }
    }
}