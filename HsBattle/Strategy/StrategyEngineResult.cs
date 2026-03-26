using System;

namespace HsBattle.Strategy
{
    internal enum StrategyEngineStatus
    {
        Success,
        Fallback
    }

    internal sealed class StrategyEngineResult
    {
        private StrategyEngineResult(StrategyEngineStatus status, StrategyActionPlan plan, string reason)
        {
            Status = status;
            Plan = plan;
            Reason = reason;
        }

        public StrategyEngineStatus Status { get; private set; }

        public StrategyActionPlan Plan { get; private set; }

        public string Reason { get; private set; }

        public static StrategyEngineResult Success(StrategyActionPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException("plan");
            }

            return new StrategyEngineResult(StrategyEngineStatus.Success, plan, null);
        }

        public static StrategyEngineResult Fallback(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "Strategy engine fell back without a specific reason.";
            }

            return new StrategyEngineResult(StrategyEngineStatus.Fallback, null, reason);
        }
    }
}
