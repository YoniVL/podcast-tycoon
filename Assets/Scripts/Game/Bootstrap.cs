using System.Collections.Generic;
using PodcastTycoon.Core;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField] PanelSettings _panelSettings;

        UIDocument _document;
        VisualElement _app;

        public Engine Engine { get; private set; }
        public Theme Theme { get; } = new Theme();

        readonly Queue<MilestoneEvent> _pendingMilestones = new Queue<MilestoneEvent>();
        bool _goalJustReached;
        WeekScreen _weekScreen;

        enum View { Title, Menu, Setup, Week, Results, End }
        View _screen;
        VisualElement _dialog;
        Label _dialogTitle, _dialogBody;
        Button _dialogYes, _dialogNo;
        System.Action _dialogOnYes;

        void Start()
        {
            Application.targetFrameRate = 60;
            _document = GetComponent<UIDocument>();
            if (_document.panelSettings == null && _panelSettings != null)
                _document.panelSettings = _panelSettings;
            var root = _document.rootVisualElement;
            if (_styleSheet != null && !root.styleSheets.Contains(_styleSheet))
                root.styleSheets.Add(_styleSheet);
            _app = root.Q<VisualElement>("app") ?? root;
            _app.AddToClassList("app");
            SetupSafeArea(root);
            SetupTapTooltips(root);
            SetupDialog(root);
            Theme.Set("#2F6DB5", "#F2C14E");
            ShowTitle();
        }

        void Update()
        {
            // The Android back button arrives as Escape.
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) OnBack();
        }

        void OnBack()
        {
            if (_dialog.style.display == DisplayStyle.Flex) { CloseDialog(); return; }
            if (_tip.style.display == DisplayStyle.Flex) { HideTip(); return; }
            switch (_screen)
            {
                case View.Menu: ShowTitle(); return;
                case View.Setup: ShowMenu(); return;
                case View.Week:
                    if (_weekScreen != null && _weekScreen.HandleBack()) return;
                    break;
            }
            string body = _screen == View.Week
                ? "Your run is saved after every episode. What you've done since the last one is lost."
                : "Your run is saved after every episode.";
            ShowDialog("Leave the game?", body, "Keep playing", "Quit", Application.Quit);
        }

        void SetupDialog(VisualElement root)
        {
            _dialog = new VisualElement();
            _dialog.AddToClassList("modal-backdrop");
            _dialog.style.display = DisplayStyle.None;
            var box = Ui.Box("panel", "modal");
            _dialogTitle = Ui.Text("", "h2");
            _dialogBody = Ui.Wrapping("", "body", "dim");
            _dialogNo = Ui.Btn("", CloseDialog, "btn-primary");
            _dialogYes = Ui.Btn("", () => { var go = _dialogOnYes; CloseDialog(); go?.Invoke(); }, "btn-ghost");
            box.Add(_dialogTitle);
            box.Add(_dialogBody);
            box.Add(_dialogNo);
            box.Add(_dialogYes);
            _dialog.Add(box);
            root.Add(_dialog);
        }

        /// <summary>A two-button confirm: the safe option (primary) closes, the other one acts.</summary>
        void ShowDialog(string title, string body, string safeLabel, string actLabel, System.Action onAct)
        {
            _dialogTitle.text = title;
            _dialogBody.text = body;
            _dialogNo.text = safeLabel;
            _dialogYes.text = actLabel;
            _dialogOnYes = onAct;
            _dialog.style.display = DisplayStyle.Flex;
            _dialog.BringToFront();
        }

        void CloseDialog() => _dialog.style.display = DisplayStyle.None;

        // --- phone support ---------------------------------------------------

        /// <summary>Keep the UI out from under the notch / punch-hole and the gesture bar.
        /// Screen.safeArea is in physical pixels; the panel works in scaled logical units.</summary>
        void SetupSafeArea(VisualElement root)
        {
            void Apply()
            {
                if (Screen.width <= 0 || root.resolvedStyle.width <= 0 || float.IsNaN(root.resolvedStyle.width)) return;
                var safe = Screen.safeArea;
                float k = root.resolvedStyle.width / Screen.width;
                _app.style.paddingTop = (Screen.height - (safe.y + safe.height)) * k;
                _app.style.paddingBottom = safe.y * k;
                _app.style.paddingLeft = safe.x * k;
                _app.style.paddingRight = (Screen.width - (safe.x + safe.width)) * k;
            }
            root.RegisterCallback<GeometryChangedEvent>(_ => Apply());
        }

        VisualElement _tip;
        Label _tipText;
        IVisualElementScheduledItem _tipHide;

        /// <summary>There is no hover on a phone, so every element that has a tooltip shows it
        /// in a small bubble when tapped (tap anywhere else, or wait, to dismiss it).</summary>
        void SetupTapTooltips(VisualElement root)
        {
            _tip = new VisualElement();
            _tip.AddToClassList("tip-bubble");
            _tip.style.display = DisplayStyle.None;
            _tipText = new Label();
            _tip.Add(_tipText);
            root.Add(_tip);

            root.RegisterCallback<ClickEvent>(evt =>
            {
                var el = evt.target as VisualElement;
                if (el != null && (el == _tip || _tip.Contains(el))) { HideTip(); return; }
                string text = null;
                for (var e = el; e != null; e = e.parent)
                {
                    if (e is Button) break; // a button is an action, not an explanation
                    if (!string.IsNullOrEmpty(e.tooltip)) { text = e.tooltip; break; }
                }
                if (text == null) { HideTip(); return; }
                _tipText.text = text;
                _tip.style.display = DisplayStyle.Flex;
                _tip.BringToFront();
                _tipHide?.Pause();
                _tipHide = _tip.schedule.Execute(HideTip).StartingIn(6000);
            }, TrickleDown.TrickleDown);
        }

        void HideTip()
        {
            _tipHide?.Pause();
            if (_tip != null) _tip.style.display = DisplayStyle.None;
        }

        void Swap(VisualElement screen, View kind)
        {
            HideTip();
            _screen = kind;
            _app.Clear();
            _app.Add(screen);
        }

        // ------------------------------------------------------------------
        public void ShowTitle() => Swap(new TitleScreen(this).Build(), View.Title);

        public void ShowMenu() => Swap(new MenuScreen(this).Build(), View.Menu);

        void ShowMenu(string notice) => Swap(new MenuScreen(this, notice).Build(), View.Menu);

        public void ShowSetup() => Swap(new SetupScreen(this).Build(), View.Setup);

        /// <summary>From the Help tab: back to the menu, after a warning about unsaved progress.</summary>
        public void RequestMainMenu()
        {
            string body = Engine != null && Engine.State.EpisodesPublished > 0
                ? "Your run is saved after every episode. What you've done since the last one is lost."
                : "This run hasn't published an episode yet, so there's nothing to save.";
            ShowDialog("Back to the main menu?", body, "Keep playing", "Main menu", ShowMenu);
        }

        public void LoadGame()
        {
            IRng rng = _rngSeed == 0 ? new SystemRng() : new SystemRng(_rngSeed);
            var engine = SaveStore.Load(rng, out string error);
            if (engine == null) { ShowMenu(error); return; }
            AttachEngine(engine);
            _weekScreen = null;
            ShowWeek();
        }

        public void StartRun(RunSetup setup)
        {
            var cfg = new GameConfig();
            IRng rng = _rngSeed == 0 ? new SystemRng() : new SystemRng(_rngSeed);
            AttachEngine(new Engine(setup, cfg, rng));
            _weekScreen = null;
            ShowWeek();
        }

        void AttachEngine(Engine engine)
        {
            Engine = engine;
            Theme.Set(engine.State.ColourPrimary, engine.State.ColourSecondary);

            _pendingMilestones.Clear();
            _goalJustReached = false;

            Engine.MilestoneReached += m => _pendingMilestones.Enqueue(m);
            Engine.GoalReached += () => _goalJustReached = true;
            Engine.GameOver += _ => { /* handled after Publish returns */ };
        }

        public void ShowWeek()
        {
            Engine.BeginWeek();
            _weekScreen = new WeekScreen(this);
            Swap(_weekScreen.Build(), View.Week);
        }

        /// <summary>Re-render the current week without re-simulating it (after a studio purchase).</summary>
        public void RerenderWeek()
        {
            if (_weekScreen == null) { ShowWeek(); return; }
            Swap(_weekScreen.Build(), View.Week);
        }

        public void Publish(ProductionPlan plan)
        {
            var result = Engine.Publish(plan);

            // Autosave after every episode; a finished (game-over) run is not worth keeping.
            if (Engine.State.IsGameOver) SaveStore.Delete();
            else SaveStore.Save(Engine);

            var milestones = new List<MilestoneEvent>();
            while (_pendingMilestones.Count > 0) milestones.Add(_pendingMilestones.Dequeue());

            Swap(new ResultsScreen(this, result, milestones, _goalJustReached).Build(), View.Results);
            _goalJustReached = false;
        }

        public void ContinueFromResults()
        {
            if (Engine.State.IsGameOver)
            {
                Swap(EndScreen.GameOver(this).Build(), View.End);
                return;
            }
            ShowWeek();
        }

        public void RestartToSetup()
        {
            Engine = null;
            ShowSetup();
        }

        // --- hooks used by the headless screenshot capture tool ---
        public void DebugStartRun()
        {
            StartRun(new RunSetup
            {
                PodcastName = "The Back Post",
                ClubName = "Fenwick Rovers",
                ColourPrimary = "#C0433F",
                ColourSecondary = "#F2C14E",
                Difficulty = Difficulty.Regular
            });
        }

        public void DebugPickFirstTopic() => _weekScreen?.DebugPickFirst();
        public void DebugPublish() => _weekScreen?.DebugPublish();
    }
}
