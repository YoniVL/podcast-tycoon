using System;

namespace PodcastTycoon.Core
{
    /// <summary>Light match model (spec §4). Produces an outcome, a scoreline, and a surprise rating.</summary>
    public sealed class MatchSimulator
    {
        readonly GameConfig _cfg;

        public MatchSimulator(GameConfig cfg) { _cfg = cfg; }

        public MatchResult Simulate(float teamStrength, float keyOut, Fixture fx, IRng rng)
        {
            float d = (teamStrength - keyOut) - fx.OpponentStrength
                      + (fx.Home ? _cfg.HomeAdvantage : -_cfg.HomeAdvantage);

            float pWin = MathX.Clamp(_cfg.MatchProbBase + _cfg.MatchProbSlope * d, _cfg.MatchProbMin, _cfg.MatchProbMax);
            float pLoss = MathX.Clamp(_cfg.MatchProbBase - _cfg.MatchProbSlope * d, _cfg.MatchProbMin, _cfg.MatchProbMax);
            float pDraw = Math.Max(0.10f, 1f - pWin - pLoss);

            float sum = pWin + pDraw + pLoss;
            pWin /= sum; pDraw /= sum; pLoss /= sum;

            double roll = rng.NextDouble();
            MatchOutcome outcome = roll < pWin
                ? MatchOutcome.Win
                : roll < pWin + pDraw ? MatchOutcome.Draw : MatchOutcome.Loss;

            var result = new MatchResult
            {
                Outcome = outcome,
                WinProbability = pWin,
                LossProbability = pLoss,
                Surprise = Classify(outcome, pWin, pLoss)
            };

            Scoreline(result, outcome, d, rng);
            return result;
        }

        Surprise Classify(MatchOutcome outcome, float pWin, float pLoss)
        {
            bool expectedWin = pWin > _cfg.ExpectationThreshold;
            bool expectedLoss = pLoss > _cfg.ExpectationThreshold;

            if (expectedWin)
            {
                if (outcome == MatchOutcome.Win) return Surprise.Par;
                if (outcome == MatchOutcome.Draw) return Surprise.Poor;
                return Surprise.Disaster;
            }
            if (expectedLoss)
            {
                if (outcome == MatchOutcome.Win) return Surprise.Heroic;
                if (outcome == MatchOutcome.Draw) return Surprise.Good;
                return Surprise.Par;
            }
            // toss-up
            if (outcome == MatchOutcome.Win) return Surprise.Good;
            if (outcome == MatchOutcome.Draw) return Surprise.Par;
            return Surprise.Poor;
        }

        void Scoreline(MatchResult result, MatchOutcome outcome, float d, IRng rng)
        {
            // Base goals for each side scale gently with the strength gap.
            float forExpected = MathX.Clamp(1.3f + d * 1.4f, 0.3f, 3.6f);
            float againstExpected = MathX.Clamp(1.3f - d * 1.4f, 0.3f, 3.6f);

            int gf = Math.Min(5, Poissonish(forExpected, rng));
            int ga = Math.Min(5, Poissonish(againstExpected, rng));

            // Nudge the scoreline to agree with the outcome we already rolled.
            switch (outcome)
            {
                case MatchOutcome.Win:
                    if (gf <= ga) gf = ga + 1;
                    break;
                case MatchOutcome.Loss:
                    if (ga <= gf) ga = gf + 1;
                    break;
                case MatchOutcome.Draw:
                    ga = gf;
                    break;
            }

            result.GoalsFor = gf;
            result.GoalsAgainst = ga;
        }

        static int Poissonish(float mean, IRng rng)
        {
            // Cheap approximation: sum of a few weighted coin flips around the mean.
            double t = Math.Exp(-mean);
            int k = 0;
            double p = 1.0;
            do
            {
                k++;
                p *= rng.NextDouble();
            } while (p > t && k < 8);
            return k - 1;
        }
    }
}
