using System;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// How well-off a caller is. Richer marks pay far more but are warier, so their AI
    /// persona pushes back harder. A tier is rolled per call and scales that scam's reward.
    /// </summary>
    public sealed class TargetTier
    {
        public readonly string Id;
        public readonly string Label;
        public readonly double RewardMultiplier;
        public readonly double Weight;        // relative chance of being rolled
        /// <summary>Appended to the caller persona so the AI plays the wariness.</summary>
        public readonly string PersonaHint;

        private TargetTier(string id, string label, double multiplier, double weight, string personaHint)
        {
            Id = id; Label = label; RewardMultiplier = multiplier; Weight = weight; PersonaHint = personaHint;
        }

        public static readonly TargetTier Broke = new TargetTier("broke", "Broke", 0.4, 3.0,
            "You have very little money and are easily rattled, so you tend to go along with instructions quickly, but there is not much to take.");
        public static readonly TargetTier Regular = new TargetTier("regular", "Regular", 1.0, 5.0,
            "You are an ordinary person of average means and average caution.");
        public static readonly TargetTier WellOff = new TargetTier("welloff", "Well-off", 2.2, 2.0,
            "You are comfortably wealthy and a little skeptical. You ask follow-up questions and need a convincing explanation before you act.");
        public static readonly TargetTier Whale = new TargetTier("whale", "Whale", 5.0, 0.6,
            "You are very wealthy and quite sharp. You are slow to trust, you probe for inconsistencies, and you will hang up if anything feels off, but if genuinely convinced the amounts at stake are large.");

        public static readonly TargetTier[] All = { Broke, Regular, WellOff, Whale };

        public static TargetTier ById(string id)
        {
            foreach (var tier in All) if (tier.Id == id) return tier;
            return Regular;
        }

        public static TargetTier Roll(Random rng)
        {
            double total = 0;
            foreach (var tier in All) total += tier.Weight;
            double roll = rng.NextDouble() * total;
            foreach (var tier in All)
            {
                roll -= tier.Weight;
                if (roll <= 0) return tier;
            }
            return Regular;
        }
    }
}
