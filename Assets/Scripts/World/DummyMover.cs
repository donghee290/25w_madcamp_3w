using UnityEngine;

public class AutoForwardMover : MonoBehaviour
{
    public float speed = 6f;
    public bool useWorldForward = true; // true면 월드 +Z로, false면 오브젝트 forward로

    void Update()
    {
        Vector3 dir = useWorldForward ? Vector3.forward : transform.forward;
        transform.position += dir * speed * Time.deltaTime;
    }
}