namespace HsBattle.Strategy
{
    internal sealed class LegacyStrategyEngine : IStrategyEngine
    {
        public StrategyEngineResult TryBuildPlan(HsBattle.BattleController controller, StrategyContext context)
        {
            if (controller == null)
            {
                return StrategyEngineResult.Fallback("Legacy battle controller is unavailable.");
            }

            if (context == null || context.GameState == null || context.Options == null)
            {
                return StrategyEngineResult.Fallback("Legacy strategy context is incomplete.");
            }

            StrategyActionPlan plan = controller.BuildLegacyStrategyActionPlan(context.GameState, context.Options);
            if (plan == null)
            {
                return StrategyEngineResult.Fallback("Legacy strategy engine could not build a plan.");
            }

            return StrategyEngineResult.Success(plan);
        }
    }
}
