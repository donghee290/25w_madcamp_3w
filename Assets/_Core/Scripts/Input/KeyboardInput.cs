using UnityEngine;

public class KeyboardInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool RollHeld { get; private set; }
    public float MoveLevel { get; private set; }

    void Update()
    {
        // 점프: Space 또는 ↑
        JumpTriggered = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow);

        // 레인: ←/→ 또는 A/D
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Lane = Mathf.Max(-1, Lane - 1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Lane = Mathf.Min(1, Lane + 1);

        // 슬라이드: ↓ 또는 Ctrl
        RollHeld = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftControl);

        // 이동 강도(그대로)
        MoveLevel = 1f;
        if (Input.GetKey(KeyCode.S)) MoveLevel = 0.3f;
        if (Input.GetKey(KeyCode.X)) MoveLevel = 0f;
    }
}
