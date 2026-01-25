using UnityEngine;

public class KeyboardInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool RollHeld { get; private set; }
    public float MoveLevel { get; private set; } // 0=정지, 1=달리기

    void Update()
    {
        // 점프: Space만 (UpArrow는 Run에만 사용)
        JumpTriggered = Input.GetKeyDown(KeyCode.Space);

        // 레인 이동: 좌/우 or A/D
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Lane = Mathf.Max(-1, Lane - 1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Lane = Mathf.Min(1, Lane + 1);

        // 롤(슬라이드): 아래 or Ctrl
        RollHeld = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftControl);

        // 달리기: 위쪽 화살표를 누르고 있을 때만 달림
        MoveLevel = Input.GetKey(KeyCode.UpArrow) ? 1f : 0f;

        // (원하면) 걷기/느리게 같은 단계도 여기서 확장 가능
        // 예: Shift 누르면 1.2, S 누르면 0.5 등
    }
}