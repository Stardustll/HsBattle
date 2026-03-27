using HsBattle.Strategy.Hb;

namespace HsBattle.Strategy
{
    internal sealed class HbStrategyEngine : IStrategyEngine
    {
        private readonly HbSnapshotAdapter _snapshotAdapter = new HbSnapshotAdapter();
        private readonly HbBattleDecisionService _battleDecisionService = new HbBattleDecisionService();

        public StrategyEngineResult TryBuildPlan(HsBattle.BattleController controller, StrategyContext context)
        {
            if (context == null)
            {
                return StrategyEngineResult.Fallback("HB strategy context is missing.");
            }

            HbBattleSnapshot snapshot = _snapshotAdapter.CreateBattleSnapshot(context);
            StrategyActionPlan plan = _battleDecisionService.Decide(snapshot);
            return plan != null ? StrategyEngineResult.Success(plan) : StrategyEngineResult.Fallback("HB strategy engine could not build a safe plan.");
        }
    }
}
