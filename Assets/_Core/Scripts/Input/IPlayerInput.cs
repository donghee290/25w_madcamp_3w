public interface IPlayerInput
{
    int Lane { get; }           // -1,0,1
    bool JumpTriggered { get; } // �� ������ Ʈ����
    bool RollHeld { get; }      // ������ �ִ� ����
    float MoveLevel { get; }    // 0(����)~1(�޸�)
    bool FlyForward { get; }
}