using UnityEngine;

public class PlayerPoseInput : MonoBehaviour
{
    [Header("Debug Keyboard (먼저 성공용)")]
    public bool useKeyboard = true;

    public int Lane { get; private set; } // -1,0,1
    public bool JumpTriggered { get; private set; }
    public bool SlideHeld { get; private set; }

    void Update()
    {
        JumpTriggered = false;

        if (useKeyboard)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Lane = Mathf.Max(-1, Lane - 1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Lane = Mathf.Min(1, Lane + 1);

            if (Input.GetKeyDown(KeyCode.UpArrow)) JumpTriggered = true;
            SlideHeld = Input.GetKey(KeyCode.DownArrow);
            return;
        }

        // TODO: 여기부터 MediaPipe Pose 연결 (다음 단계에서 붙임)
    }
}
