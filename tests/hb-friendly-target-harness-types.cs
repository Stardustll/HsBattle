using System.Collections.Generic;

namespace HsBattle.Strategy
{
    public enum StrategyActionKind
    {
        Choice,
        PlayCard,
        HeroPower,
        Attack,
        Pass,
        EndTurn,
        Other
    }

    public enum StrategyMode
    {
        Legacy = 0,
        HbFrameworkExperimental = 1
    }
}

namespace HsBattle.Strategy.Hb
{
    public enum HbActionSupportKind
    {
        SupportedExact,
        UnsupportedComplex
    }

    public sealed class HbBattleSnapshot
    {
        private readonly List<HbBattleOptionSnapshot> _options;

        public HbBattleSnapshot()
        {
            _options = new List<HbBattleOptionSnapshot>();
        }

        public StrategyMode StrategyMode { get; set; }
        public bool IsFriendlyTurn { get; set; }
        public int FriendlyHeroHealth { get; set; }
        public int EnemyHeroHealth { get; set; }
        public bool IsFriendlyHeroHealthKnown { get; set; }
        public bool IsEnemyHeroHealthKnown { get; set; }
        public List<HbBattleOptionSnapshot> Options
        {
            get
            {
                return _options;
            }
        }
    }

    public sealed class HbBattleOptionSnapshot
    {
        private readonly List<HbBattleTargetSnapshot> _targets;

        public HbBattleOptionSnapshot()
        {
            _targets = new List<HbBattleTargetSnapshot>();
        }

        public int Attack { get; set; }
        public int DamageAmount { get; set; }
        public int SourceHealth { get; set; }
        public StrategyActionKind Kind { get; set; }
        public HbActionSupportKind SupportKind { get; set; }
        public bool CanLethal { get; set; }
        public bool IsPlayable { get; set; }
        public bool IsEndTurn { get; set; }
        public bool IsPass { get; set; }
        public bool RequiresTarget { get; set; }
        public int TargetCount { get; set; }
        public bool CanTargetEnemyHero { get; set; }
        public int Cost { get; set; }
        public bool IsEntityResolved { get; set; }
        public int OptionIndex { get; set; }
        public int EntityId { get; set; }
        public List<HbBattleTargetSnapshot> Targets
        {
            get
            {
                return _targets;
            }
        }
    }

    public sealed class HbBattleTargetSnapshot
    {
        public int EntityId { get; set; }
        public int Attack { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int MissingHealth { get; set; }
        public bool IsDamaged { get; set; }
        public bool IsResolved { get; set; }
        public bool IsEnemyHero { get; set; }
        public bool IsEnemyCharacter { get; set; }
        public bool IsFriendlyHero { get; set; }
        public bool IsFriendlyCharacter { get; set; }
    }

    public sealed class HbMulliganSnapshot
    {
        private readonly List<HbMulliganCardSnapshot> _cards;

        public HbMulliganSnapshot()
        {
            _cards = new List<HbMulliganCardSnapshot>();
        }

        public List<HbMulliganCardSnapshot> Cards
        {
            get
            {
                return _cards;
            }
        }
    }

    public sealed class HbMulliganCardSnapshot
    {
        public int Cost { get; set; }
        public bool IsCostKnown { get; set; }
        public bool IsCoin { get; set; }
        public bool IsCoinKnown { get; set; }
    }
}
