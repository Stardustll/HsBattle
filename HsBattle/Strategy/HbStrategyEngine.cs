namespace HsBattle.Strategy
{
    internal sealed class HbStrategyEngine : IStrategyEngine
    {
        public StrategyEngineResult TryBuildPlan(HsBattle.BattleController controller, StrategyContext context)
        {
            return StrategyEngineResult.Fallback("HB strategy engine is not implemented yet.");
        }
    }
}
