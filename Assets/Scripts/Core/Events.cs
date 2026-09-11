using System;
using System.Collections.Generic;
using System.Linq;

namespace PodcastTycoon.Core
{
    public sealed class EventOption
    {
        public string Label;
        public string Outcome;                 // shown after the player picks it
        public Action<Engine> Apply;
    }

    public sealed class GameEvent
    {
        public string Id;
        public string Prompt;
        public string Detail;
        public List<EventOption> Options = new List<EventOption>();

        public Func<Engine, bool> CanFire = _ => true;
    }

    /// <summary>
    /// Interrupt events with a choice (spec §10). One fires occasionally at the start of a week and
    /// must be resolved before the player publishes.
    /// </summary>
    public sealed class EventManager
    {
        readonly IRng _rng;
        int _cooldown = 2;

        public GameEvent Pending { get; private set; }
        public string LastOutcome { get; private set; }

        readonly List<GameEvent> _pool;

        public EventManager(IRng rng)
        {
            _rng = rng;
            _pool = BuildPool();
        }

        public void MaybeFire(Engine engine, WeekContext ctx)
        {
            // An event from last week that was never resolved carries over.
            if (Pending != null) { ctx.PendingEvent = Pending; return; }

            LastOutcome = null;

            if (ctx.IsMatchless) return;
            if (_cooldown > 0) { _cooldown--; return; }
            if (_rng.NextDouble() > 0.40) return;

            var eligible = _pool.Where(e => e.CanFire(engine)).ToList();
            if (eligible.Count == 0) return;

            Pending = eligible[_rng.Range(0, eligible.Count)];
            ctx.PendingEvent = Pending;
        }

        public void Resolve(Engine engine, int optionIndex)
        {
            if (Pending == null) return;
            var opt = Pending.Options[MathX.ClampInt(optionIndex, 0, Pending.Options.Count - 1)];
            opt.Apply?.Invoke(engine);
            LastOutcome = opt.Outcome;
            engine.CurrentWeek.EventOutcome = opt.Outcome;
            Pending = null;
            _cooldown = 3;
        }

        // ------------------------------------------------------------------
        List<GameEvent> BuildPool()
        {
            IRng r = _rng;

            void Money(Engine e, float d) => e.State.Money += d;
            void Rep(Engine e, float d) => e.State.Reputation = MathX.Clamp(e.State.Reputation + d, 0f, 100f);
            void Social(Engine e, float d) => e.State.SocialReach = MathX.Clamp(e.State.SocialReach + d, 0f, 100f);
            void Cred(Engine e, float d) => e.State.Credibility = MathX.Clamp(e.State.Credibility + d, 0f, 100f);
            void ListenersPct(Engine e, float pct) =>
                e.State.Listeners = Math.Max(0, (int)Math.Round(e.State.Listeners * (1f + pct)));
            void Drift(Engine e, float perWeek, int weeks)
            {
                e.State.WeeklyListenerDrift = perWeek;
                e.State.WeeklyListenerDriftWeeks = weeks;
            }

            return new List<GameEvent>
            {
                new GameEvent
                {
                    Id = "tipoff",
                    Prompt = "A club insider slips you a story",
                    Detail = "Someone at the club has given you something before it's public. It's a scoop — but running unverified information is a risk.",
                    Options =
                    {
                        new EventOption
                        {
                            Label = "Run it now",
                            Outcome = "You broke it first. The numbers spiked — and the reaction was mixed.",
                            Apply = e =>
                            {
                                Social(e, 4f); ListenersPct(e, 0.02f); Rep(e, -3f);
                                if (r.NextDouble() < 0.3) { Rep(e, -5f); }
                            }
                        },
                        new EventOption
                        {
                            Label = "Sit on it until it's official",
                            Outcome = "You held it. The club noticed you can be trusted.",
                            Apply = e => Rep(e, 5f)
                        },
                        new EventOption
                        {
                            Label = "Tip off a national outlet",
                            Outcome = "You passed it up the chain. They owe you one now.",
                            Apply = e => { Rep(e, 1f); Social(e, 1.5f); }
                        }
                    }
                },

                new GameEvent
                {
                    Id = "poach",
                    CanFire = e => e.State.HasCoHost,
                    Prompt = "A bigger show wants your co-host",
                    Detail = "They've been offered more money and a bigger platform. Your call.",
                    Options =
                    {
                        new EventOption
                        {
                            Label = "Match the offer",
                            Outcome = "They're staying — but the wage bill just went up.",
                            Apply = e => e.State.CoHostWageBump += 40
                        },
                        new EventOption
                        {
                            Label = "Wish them well",
                            Outcome = "They're gone. Back to doing it solo.",
                            Apply = e => e.State.HasCoHost = false
                        },
                        new EventOption
                        {
                            Label = "Offer them a bigger creative role",
                            Outcome = "A small raise and a bigger say. They're in.",
                            Apply = e => { e.State.CoHostWageBump += 15; Rep(e, 2f); }
                        }
                    }
                },

                new GameEvent
                {
                    Id = "outofcontext",
                    Prompt = "A clip of you is going round without the context",
                    Detail = "Twelve seconds, stripped of everything around it, and people are angry.",
                    Options =
                    {
                        new EventOption
                        {
                            Label = "Address it properly and apologise",
                            Outcome = "You took the hit cleanly. The people who matter respected it.",
                            Apply = e => { Social(e, -2f); Rep(e, 4f); }
                        },
                        new EventOption
                        {
                            Label = "Double down",
                            Outcome = "You leaned in. The clip did numbers. Some listeners didn't come back.",
                            Apply = e => { Social(e, 5f); Rep(e, -6f); ListenersPct(e, -0.03f); }
                        },
                        new EventOption
                        {
                            Label = "Say nothing and let it pass",
                            Outcome = "You rode it out. Mostly.",
                            Apply = e => { if (r.NextDouble() < 0.35) Drift(e, -0.02f, 2); }
                        }
                    }
                },

                new GameEvent
                {
                    Id = "rival",
                    Prompt = "A slick new podcast launches on your patch",
                    Detail = "Same club, better microphones, a marketing budget. They're coming for your listeners.",
                    Options =
                    {
                        new EventOption
                        {
                            Label = "Make an episode taking them apart",
                            Outcome = "Petty, fun, and it worked — for now.",
                            Apply = e => { Social(e, 4f); Rep(e, -4f); ListenersPct(e, 0.03f); }
                        },
                        new EventOption
                        {
                            Label = "Reach out about a crossover",
                            Outcome = "A collab episode. Both audiences had a look at the other.",
                            Apply = e => { Social(e, 2f); ListenersPct(e, 0.04f); }
                        },
                        new EventOption
                        {
                            Label = "Ignore them and keep your head down",
                            Outcome = "You said nothing. They chipped away at your numbers for a few weeks.",
                            Apply = e => Drift(e, -0.02f, 3)
                        }
                    }
                },

                new GameEvent
                {
                    Id = "liveshow",
                    Prompt = "A venue offers you a one-off live recording",
                    Detail = "A room full of listeners, a proper night out — and a week where the episode has to fit around it.",
                    Options =
                    {
                        new EventOption
                        {
                            Label = "Book it",
                            Outcome = "A packed room and a great night. This week's episode had to give way a little.",
                            Apply = e =>
                            {
                                float cost = Math.Max(50f, e.State.Listeners * 0.02f);
                                Money(e, -cost);
                                ListenersPct(e, 0.05f);
                                Social(e, 5f);
                                e.State.PrepPenaltyThisWeek += 2;
                            }
                        },
                        new EventOption
                        {
                            Label = "Not this time",
                            Outcome = "You passed. Maybe when the show's bigger.",
                            Apply = _ => { }
                        }
                    }
                },

                new GameEvent
                {
                    Id = "gear",
                    Prompt = "Your microphone packs in mid-recording",
                    Detail = "Half an episode done and the audio just died.",
                    Options =
                    {
                        new EventOption
                        {
                            Label = "Replace it straight away",
                            Outcome = "Sorted. An unplanned expense, but the show sounds right.",
                            Apply = e => Money(e, e.State.HasGear(Gear.XlrMic) ? -60f : -120f)
                        },
                        new EventOption
                        {
                            Label = "Finish it on the laptop mic",
                            Outcome = "You got it out. It sounded like you got it out on a laptop mic.",
                            Apply = e => { ListenersPct(e, -0.01f); Rep(e, -1f); }
                        }
                    }
                },

                new GameEvent
                {
                    Id = "advertiser",
                    Prompt = "A local business asks about advertising on the show",
                    Detail = "Nothing formal yet — just a feeler about whether you'd read an ad.",
                    Options =
                    {
                        new EventOption
                        {
                            Label = "Take the meeting",
                            Outcome = "A friendly chat and a small thank-you for your time. They'll be in touch.",
                            Apply = e => Money(e, 60f)
                        },
                        new EventOption
                        {
                            Label = "Not yet — keep it clean",
                            Outcome = "You're not ready to run ads. The purists respected it.",
                            Apply = e => Rep(e, 1f)
                        }
                    }
                },

                // --- discourse & beef (spec §13) ---
                new GameEvent
                {
                    Id = "beef",
                    Prompt = "A rival podcast keeps taking shots at you",
                    Detail = "They've been digging at you for weeks — on air, in their socials. Everyone's noticed.",
                    CanFire = e => e.State.SocialReach >= 20f,
                    Options =
                    {
                        new EventOption
                        {
                            Label = "Call a truce, cross-promote instead",
                            Outcome = "You reached out. A joint episode did both audiences good.",
                            Apply = e => { Social(e, 3f); ListenersPct(e, 0.02f); Money(e, 40f); }
                        },
                        new EventOption
                        {
                            Label = "Escalate — go after them on air",
                            Outcome = "You leaned all the way in. Huge numbers this week — but some of what showed up isn't the audience you wanted.",
                            Apply = e =>
                            {
                                Social(e, 10f); Cred(e, -3f); ListenersPct(e, 0.03f);
                                Drift(e, -0.015f, 3);   // a bad-faith audience that churns hard for a few weeks
                            }
                        },
                        new EventOption
                        {
                            Label = "Ignore it and let it burn out",
                            Outcome = "You said nothing. Mostly it worked.",
                            Apply = e => { if (r.NextDouble() < 0.3) Social(e, -3f); }
                        }
                    }
                },

                new GameEvent
                {
                    Id = "libel",
                    Prompt = "A guest says something legally dicey, live",
                    Detail = "An unverified, pretty serious claim about a manager, said flat-out on air. It's already clipped and spreading.",
                    CanFire = e => e.State.AccessTier >= 2 || e.State.HasCrew(Crew.Booker),
                    Options =
                    {
                        new EventOption
                        {
                            Label = "Retract and apologise",
                            Outcome = "You cut it, apologised, moved on. Costs you a little credibility, but it's done.",
                            Apply = e => Cred(e, -2f)
                        },
                        new EventOption
                        {
                            Label = "Stand by it",
                            Outcome = "You didn't back down. Could go either way.",
                            Apply = e =>
                            {
                                if (r.NextDouble() < 0.45)
                                {
                                    Money(e, -Math.Max(80f, e.State.Money * 0.08f));
                                    Cred(e, -6f);
                                }
                                else
                                {
                                    Social(e, 6f); Rep(e, 1f);
                                }
                            }
                        }
                    }
                },

                new GameEvent
                {
                    Id = "discourse",
                    Prompt = "Something you said is going viral — for the wrong reasons",
                    Detail = "A clip's been taken out of context and it's everywhere today.",
                    CanFire = e => e.State.SocialReach >= 55f,
                    Options =
                    {
                        new EventOption
                        {
                            Label = "Address it head-on, next episode",
                            Outcome = "You explained yourself properly. It settled down.",
                            Apply = e => { Cred(e, 2f); Social(e, -2f); }
                        },
                        new EventOption
                        {
                            Label = "Say nothing and let it pass",
                            Outcome = "You rode it out.",
                            Apply = e => { if (r.NextDouble() < 0.4) { Cred(e, -3f); ListenersPct(e, -0.01f); } }
                        },
                        new EventOption
                        {
                            Label = "Lean into the chaos",
                            Outcome = "You made it worse on purpose. It's doing huge numbers now.",
                            Apply = e => { Social(e, 8f); Cred(e, -4f); Money(e, 30f); }
                        }
                    }
                }
            };
        }
    }
}
