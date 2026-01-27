public enum GameState
{
    //Intro,
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
