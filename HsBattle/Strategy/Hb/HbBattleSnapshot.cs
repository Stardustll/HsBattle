using System.Collections.Generic;

namespace HsBattle.Strategy.Hb
{
    internal sealed class HbBattleSnapshot
    {
        public HbBattleSnapshot()
        {
            Options = new List<HbBattleOptionSnapshot>();
            FriendlyBoard = new List<HbBattleEntitySnapshot>();
            EnemyBoard = new List<HbBattleEntitySnapshot>();
            HandCards = new List<HbBattleCardSnapshot>();
        }

        public StrategyMode StrategyMode { get; set; }

        public bool IsFriendlyTurn { get; set; }

        public int AvailableMana { get; set; }

        public bool HeroPowerUsable { get; set; }

        public int FriendlyHeroHealth { get; set; } = -1;

        public int EnemyHeroHealth { get; set; } = -1;

        public bool IsFriendlyHeroHealthKnown { get; set; }

        public bool IsEnemyHeroHealthKnown { get; set; }

        public List<HbBattleEntitySnapshot> FriendlyBoard { get; }

        public List<HbBattleEntitySnapshot> EnemyBoard { get; }

        public List<HbBattleCardSnapshot> HandCards { get; }

        public List<HbBattleOptionSnapshot> Options { get; }
    }
}
