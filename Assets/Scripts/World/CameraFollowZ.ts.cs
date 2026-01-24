using UnityEngine;

public class CameraFollowZ : MonoBehaviour
{
    public Transform target;          // PlayerDummy
    public Vector3 offset;             // 현재 카메라 위치 그대로
    public float followSpeed = 10f;    // 따라오는 속도

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desired = new Vector3(
            offset.x,
            offset.y,
            target.position.z + offset.z
        );

        transform.position = Vector3.Lerp(
            transform.position,
            desired,
            followSpeed * Time.deltaTime
        );
    }
}