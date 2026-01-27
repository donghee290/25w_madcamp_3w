using UnityEngine;

public class ChaserCamRigFollow : MonoBehaviour
{
    public Transform target;              // 이제 ChaserRoot(Granny)
    public Transform lookTarget;          // PlayerRoot(플레이어)
    public Vector3 localOffset = new Vector3(0f, 1.8f, 2.0f); // 체이서 "앞" + 거리(중요: z가 +)
    public float lookHeight = 1.5f;
    public float followSharpness = 12f;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desiredPos = target.position
                             + target.right * localOffset.x
                             + Vector3.up * localOffset.y
                             + target.forward * localOffset.z;

        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);

        Transform lt = lookTarget ? lookTarget : target;
        Vector3 lookAt = lt.position + Vector3.up * lookHeight;
        Quaternion desiredRot = Quaternion.LookRotation((lookAt - transform.position).normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);
    }
}