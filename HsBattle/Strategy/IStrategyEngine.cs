namespace HsBattle.Strategy
{
    internal interface IStrategyEngine
    {
        StrategyEngineResult TryBuildPlan(HsBattle.BattleController controller, StrategyContext context);
    }
}
