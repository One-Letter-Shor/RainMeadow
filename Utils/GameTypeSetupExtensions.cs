namespace RainMeadow;

public static class GameTypeSetupExtensions
{
    private static int _killScore = 0;
    private static int _emptyDeathScore = 0;

    extension(ArenaSetup.GameTypeSetup self)
    {
        public int KillScore { get => _killScore; set => _killScore = value; }
        public int EmptyDeathScore { get => _emptyDeathScore; set => _emptyDeathScore = value; }
    }
}
