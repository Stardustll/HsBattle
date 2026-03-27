namespace HsBattle.Strategy.Hb
{
    internal sealed class HbMulliganCardSnapshot
    {
        public int Index { get; set; } = -1;

        public int EntityId { get; set; } = -1;

        public int Cost { get; set; } = -1;

        public bool IsResolved { get; set; }

        public bool IsCostKnown { get; set; }

        public bool IsCoin { get; set; }

        public bool IsCoinKnown { get; set; }
    }
}
