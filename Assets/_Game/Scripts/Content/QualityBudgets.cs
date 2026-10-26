namespace UtsavNights.Content
{
    /// <summary>
    /// Per-tier scene budgets from PRD 14 (hypotheses until measured on the budget phone). One place,
    /// read by the content linter and the runtime quality governor.
    /// </summary>
    public static class QualityBudgets
    {
        /// <summary>Total live particles the whole scene may hold.</summary>
        public static int MaxSceneParticles(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.Low: return 800;
                case QualityTier.Mid: return 1500;
                default: return 3000;
            }
        }

        /// <summary>Simultaneous audio voices.</summary>
        public static int MaxVoices(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.Low: return 16;
                case QualityTier.Mid: return 24;
                default: return 32;
            }
        }

        /// <summary>Flash lights per second, all tiers (PRD 6.2).</summary>
        public const int MaxFlashesPerSecond = 3;
    }
}
