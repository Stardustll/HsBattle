namespace HsBattle.Strategy
{
    internal sealed class StrategyContext
    {
        public StrategyContext(GameState gameState, Network.Options options, StrategyMode strategyMode)
            : this()
        {
            GameState = gameState;
            Options = options;
            StrategyMode = strategyMode;
        }

        private StrategyContext()
        {
        }

        public GameState GameState { get; }

        public Network.Options Options { get; }

        public StrategyMode StrategyMode { get; }
    }
}
