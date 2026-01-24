public enum GameState
{
    Playing,
    GameOver
}

public enum GameOverReason
{
    HitObstacle,
    CaughtByChaser
}

public enum ChaserState
{
    Far,
    Close,
    Caught
}
