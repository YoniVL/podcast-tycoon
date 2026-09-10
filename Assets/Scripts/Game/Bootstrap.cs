using System.Collections.Generic;
using PodcastTycoon.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace PodcastTycoon.Game
{
    /// <summary>
    /// Entry point. Owns the <see cref="Engine"/> and swaps the single on-screen panel
    /// between Setup → Week → Results → (Week …) with End screens for bankruptcy / the goal.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] int _rngSeed = 0; // 0 => random
        [SerializeField] StyleSheet _styleSheet;

        UIDocument _document;
        VisualElement _app;

        public Engine Engine { get; private set; }
        public Theme Theme { get; } = new Theme();

        readonly Queue<MilestoneEvent> _pendingMilestones = new Queue<MilestoneEvent>();
        bool _goalJustReached;

        void Start()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;
            if (_styleSheet != null && !root.styleSheets.Contains(_styleSheet))
                root.styleSheets.Add(_styleSheet);
            _app = root.Q<VisualElement>("app") ?? root;
            _app.AddToClassList("app");
            ShowSetup();
        }

        void Swap(VisualElement screen)
        {
            _app.Clear();
            _app.Add(screen);
        }

        // ------------------------------------------------------------------
        public void ShowSetup()
        {
            Swap(new SetupScreen(this).Build());
        }

        public void StartRun(RunSetup setup)
        {
            var cfg = new GameConfig();
            IRng rng = _rngSeed == 0 ? new SystemRng() : new SystemRng(_rngSeed);
            Engine = new Engine(setup, cfg, rng);
            Theme.Set(setup.ColourPrimary, setup.ColourSecondary);

            _pendingMilestones.Clear();
            _goalJustReached = false;

            Engine.MilestoneReached += m => _pendingMilestones.Enqueue(m);
            Engine.GoalReached += () => _goalJustReached = true;
            Engine.GameOver += _ => { /* handled after Publish returns */ };

            ShowWeek();
        }

        public void ShowWeek()
        {
            Engine.BeginWeek();
            Swap(new WeekScreen(this).Build());
        }

        public void Publish(ProductionPlan plan)
        {
            var result = Engine.Publish(plan);

            var milestones = new List<MilestoneEvent>();
            while (_pendingMilestones.Count > 0) milestones.Add(_pendingMilestones.Dequeue());

            Swap(new ResultsScreen(this, result, milestones, _goalJustReached).Build());
            _goalJustReached = false;
        }

        public void ContinueFromResults()
        {
            if (Engine.State.IsGameOver)
            {
                Swap(EndScreen.GameOver(this).Build());
                return;
            }
            ShowWeek();
        }

        public void RestartToSetup()
        {
            Engine = null;
            ShowSetup();
        }
    }
}
