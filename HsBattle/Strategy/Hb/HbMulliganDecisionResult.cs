using System.Collections.Generic;

namespace HsBattle.Strategy.Hb
{
    internal sealed class HbMulliganDecisionResult
    {
        public HbMulliganDecisionResult()
        {
            KeepIndices = new List<int>();
            ReplaceIndices = new List<int>();
        }

        public bool ShouldFallbackToKeepAll { get; set; }

        public string Reason { get; set; } = string.Empty;

        public List<int> KeepIndices { get; }

        public List<int> ReplaceIndices { get; }
    }
}
