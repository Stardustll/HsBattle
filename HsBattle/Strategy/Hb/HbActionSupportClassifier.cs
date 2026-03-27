namespace HsBattle.Strategy.Hb
{
    internal sealed class HbActionSupportClassifier
    {
        public HbActionSupportKind Classify(HbBattleOptionSnapshot option)
        {
            if (option == null || !option.IsPlayable)
            {
                return HbActionSupportKind.UnsupportedComplex;
            }

            if (option.Kind == StrategyActionKind.Attack)
            {
                return HbActionSupportKind.SupportedExact;
            }

            if (option.Kind == StrategyActionKind.HeroPower)
            {
                return HbActionSupportKind.SupportedExact;
            }

            if (option.Kind == StrategyActionKind.PlayCard)
            {
                if (option.HasBattlecry || option.DrawCount > 0)
                {
                    return HbActionSupportKind.UnsupportedComplex;
                }

                return HbActionSupportKind.SupportedExact;
            }

            return HbActionSupportKind.UnsupportedComplex;
        }
    }
}
