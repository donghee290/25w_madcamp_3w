using UnityEngine;

public class ChaserCamRigFollow : MonoBehaviour
{
    [Header("Follow")]
    public Transform followTarget;  // PlayerRoot (포지션 기준)
    public Vector3 localOffset = new Vector3(0f, 2.2f, -5.0f);
    public float followSharpness = 12f;

    [Header("LookAt")]
    public Transform lookTarget;    // ChaserRoot (회전 기준)
    public float lookHeight = 1.5f; // lookTarget 기준 y 오프셋
    public bool useLookAt = true;

    [Header("Optional")]
    public bool lockRoll = true;    // 기울어짐 방지

    void LateUpdate()
    {
        if (!followTarget) return;

        // 1) 위치: 플레이어 기준 뒤쪽(플레이어 forward 기준)
        Vector3 desiredPos =
            followTarget.position
            + followTarget.right * localOffset.x
            + Vector3.up * localOffset.y
            + followTarget.forward * localOffset.z;

        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);

        // 2) 회전: 기본은 lookTarget(학주)을 바라봄
        if (useLookAt && lookTarget != null)
        {
            Vector3 lookAt = lookTarget.position + Vector3.up * lookHeight;
            Vector3 dir = (lookAt - transform.position);
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);
            }
        }
        else
        {
            // fallback: 플레이어 바라보기(기존 동작)
            Vector3 lookAt = followTarget.position + Vector3.up * lookHeight;
            Vector3 dir = (lookAt - transform.position);
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);
            }
        }

        // 3) 롤 제거(좌우 기울어짐 방지)
        if (lockRoll)
        {
            var e = transform.eulerAngles;
            e.z = 0f;
            transform.eulerAngles = e;
        }
    }
}