public enum GameState
{
    //Intro,
    Countdown,
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
