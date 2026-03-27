namespace HsBattle.Strategy.Hb
{
    internal sealed class HbBattleTargetSnapshot
    {
        public int EntityId { get; set; } = -1;

        public int Attack { get; set; }

        public int Health { get; set; } = -1;

        public int MaxHealth { get; set; } = -1;

        public int MissingHealth { get; set; }

        public bool IsDamaged { get; set; }

        public bool IsResolved { get; set; }

        public bool IsEnemyHero { get; set; }

        public bool IsEnemyCharacter { get; set; }

        public bool IsFriendlyHero { get; set; }

        public bool IsFriendlyCharacter { get; set; }

        public bool HasTaunt { get; set; }

        public bool HasDivineShield { get; set; }

        public bool IsStealthed { get; set; }

        public bool IsFrozen { get; set; }
    }
}
