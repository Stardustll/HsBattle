namespace HsBattle.Strategy.Hb
{
    internal sealed class HbBattleEntitySnapshot
    {
        public int EntityId { get; set; } = -1;

        public bool IsFriendly { get; set; }

        public bool IsHero { get; set; }

        public bool IsMinion { get; set; }

        public int Attack { get; set; }

        public int Health { get; set; } = -1;

        public int MaxHealth { get; set; } = -1;

        public bool CanAttack { get; set; }

        public bool HasAttacked { get; set; }

        public bool HasTaunt { get; set; }

        public bool HasDivineShield { get; set; }

        public bool IsStealthed { get; set; }

        public bool IsFrozen { get; set; }
    }
}
