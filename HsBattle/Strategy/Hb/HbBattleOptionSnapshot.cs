using System.Collections.Generic;

namespace HsBattle.Strategy.Hb
{
    internal sealed class HbBattleOptionSnapshot
    {
        public HbBattleOptionSnapshot()
        {
            Targets = new List<HbBattleTargetSnapshot>();
        }

        public int OptionIndex { get; set; } = -1;

        public int EntityId { get; set; } = -1;

        public int Cost { get; set; }

        public int Attack { get; set; }

        public int DamageAmount { get; set; }

        public int DrawCount { get; set; }

        public int SourceHealth { get; set; } = -1;

        public int TargetCount { get; set; }

        public bool RequiresTarget { get; set; }

        public bool CanTargetEnemyHero { get; set; }

        public bool CanLethal { get; set; }

        public bool IsPlayable { get; set; }

        public bool IsEndTurn { get; set; }

        public bool IsPass { get; set; }

        public bool IsEntityResolved { get; set; }

        public bool HasBattlecry { get; set; }

        public HbActionSupportKind SupportKind { get; set; } = HbActionSupportKind.UnsupportedComplex;

        public StrategyActionKind Kind { get; set; } = StrategyActionKind.Other;

        public string Description { get; set; } = string.Empty;

        public List<HbBattleTargetSnapshot> Targets { get; }
    }
}
