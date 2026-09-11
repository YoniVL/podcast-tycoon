using System.Collections.Generic;

namespace PodcastTycoon.Core
{
    /// <summary>The five upgrade tracks (spec §20). Buy in order — each tier gates on money and
    /// supersedes the one before it, rather than stacking with it.</summary>
    public enum UpgradeTrack { Set, Audio, Post, Studio, Distribution }

    /// <summary>One rung on a track's ladder. Effect fields are absolute values at this tier
    /// (they replace the tier below, they don't add to it).</summary>
    public sealed class UpgradeTier
    {
        public UpgradeTrack Track;
        public int Level;              // 1..4
        public string Name;
        public string Effect;          // human-readable, for the UI
        public int Cost;
        public int Monthly;

        public float QualityFloorBonus;
        public float ComedyAppealBonus;
        public float AppealMult = 1f;
        public float MicQuality = 0.20f;
        public float EditSkill = 0.15f;
        public float ChurnMult = 1f;
        public bool AudioGlitchProof;
        public int PrepBonus;
        public float ReachMult = 1f;
        public float AdRateMult = 1f;
        public float FollowerPassivePct;
        public float SocialDecayMult = 1f;
        public float CorePassivePct;
    }

    public static class UpgradeCatalog
    {
        // Tier 0 = not bought, the baseline every track starts at.
        static readonly Dictionary<UpgradeTrack, UpgradeTier> Baseline = new Dictionary<UpgradeTrack, UpgradeTier>
        {
            [UpgradeTrack.Set] = new UpgradeTier { Track = UpgradeTrack.Set, Level = 0, Name = "Bare corner", Effect = "Nothing behind you but a wall." },
            [UpgradeTrack.Audio] = new UpgradeTier { Track = UpgradeTrack.Audio, Level = 0, Name = "Laptop mic", Effect = "It's a laptop mic.", MicQuality = 0.20f },
            [UpgradeTrack.Post] = new UpgradeTier { Track = UpgradeTrack.Post, Level = 0, Name = "No editing", Effect = "Whatever comes out of the recorder.", EditSkill = 0.15f },
            [UpgradeTrack.Studio] = new UpgradeTier { Track = UpgradeTrack.Studio, Level = 0, Name = "Spare room", Effect = "A room you happen to have." },
            [UpgradeTrack.Distribution] = new UpgradeTier { Track = UpgradeTrack.Distribution, Level = 0, Name = "RSS only", Effect = "However people happen to find it." },
        };

        static readonly Dictionary<UpgradeTrack, List<UpgradeTier>> Tracks = Build();

        static Dictionary<UpgradeTrack, List<UpgradeTier>> Build()
        {
            var d = new Dictionary<UpgradeTrack, List<UpgradeTier>>();

            d[UpgradeTrack.Set] = new List<UpgradeTier>
            {
                new UpgradeTier { Track = UpgradeTrack.Set, Level = 1, Name = "Plants & posters",
                    Effect = "Lifts the quality floor a little.", Cost = 120, QualityFloorBonus = 0.03f },
                new UpgradeTier { Track = UpgradeTrack.Set, Level = 2, Name = "Branded backdrop",
                    Effect = "A proper backdrop. Better floor, clips look sharper.", Cost = 300, Monthly = 10,
                    QualityFloorBonus = 0.06f },
                new UpgradeTier { Track = UpgradeTrack.Set, Level = 3, Name = "Green screen",
                    Effect = "Visual bits land better, and clips travel further.", Cost = 700, Monthly = 20,
                    QualityFloorBonus = 0.08f, ComedyAppealBonus = 0.15f },
                new UpgradeTier { Track = UpgradeTrack.Set, Level = 4, Name = "Custom built set",
                    Effect = "Looks like a real show. Floor and appeal both up.", Cost = 1800, Monthly = 40,
                    QualityFloorBonus = 0.10f, ComedyAppealBonus = 0.15f, AppealMult = 1.04f },
            };

            d[UpgradeTrack.Audio] = new List<UpgradeTier>
            {
                new UpgradeTier { Track = UpgradeTrack.Audio, Level = 1, Name = "XLR microphone",
                    Effect = "Raises the quality ceiling.", Cost = 150, MicQuality = 0.35f },
                new UpgradeTier { Track = UpgradeTrack.Audio, Level = 2, Name = "Interface & treatment",
                    Effect = "Cleaner sound, a little less churn.", Cost = 350, Monthly = 8,
                    MicQuality = 0.45f, ChurnMult = 0.97f },
                new UpgradeTier { Track = UpgradeTrack.Audio, Level = 3, Name = "Treated room",
                    Effect = "No more mid-recording audio disasters.", Cost = 800, Monthly = 15,
                    MicQuality = 0.55f, ChurnMult = 0.95f, AudioGlitchProof = true },
                new UpgradeTier { Track = UpgradeTrack.Audio, Level = 4, Name = "Broadcast booth",
                    Effect = "Studio-grade, guests sound as good as you do.", Cost = 2000, Monthly = 35,
                    MicQuality = 0.65f, ChurnMult = 0.90f, AudioGlitchProof = true },
            };

            d[UpgradeTrack.Post] = new List<UpgradeTier>
            {
                new UpgradeTier { Track = UpgradeTrack.Post, Level = 1, Name = "Editing software",
                    Effect = "A real edit instead of a raw dump.", Cost = 200, EditSkill = 0.30f },
                new UpgradeTier { Track = UpgradeTrack.Post, Level = 2, Name = "Editing suite",
                    Effect = "Faster, tighter edits — every segment takes a little less effort.", Cost = 500, Monthly = 12,
                    EditSkill = 0.38f },
                new UpgradeTier { Track = UpgradeTrack.Post, Level = 3, Name = "Editor workstation",
                    Effect = "Proper clip output — a slow trickle of new followers every week.", Cost = 1200, Monthly = 25,
                    EditSkill = 0.45f, FollowerPassivePct = 0.006f },
                new UpgradeTier { Track = UpgradeTrack.Post, Level = 4, Name = "Auto clip pipeline",
                    Effect = "Clips practically make themselves. Social reach fades half as fast.", Cost = 2800, Monthly = 50,
                    EditSkill = 0.50f, FollowerPassivePct = 0.012f, SocialDecayMult = 0.5f },
            };

            d[UpgradeTrack.Studio] = new List<UpgradeTier>
            {
                new UpgradeTier { Track = UpgradeTrack.Studio, Level = 1, Name = "Rented unit",
                    Effect = "+2 prep points a week, a little more reach.", Cost = 900, Monthly = 30,
                    PrepBonus = 2, ReachMult = 1.08f },
                new UpgradeTier { Track = UpgradeTrack.Studio, Level = 2, Name = "Proper studio",
                    Effect = "+1 more prep point, a real base of operations.", Cost = 2200, Monthly = 55,
                    PrepBonus = 3, ReachMult = 1.08f },
                new UpgradeTier { Track = UpgradeTrack.Studio, Level = 3, Name = "Second room",
                    Effect = "Room for guests without disrupting the main setup.", Cost = 3200, Monthly = 70,
                    PrepBonus = 4, ReachMult = 1.10f },
                new UpgradeTier { Track = UpgradeTrack.Studio, Level = 4, Name = "Guest suite",
                    Effect = "A proper facility. More prep, more reach.", Cost = 4500, Monthly = 90,
                    PrepBonus = 5, ReachMult = 1.12f },
            };

            d[UpgradeTrack.Distribution] = new List<UpgradeTier>
            {
                new UpgradeTier { Track = UpgradeTrack.Distribution, Level = 1, Name = "Website",
                    Effect = "A proper home for the show. Slightly better ad rate.", Cost = 100, AdRateMult = 1.05f },
                new UpgradeTier { Track = UpgradeTrack.Distribution, Level = 2, Name = "App",
                    Effect = "Push notifications bring the core back reliably.", Cost = 600, Monthly = 15,
                    AdRateMult = 1.12f, CorePassivePct = 0.003f },
                new UpgradeTier { Track = UpgradeTrack.Distribution, Level = 3, Name = "Media network deal",
                    Effect = "A real outlet behind you — better rates, more legitimacy.", Cost = 1500, Monthly = 30,
                    AdRateMult = 1.20f, CorePassivePct = 0.004f },
                new UpgradeTier { Track = UpgradeTrack.Distribution, Level = 4, Name = "Own the feed",
                    Effect = "You own the whole pipeline now.", Cost = 3500, Monthly = 60,
                    AdRateMult = 1.30f, CorePassivePct = 0.006f },
            };

            return d;
        }

        public static IReadOnlyList<UpgradeTier> Ladder(UpgradeTrack track) => Tracks[track];

        /// <summary>Tier 0 (baseline) through the top of the ladder.</summary>
        public static UpgradeTier Get(UpgradeTrack track, int level)
        {
            if (level <= 0) return Baseline[track];
            var ladder = Tracks[track];
            return level <= ladder.Count ? ladder[level - 1] : ladder[ladder.Count - 1];
        }

        public static int TierOf(GameState st, UpgradeTrack track) => track switch
        {
            UpgradeTrack.Set => st.SetTier,
            UpgradeTrack.Audio => st.AudioTier,
            UpgradeTrack.Post => st.PostTier,
            UpgradeTrack.Studio => st.StudioTier,
            _ => st.DistributionTier
        };

        public static void SetTierOf(GameState st, UpgradeTrack track, int level)
        {
            switch (track)
            {
                case UpgradeTrack.Set: st.SetTier = level; break;
                case UpgradeTrack.Audio: st.AudioTier = level; break;
                case UpgradeTrack.Post: st.PostTier = level; break;
                case UpgradeTrack.Studio: st.StudioTier = level; break;
                default: st.DistributionTier = level; break;
            }
        }

        public static UpgradeTier Current(GameState st, UpgradeTrack track) => Get(track, TierOf(st, track));

        /// <summary>The next tier to buy, or null if the track is maxed out.</summary>
        public static UpgradeTier Next(GameState st, UpgradeTrack track)
        {
            int next = TierOf(st, track) + 1;
            var ladder = Tracks[track];
            return next <= ladder.Count ? ladder[next - 1] : null;
        }

        public static int TotalMonthlyUpkeep(GameState st)
        {
            int sum = 0;
            foreach (UpgradeTrack t in System.Enum.GetValues(typeof(UpgradeTrack)))
                sum += Current(st, t).Monthly;
            return sum;
        }
    }
}
