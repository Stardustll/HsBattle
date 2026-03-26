namespace HsBattle.Strategy
{
    internal sealed class StrategyActionPlan
    {
        public int OptionIndex { get; set; }

        public int SubOptionIndex { get; set; } = -1;

        public int TargetId { get; set; } = -1;

        public int Position { get; set; }

        public int Score { get; set; }

        public string Description { get; set; }

        public StrategyActionKind Kind { get; set; }
    }
}
