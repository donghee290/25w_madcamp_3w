using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 3.5f, -6.5f);
    public float smooth = 10f;
    public bool lookAt = true;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * smooth);

        if (lookAt)
        {
            Vector3 lookPos = target.position + Vector3.up * 1.2f;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookPos - transform.position),
                Time.deltaTime * smooth);
        }
    }
}
