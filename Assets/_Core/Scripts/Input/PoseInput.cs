using UnityEngine;

public class PoseInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool RollHeld { get; private set; }
    public float MoveLevel { get; private set; }

    // TODO: 여기서 MediaPipe landmark 받아서 값 세팅
    void Update()
    {
        // 임시: 아직 연결 전이면 전부 stop 상태
        JumpTriggered = false;
        RollHeld = false;
        MoveLevel = 0f;
        Lane = 0;
    }
}
