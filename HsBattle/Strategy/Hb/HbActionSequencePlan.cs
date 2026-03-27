using System.Collections.Generic;

namespace HsBattle.Strategy.Hb
{
    internal sealed class HbActionSequencePlan
    {
        public HbActionSequencePlan()
        {
            Steps = new List<StrategyActionPlan>();
        }

        public int Score { get; set; }

        public List<StrategyActionPlan> Steps { get; }
    }
}
