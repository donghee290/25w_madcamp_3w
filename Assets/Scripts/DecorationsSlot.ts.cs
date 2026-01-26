using UnityEngine;

public class DecorationsSlot : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("Slot meta")]
    public Side side = Side.Left;

    // 선택: 슬롯 축을 강제하고 싶으면 사용 (기본은 Transform 축 그대로 씀)
    // public bool useLocalAxes = false;

    private void OnDrawGizmos()
    {
        // 씬에서 방향 확인용(안 보이면 길이만 늘려도 됨)
        Gizmos.color = (side == Side.Left) ? new Color(0.3f, 0.8f, 1f, 1f) : new Color(1f, 0.6f, 0.2f, 1f);
        Gizmos.DrawSphere(transform.position, 0.05f);

        // forward / right 방향 표시
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.right * 0.4f);
    }
}