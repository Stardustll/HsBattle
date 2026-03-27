using System.Collections.Generic;

namespace HsBattle.Strategy.Hb
{
    internal sealed class HbMulliganSnapshot
    {
        public HbMulliganSnapshot()
        {
            Cards = new List<HbMulliganCardSnapshot>();
        }

        public bool IsActive { get; set; }

        public bool IsIntroActive { get; set; }

        public bool WaitingForUserInput { get; set; }

        public bool RequiresConfirmation { get; set; }

        public bool HasUnknownCards { get; set; }

        public List<HbMulliganCardSnapshot> Cards { get; }
    }
}
