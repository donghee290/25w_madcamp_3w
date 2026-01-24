public interface IPlayerInput
{
    int Lane { get; }           // -1,0,1
    bool JumpTriggered { get; } // 한 프레임 트리거
    bool RollHeld { get; }      // 누르고 있는 상태
    float MoveLevel { get; }    // 0(멈춤)~1(달림)
}
