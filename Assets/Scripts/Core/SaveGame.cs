using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace PodcastTycoon.Core
{
    /// <summary>What the load screen shows about a save without loading the whole run.</summary>
    public sealed class SaveHeader
    {
        public int Version;
        public string PodcastName;
        public string ClubName;
        public int Season;
        public int Week;                 // lifetime week the next episode will be
        public int Listeners;
        public int EpisodesPublished;
        public string SavedAtUtc;
    }

    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// Saves and restores a whole <see cref="Engine"/> run as JSON. A run is saved right after an
    /// episode is published (nothing mid-week is pending then), and loading resumes at the start
    /// of the next week — the week's match, offer and events are generated afresh by
    /// <see cref="Engine.BeginWeek"/>.
    ///
    /// The state is walked by reflection over fields (public and private, readonly included) rather
    /// than hand-written DTOs, so a new field on any state class is saved automatically. Things that
    /// are rebuilt rather than saved: delegates/events, the RNG, the config and engine back-references
    /// (those are wired up by constructing a fresh Engine first), and a pending event (never pending
    /// right after a publish).
    /// </summary>
    public static class SaveGame
    {
        public const int CurrentVersion = 1;

        sealed class FieldContractResolver : DefaultContractResolver
        {
            static bool Skip(Type t) =>
                typeof(Delegate).IsAssignableFrom(t) || typeof(IRng).IsAssignableFrom(t)
                || t == typeof(GameConfig) || t == typeof(Engine)
                || t == typeof(MatchSimulator) || t == typeof(Resolution)
                || t == typeof(GameEvent);

            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                var members = new List<MemberInfo>();
                for (var t = objectType; t != null && t != typeof(object); t = t.BaseType)
                {
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public
                                                  | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (!Skip(f.FieldType)) members.Add(f);
                }
                return members;
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var p = base.CreateProperty(member, memberSerialization);
                p.Readable = true;
                p.Writable = true;
                p.Ignored = false;
                return p;
            }

            protected override JsonObjectContract CreateObjectContract(Type objectType)
            {
                var c = base.CreateObjectContract(objectType);
                if (!objectType.IsAbstract && !objectType.IsInterface && !objectType.IsValueType
                    && objectType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                           null, Type.EmptyTypes, null) == null)
                {
                    // No parameterless constructor: every field comes from the JSON anyway.
                    c.OverrideCreator = null;
                    c.DefaultCreator = () => FormatterServices.GetUninitializedObject(objectType);
                }
                return c;
            }
        }

        static readonly FieldContractResolver Resolver = new FieldContractResolver();

        static JsonSerializer NewSerializer() => JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = Resolver,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Formatting = Formatting.None
        });

        const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static T GetField<T>(object o, string name) => (T)typeof(Engine).GetField(name, AnyField).GetValue(o);
        static void SetField(object o, string name, object v) => typeof(Engine).GetField(name, AnyField).SetValue(o, v);

        // ------------------------------------------------------------------
        public static string Serialize(Engine e)
        {
            var ser = NewSerializer();
            JToken Part(object o) => JToken.FromObject(o, ser);

            var parts = new JObject
            {
                ["state"] = Part(e.State),
                ["roster"] = Part(e.Roster),
                ["threads"] = Part(e.Threads),
                ["events"] = Part(e.Events),
                ["sponsors"] = Part(e.Sponsors),
                ["rivals"] = Part(e.Rivals),
                ["access"] = Part(e.Access),
                ["scoops"] = Part(e.Scoops),
                ["cards"] = Part(e.Cards),
                ["calendar"] = Part(e.Calendar),
                ["engine"] = new JObject
                {
                    ["seasonStartListeners"] = GetField<int>(e, "_seasonStartListeners"),
                    ["seasonStartRep"] = GetField<float>(e, "_seasonStartRep"),
                    ["weekSeed"] = GetField<int>(e, "_weekSeed")
                }
            };

            var header = new SaveHeader
            {
                Version = CurrentVersion,
                PodcastName = e.State.PodcastName,
                ClubName = e.State.ClubName,
                Season = e.State.Season,
                Week = e.State.GlobalWeek,
                Listeners = e.State.Listeners,
                EpisodesPublished = e.State.EpisodesPublished,
                SavedAtUtc = DateTime.UtcNow.ToString("o")
            };

            var root = new JObject { ["header"] = JObject.FromObject(header), ["parts"] = parts };
            return root.ToString(Formatting.None);
        }

        public static SaveHeader ReadHeader(string json)
        {
            try
            {
                var root = JObject.Parse(json);
                return root["header"].ToObject<SaveHeader>();
            }
            catch (Exception ex)
            {
                throw new SaveFormatException("The save file is damaged.", ex);
            }
        }

        public static Engine Deserialize(string json, IRng rng)
        {
            try
            {
                var root = JObject.Parse(json);
                var header = root["header"].ToObject<SaveHeader>();
                if (header.Version != CurrentVersion)
                    throw new SaveFormatException($"This save is from a different version ({header.Version}).");

                var parts = (JObject)root["parts"];
                var ser = NewSerializer();

                // A fresh engine supplies everything that isn't saved (config, RNG, event wiring);
                // the saved state is then poured over it.
                var stateJson = (JObject)parts["state"];
                var setup = new RunSetup
                {
                    PodcastName = (string)stateJson["PodcastName"],
                    ClubName = (string)stateJson["ClubName"],
                    ColourPrimary = (string)stateJson["ColourPrimary"],
                    ColourSecondary = (string)stateJson["ColourSecondary"],
                    Difficulty = (Difficulty)(int)stateJson["Difficulty"],
                    Modifiers = stateJson["Modifiers"].ToObject<RunModifiers>(ser)
                };
                var engine = new Engine(setup, new GameConfig(), rng);

                void Fill(string key, object target)
                {
                    using (var reader = parts[key].CreateReader()) ser.Populate(reader, target);
                }
                Fill("state", engine.State);
                Fill("roster", engine.Roster);
                Fill("threads", engine.Threads);
                Fill("events", engine.Events);
                Fill("sponsors", engine.Sponsors);
                Fill("rivals", engine.Rivals);
                Fill("access", engine.Access);
                Fill("scoops", engine.Scoops);
                Fill("cards", engine.Cards);
                Fill("calendar", engine.Calendar);

                var eng = (JObject)parts["engine"];
                SetField(engine, "_seasonStartListeners", (int)eng["seasonStartListeners"]);
                SetField(engine, "_seasonStartRep", (float)eng["seasonStartRep"]);
                if (eng["weekSeed"] != null) SetField(engine, "_weekSeed", (int)eng["weekSeed"]);   // saves from before this existed keep a fresh seed
                return engine;
            }
            catch (SaveFormatException) { throw; }
            catch (Exception ex)
            {
                throw new SaveFormatException("The save file is damaged.", ex);
            }
        }
    }
}
