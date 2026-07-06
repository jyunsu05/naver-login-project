public static class GameSession
{
    public static int Score { get; set; }
    public static int HighScore { get; set; }

    public static void ResetScore()
    {
        Score = 0;
    }
}
