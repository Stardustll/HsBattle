using HsBattle.Strategy.Hb;

namespace HbFriendlyTargetBehavior
{
    public static class EvaluatorProxy
    {
        public static int ScoreTarget(HbBattleOptionSnapshot option, HbBattleTargetSnapshot target)
        {
            HbHeuristicEvaluator evaluator = new HbHeuristicEvaluator();
            return evaluator.ScoreBattleTarget(null, option, target);
        }
    }
}
