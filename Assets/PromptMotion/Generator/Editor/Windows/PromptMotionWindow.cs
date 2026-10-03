using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PromptMotion.Auth;
using PromptMotion.Http;
using PromptMotion.Models;
using PromptMotion.Services;
using PromptMotion.Settings;
using PromptMotion.Views;
using PromptMotion.Shared.Clips;
using PromptMotion.Shared.Humanoid;
using PromptMotion.Shared.Preview;
using PromptMotion.Shared.Utilities;
using PromptMotion.Utilities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PromptMotion.Windows
{
    /// <summary>
    /// PromptMotion's primary editor window. Round U-D merged the dev tool's
    /// polished UX (character validation, preview viewport, transport controls)
    /// with the Asset Store fork's production auth/quota/error layer (invoice
    /// activation, ErrorPresenter, auto-retry). Single window, single menu
    /// entry, single product.
    /// </summary>
    public class PromptMotionWindow : EditorWindow
    {
        // === Composed services ===
        // Constructed in CreateGUI; rebuilt on domain reload.
        // Test hooks let unit tests swap the transport + settings.
        internal IHttpTransport TransportOverrideForTest;
        internal PromptMotionSettings SettingsOverrideForTest;
        internal GenerationOrchestrator.IFbxSink SinkOverrideForTest;

        private PromptMotionSettings _settings;
        private AssetStoreApiClient _apiClient;
        private TrialActivator _activator;
        private GenerationOrchestrator _orchestrator;

        // Asset Store upgrade targets surfaced when the free trial is used up.
        private const string UpgradeStandardUrl = "https://u3d.as/1qyr"; // PromptMotion (Standard)
        private const string UpgradeProUrl = "https://u3d.as/1CAn";      // PromptMotion Pro

        // === Auth state ===
        private WindowState _state;
        private TrialAuthBanner _banner;
        private CancellationTokenSource _verifyCts;
        // Where Cancel returns to when the user backs out of a Busy round-trip.
        private AuthState _stateBeforeBusy = AuthState.NeedEmail;

        // === Generation state ===
        private CancellationTokenSource _generateCts;
        private bool _isGenerating;
        private ErrorPresentation _lastError;
        private DateTime _autoRetryAt;
        private int _autoRetryAttempts;
        private string _pendingPrompt;
        private int _pendingDuration;
        private int? _pendingSeed;
        private string _pendingClipName;

        // === Gated section root (everything below the banner) ===
        // Hidden until auth state == Ready so the customer can't fiddle with
        // character/prompt before activation.
        private VisualElement _gatedRoot;

        // === Character section ===
        private ObjectField _characterField;
        private VisualElement _statusContainer;
        private VisualElement _issuesContainer;
        private bool _isCharacterUsable;

        // === Generation form ===
        private TextField _promptField;
        private TextField _clipNameField;
        private SliderInt _durationSlider;
        private IntegerField _seedField;
        private Button _generateButton;
        private Button _cancelButton;
        private Button _applyButton;
        private Label _generationStatus;

        // === Progress bar ===
        private VisualElement _progressContainer;
        private VisualElement _progressBar;
        private VisualElement _progressFill;
        private Label _phaseLabel;
        private Label _progressLabel;
        private Label _elapsedLabel;
        private double _generationStartTime;
        private double _lastProgressTickTime;
        private double _progressHideTime;
        private float _displayedProgress;
        private float _targetProgress;
        private string _currentPhase;
        private string _lastPhaseText;
        private string _lastPctText;
        private string _lastElapsedText;

        // === Preview ===
        private AnimationPreviewRenderer _preview;
        private IMGUIContainer _previewViewport;
        private Button _playPauseButton;
        private Slider _scrubSlider;
        private Label _timeLabel;
        private double _lastEditorTime;
        private bool _suppressScrubCallback;
        private CameraDragMode _dragMode;
        private Vector2 _lastMousePosition;
        private VisualElement _previewSectionContent;
        private Label _previewPlaceholder;

        private enum CameraDragMode { None, Orbit, Pan }

        // === Serialised state (survives domain reload) ===
        [SerializeField] private GameObject _serializedCharacter;
        [SerializeField] private string _serializedPrompt;
        [SerializeField] private string _serializedClipName;
        [SerializeField] private int _serializedDuration = -1; // -1 = use settings default
        [SerializeField] private int _serializedSeed = -1;
        [SerializeField] private bool _serializedLoop = true;
        [SerializeField] private bool _serializedFollow;
        [SerializeField] private float _serializedSpeed = 1f;
        [SerializeField] private AnimationClip _serializedClip;
        [SerializeField] private bool _historyExpanded = true;

        // === History panel ===
        private HistoryStore _history;
        private IMGUIContainer _historyPanel;

        // === Menu items ===

        [MenuItem("Window/PromptMotion/Generator")]
        public static void OpenGenerator()
        {
            var window = GetWindow<PromptMotionWindow>();
            window.titleContent = new GUIContent("PromptMotion");
            window.minSize = new Vector2(480, 600);
            window.Show();
            window.Focus();
        }

        [MenuItem("Window/PromptMotion/Settings")]
        public static void OpenSettings()
        {
            var settings = PromptMotionSettings.Instance;
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Window/PromptMotion/Help")]
        public static void OpenHelp()
        {
            var docs = Application.dataPath + "/PromptMotion/Documentation/Documentation.html";
            if (!System.IO.File.Exists(docs))
            {
                EditorUtility.DisplayDialog(
                    "PromptMotion",
                    $"Could not find the documentation at:\n{docs}",
                    "OK");
                return;
            }
            Application.OpenURL("file://" + System.IO.Path.GetFullPath(docs));
        }

        [MenuItem("Window/PromptMotion/Reset Activation")]
        public static void ResetActivationFromMenu()
        {
            var confirmed = EditorUtility.DisplayDialog(
                "Reset activation?",
                "This clears the activated email on this machine. You'll need to " +
                "activate again with an email to keep using the free trial.",
                "Reset",
                "Cancel");
            if (!confirmed) return;

            SecureStore.Clear();
            WindowState.ClearPersisted();

            // If a window is open, refresh it so the banner returns to the
            // "Enter your email" state without requiring a domain reload.
            if (HasOpenInstances<PromptMotionWindow>())
            {
                var open = GetWindow<PromptMotionWindow>(utility: false, title: null, focus: false);
                open?.ResetToNeedEmailForTest();
            }
        }

        // === Lifecycle ===

        internal void CreateGUI()
        {
            _settings = SettingsOverrideForTest != null
                ? SettingsOverrideForTest
                : PromptMotionSettings.Instance;
            var transport = TransportOverrideForTest ?? new UnityWebRequestHttpTransport();
            _apiClient = new AssetStoreApiClient(_settings, transport);
            _activator = new TrialActivator(_apiClient);
            _orchestrator = new GenerationOrchestrator(_apiClient, SinkOverrideForTest ?? new GenerationOrchestrator.DefaultFbxSink())
            {
                OnCachedResult = () =>
                {
                    HideProgressBar();
                    var useCached = EditorUtility.DisplayDialog(
                        "PromptMotion — Cached Result",
                        "A cached animation already exists for this prompt.\n\nUse the cached clip, or generate a new variation?",
                        "Use Cached",
                        "Generate New");
                    ShowProgressBar();
                    return useCached;
                },
                OnPhaseChanged = (phase, progress) =>
                {
                    _currentPhase = phase;
                    _targetProgress = Mathf.Max(_targetProgress, progress);
                }
            };

            _state = WindowState.LoadOrDefault();

            _history = new HistoryStore();
            _history.Load();

            _preview = new AnimationPreviewRenderer();
            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            // Subscribe to the async-avatar-assignment notification so the
            // window re-validates once the user finishes the bone-assist
            // popup. Without this the "no Avatar assigned" issue persists in
            // the UI even though the Animator now has a valid avatar.
            PromptMotion.Shared.Humanoid.HumanoidAvatarFixer.AvatarAssignedAsync -= OnAsyncAvatarAssigned;
            PromptMotion.Shared.Humanoid.HumanoidAvatarFixer.AvatarAssignedAsync += OnAsyncAvatarAssigned;

            BuildUI();
            RestoreFromState();
        }

        private void OnDisable() => Cleanup();
        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            EditorApplication.update -= OnEditorUpdate;
            PromptMotion.Shared.Humanoid.HumanoidAvatarFixer.AvatarAssignedAsync -= OnAsyncAvatarAssigned;
            _verifyCts?.Cancel();
            _verifyCts?.Dispose();
            _verifyCts = null;
            _generateCts?.Cancel();
            _generateCts?.Dispose();
            _generateCts = null;
            if (_isGenerating)
            {
                _isGenerating = false;
            }
            _preview?.Dispose();
            _preview = null;
            if (SettingsOverrideForTest != null)
            {
                DestroyImmediate(SettingsOverrideForTest);
                SettingsOverrideForTest = null;
                _settings = null;
            }
        }

        // === UI construction ===

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 12;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingBottom = 12;

            // Banner gates everything below it. It sits outside the scroll
            // container so the auth state is always visible at the top of the
            // window, even when the scrollable section is scrolled down.
            _banner = new TrialAuthBanner();
            _banner.SendCodeRequested += OnSendCodeRequested;
            _banner.ActivateRequested += OnActivateRequested;
            _banner.ResendRequested += OnResendRequested;
            _banner.ChangeEmailRequested += OnChangeEmailRequested;
            _banner.CancelRequested += OnCancelBusyRequested;
            _banner.RefreshStatusRequested += OnRefreshStatusRequested;
            _banner.UpgradeStandardRequested += () => Application.OpenURL(UpgradeStandardUrl);
            _banner.UpgradeProRequested += () => Application.OpenURL(UpgradeProUrl);
            root.Add(_banner);

            // ScrollView fills the rest of the window so character /
            // generation / preview / history can flow vertically and overflow
            // gracefully when the user squeezes the window short instead of
            // overlapping each other.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            root.Add(scroll);

            // Everything below the banner lives in this container so we can
            // hide/show it as a unit when auth state changes.
            _gatedRoot = new VisualElement();
            scroll.Add(_gatedRoot);

            BuildCharacterSection(_gatedRoot);
            BuildDivider(_gatedRoot);
            BuildGenerationSection(_gatedRoot);
            BuildDivider(_gatedRoot);
            BuildPreviewSection(_gatedRoot);
            BuildDivider(_gatedRoot);
            BuildHistorySection(_gatedRoot);

            // Re-apply serialised preview state after a domain reload.
            _preview.Loop = _serializedLoop;
            _preview.Speed = _serializedSpeed;
            _preview.FollowCharacter = _serializedFollow;
            if (_serializedClip != null)
            {
                _preview.SetClip(_serializedClip);
                if (_scrubSlider != null)
                    _scrubSlider.highValue = Mathf.Max(_serializedClip.length, 0.01f);
            }

            RefreshValidation(_serializedCharacter);
            UpdateGenerateAvailability();
            UpdatePlayPauseIcon();
            UpdateTimeLabel();
        }

        // === Character section ===

        private void BuildCharacterSection(VisualElement root)
        {
            _characterField = new ObjectField(Strings.Window.FieldCharacter)
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
                tooltip = Strings.Window.TooltipCharacter
            };
            _characterField.RegisterValueChangedCallback(evt =>
            {
                _serializedCharacter = evt.newValue as GameObject;
                RefreshValidation(_serializedCharacter);
            });
            if (_serializedCharacter != null)
            {
                _characterField.SetValueWithoutNotify(_serializedCharacter);
            }
            root.Add(_characterField);

            _statusContainer = new VisualElement();
            _statusContainer.style.marginTop = 4;
            _statusContainer.style.marginBottom = 2;
            root.Add(_statusContainer);

            _issuesContainer = new VisualElement();
            _issuesContainer.style.marginBottom = 4;
            root.Add(_issuesContainer);
        }

        private static void BuildDivider(VisualElement root)
        {
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = Theme.Divider;
            divider.style.marginTop = 8;
            divider.style.marginBottom = 8;
            root.Add(divider);
        }

        // === Generation section ===

        private void BuildGenerationSection(VisualElement root)
        {
            var promptRow = new VisualElement();
            promptRow.style.flexDirection = FlexDirection.Row;
            promptRow.style.alignItems = Align.Center;
            promptRow.style.marginBottom = 2;

            var promptHeading = new Label(Strings.Window.SectionPrompt);
            promptHeading.style.unityFontStyleAndWeight = FontStyle.Bold;
            promptHeading.style.flexGrow = 1;
            promptRow.Add(promptHeading);

            var tipsButton = new Button(PromptBestPracticesWindow.Open) { text = "?", tooltip = "Open prompting tips" };
            tipsButton.style.width = 22;
            tipsButton.style.height = 18;
            tipsButton.style.paddingLeft = 0;
            tipsButton.style.paddingRight = 0;
            tipsButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            promptRow.Add(tipsButton);

            root.Add(promptRow);

            _promptField = new TextField
            {
                multiline = true,
                value = string.IsNullOrEmpty(_serializedPrompt)
                    ? Strings.Window.PromptPlaceholder
                    : _serializedPrompt
            };
            _promptField.style.minHeight = 60;
            _promptField.style.marginBottom = 4;
            _promptField.RegisterValueChangedCallback(evt => _serializedPrompt = evt.newValue);
            root.Add(_promptField);

            _clipNameField = new TextField(Strings.Window.FieldClipName)
            {
                value = _serializedClipName ?? string.Empty,
                tooltip = Strings.Window.TooltipClipName
            };
            _clipNameField.style.marginBottom = 8;
            _clipNameField.RegisterValueChangedCallback(evt => _serializedClipName = evt.newValue);
            root.Add(_clipNameField);

            var settings = PromptMotionSettings.Instance;
            var durationInitial = _serializedDuration > 0
                ? Mathf.Clamp(_serializedDuration, 1, 10)
                : Mathf.Clamp(settings.DefaultDurationSeconds, 1, 10);
            _durationSlider = new SliderInt(Strings.Window.FieldDuration, 1, 10)
            {
                value = durationInitial,
                showInputField = true
            };
            _durationSlider.style.marginBottom = 4;
            _durationSlider.RegisterValueChangedCallback(evt => _serializedDuration = evt.newValue);
            _serializedDuration = durationInitial;
            root.Add(_durationSlider);

            _seedField = new IntegerField(Strings.Window.FieldSeed)
            {
                value = _serializedSeed,
                tooltip = Strings.Window.TooltipSeed
            };
            _seedField.style.marginBottom = 8;
            _seedField.RegisterValueChangedCallback(evt => _serializedSeed = evt.newValue);
            root.Add(_seedField);

            _generateButton = new Button(OnGenerateClicked) { text = Strings.Window.ButtonGenerate };
            _generateButton.style.flexGrow = 1;
            _generateButton.style.height = 28;
            _generateButton.style.marginTop = 4;
            root.Add(_generateButton);

            // Progress container — phase label + green bar + elapsed — hidden until generation.
            _progressContainer = new VisualElement();
            _progressContainer.style.marginTop = 6;
            _progressContainer.style.flexShrink = 0;
            _progressContainer.style.display = DisplayStyle.None;

            _phaseLabel = new Label(string.Empty);
            _phaseLabel.style.fontSize = 11;
            _phaseLabel.style.marginBottom = 4;
            _phaseLabel.style.minHeight = 18;
            _phaseLabel.style.flexShrink = 0;
            _progressContainer.Add(_phaseLabel);

            _progressBar = new VisualElement();
            _progressBar.style.height = 28;
            _progressBar.style.minHeight = 28;
            _progressBar.style.borderTopWidth = _progressBar.style.borderBottomWidth =
                _progressBar.style.borderLeftWidth = _progressBar.style.borderRightWidth = 1;
            _progressBar.style.borderTopLeftRadius = _progressBar.style.borderTopRightRadius =
                _progressBar.style.borderBottomLeftRadius = _progressBar.style.borderBottomRightRadius = 3;
            _progressBar.style.overflow = Overflow.Hidden;

            _progressFill = new VisualElement();
            _progressFill.style.position = Position.Absolute;
            _progressFill.style.top = 0;
            _progressFill.style.bottom = 0;
            _progressFill.style.left = 0;
            _progressFill.style.width = new Length(0, LengthUnit.Percent);
            _progressBar.Add(_progressFill);

            _progressLabel = new Label("0%");
            _progressLabel.style.position = Position.Absolute;
            _progressLabel.style.top = 0;
            _progressLabel.style.bottom = 0;
            _progressLabel.style.left = 0;
            _progressLabel.style.right = 0;
            _progressLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _progressLabel.style.color = Color.white;
            _progressLabel.style.fontSize = 11;
            _progressBar.Add(_progressLabel);

            _cancelButton = new Button(OnCancelClicked) { text = Strings.Window.ButtonCancel };
            _cancelButton.style.height = 28;
            _cancelButton.style.marginLeft = 6;
            _cancelButton.style.width = 70;

            var progressBarRow = new VisualElement();
            progressBarRow.style.flexDirection = FlexDirection.Row;
            progressBarRow.style.alignItems = Align.Center;
            progressBarRow.style.flexShrink = 0;
            _progressBar.style.flexGrow = 1;
            _progressBar.style.flexShrink = 0;
            progressBarRow.Add(_progressBar);
            progressBarRow.Add(_cancelButton);
            _progressContainer.Add(progressBarRow);

            _elapsedLabel = new Label(string.Empty);
            _elapsedLabel.style.fontSize = 10;
            _elapsedLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _elapsedLabel.style.color = Theme.MutedText;
            _elapsedLabel.style.marginTop = 4;
            _elapsedLabel.style.minHeight = 18;
            _elapsedLabel.style.flexShrink = 0;
            _progressContainer.Add(_elapsedLabel);

            root.Add(_progressContainer);

            _applyButton = new Button(OnApplyClicked)
            {
                text = Strings.Window.ButtonApplyToCharacter,
                tooltip = Strings.Window.TooltipApplyToCharacter
            };
            _applyButton.style.height = 24;
            _applyButton.style.marginTop = 4;
            _applyButton.SetEnabled(false);
            root.Add(_applyButton);

            _generationStatus = new Label(string.Empty);
            _generationStatus.style.marginTop = 6;
            _generationStatus.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_generationStatus);
        }

        // === Preview section ===

        private void BuildPreviewSection(VisualElement root)
        {
            // Placeholder shown in place of the whole preview section while no
            // usable humanoid character is assigned. Toggled against
            // _previewSectionContent in UpdatePreviewSectionVisibility.
            _previewPlaceholder = new Label(
                "Preview will appear here once a humanoid character is successfully assigned.");
            _previewPlaceholder.style.color = Theme.MutedText;
            _previewPlaceholder.style.unityFontStyleAndWeight = FontStyle.Italic;
            _previewPlaceholder.style.whiteSpace = WhiteSpace.Normal;
            _previewPlaceholder.style.marginTop = 4;
            _previewPlaceholder.style.marginBottom = 4;
            root.Add(_previewPlaceholder);

            _previewSectionContent = new VisualElement();
            root.Add(_previewSectionContent);
            // Build the actual preview UI into the content container so we can
            // hide it as a unit.
            root = _previewSectionContent;

            var heading = new Label(Strings.Window.SectionPreview);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = 4;
            root.Add(heading);

            var viewportContainer = new VisualElement();
            viewportContainer.style.position = Position.Relative;
            // Bumped from 160 → 220 because the window content lives inside a
            // ScrollView now: flexGrow no longer fills the vertical viewport,
            // so minHeight is what the preview gets at rest.
            viewportContainer.style.minHeight = 220;
            viewportContainer.style.flexGrow = 1;

            _previewViewport = new IMGUIContainer(DrawViewport);
            _previewViewport.style.flexGrow = 1;
            _previewViewport.RegisterCallback<MouseDownEvent>(OnViewportMouseDown);
            _previewViewport.RegisterCallback<MouseUpEvent>(OnViewportMouseUp);
            _previewViewport.RegisterCallback<MouseMoveEvent>(OnViewportMouseMove);
            _previewViewport.RegisterCallback<WheelEvent>(OnViewportWheel);
            viewportContainer.Add(_previewViewport);

            var resetButton = new Button(OnResetCameraClicked) { text = "⌂", tooltip = "Reset camera" };
            resetButton.style.position = Position.Absolute;
            resetButton.style.right = 4;
            resetButton.style.bottom = 4;
            resetButton.style.width = 24;
            resetButton.style.height = 24;
            resetButton.style.minWidth = 24;
            resetButton.style.minHeight = 24;
            resetButton.style.paddingTop = resetButton.style.paddingBottom =
                resetButton.style.paddingLeft = resetButton.style.paddingRight = 0;
            resetButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            resetButton.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            resetButton.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            resetButton.style.borderTopWidth = resetButton.style.borderBottomWidth =
                resetButton.style.borderLeftWidth = resetButton.style.borderRightWidth = 1;
            resetButton.style.borderTopColor = resetButton.style.borderBottomColor =
                resetButton.style.borderLeftColor = resetButton.style.borderRightColor =
                    new Color(1f, 1f, 1f, 0.2f);
            resetButton.style.borderTopLeftRadius = resetButton.style.borderTopRightRadius =
                resetButton.style.borderBottomLeftRadius = resetButton.style.borderBottomRightRadius = 3;
            viewportContainer.Add(resetButton);

            root.Add(viewportContainer);

            // Transport: play/pause + scrub + time
            var transport = new VisualElement();
            transport.style.flexDirection = FlexDirection.Row;
            transport.style.alignItems = Align.Center;
            transport.style.marginTop = 6;

            _playPauseButton = new Button(OnPlayPauseClicked) { text = "▶" };
            _playPauseButton.style.width = 24;
            _playPauseButton.style.height = 24;
            _playPauseButton.style.minWidth = 24;
            _playPauseButton.style.minHeight = 24;
            _playPauseButton.style.maxWidth = 24;
            _playPauseButton.style.maxHeight = 24;
            _playPauseButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            _playPauseButton.style.paddingTop = _playPauseButton.style.paddingBottom =
                _playPauseButton.style.paddingLeft = _playPauseButton.style.paddingRight = 0;
            _playPauseButton.style.flexShrink = 0;
            _playPauseButton.tooltip = Strings.Window.TooltipPlayPause;
            transport.Add(_playPauseButton);

            _scrubSlider = new Slider(0f, 1f) { style = { flexGrow = 1, marginLeft = 6 } };
            _scrubSlider.RegisterValueChangedCallback(OnScrubChanged);
            transport.Add(_scrubSlider);

            _timeLabel = new Label("0.00s / 0.00s");
            _timeLabel.style.minWidth = 90;
            _timeLabel.style.marginLeft = 4;
            transport.Add(_timeLabel);

            root.Add(transport);

            // Speed row
            var speedRow = new VisualElement();
            speedRow.style.flexDirection = FlexDirection.Row;
            speedRow.style.alignItems = Align.Center;
            speedRow.style.marginTop = 4;
            speedRow.Add(new Label("Speed") { style = { width = 50 } });
            var speedSlider = new Slider(0.1f, 4f) { value = _serializedSpeed, style = { flexGrow = 1 } };
            speedSlider.RegisterValueChangedCallback(evt =>
            {
                _serializedSpeed = evt.newValue;
                if (_preview != null) _preview.Speed = evt.newValue;
            });
            speedRow.Add(speedSlider);
            root.Add(speedRow);

            // Loop + Follow row
            var toggleRow = new VisualElement();
            toggleRow.style.flexDirection = FlexDirection.Row;
            toggleRow.style.alignItems = Align.Center;
            toggleRow.style.marginTop = 4;

            var loopLabel = new Label("Loop");
            loopLabel.style.marginRight = 4;
            toggleRow.Add(loopLabel);
            var loopToggle = new Toggle { value = _serializedLoop };
            loopToggle.style.marginLeft = 0;
            loopToggle.RegisterValueChangedCallback(evt =>
            {
                _serializedLoop = evt.newValue;
                if (_preview != null) _preview.Loop = evt.newValue;
            });
            toggleRow.Add(loopToggle);

            var followLabel = new Label("Follow");
            followLabel.style.marginLeft = 12;
            followLabel.style.marginRight = 4;
            toggleRow.Add(followLabel);
            var followToggle = new Toggle { value = _serializedFollow };
            followToggle.style.marginLeft = 0;
            followToggle.tooltip = "Camera tracks the character's position during playback";
            followToggle.RegisterValueChangedCallback(evt =>
            {
                _serializedFollow = evt.newValue;
                if (_preview != null) _preview.FollowCharacter = evt.newValue;
            });
            toggleRow.Add(followToggle);

            root.Add(toggleRow);
        }

        // === History section ===

        private void BuildHistorySection(VisualElement root)
        {
            _historyPanel = new IMGUIContainer(DrawHistoryPanel);
            _historyPanel.style.marginTop = 4;
            _historyPanel.style.flexShrink = 0;
            root.Add(_historyPanel);
        }

        private void DrawHistoryPanel()
        {
            if (_history == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                _historyExpanded = EditorGUILayout.Foldout(
                    _historyExpanded,
                    $"Recent Generations ({_history.Entries.Count})",
                    toggleOnLabelClick: true,
                    EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                if (_history.Entries.Count > 0)
                {
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        if (EditorUtility.DisplayDialog(
                                "Clear history",
                                "Remove all entries from history? This does not delete any files.",
                                "Clear",
                                "Cancel"))
                        {
                            _history.Clear();
                            _historyPanel?.MarkDirtyRepaint();
                        }
                    }
                }
            }

            if (!_historyExpanded) return;

            if (_history.Entries.Count == 0)
            {
                EditorGUILayout.LabelField("Generate a clip to see it here.", EditorStyles.miniLabel);
                return;
            }

            var invoice = SecureStore.Load();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (var entry in _history.Entries)
                {
                    DrawHistoryRow(entry, invoice);
                }
            }
        }

        private void DrawHistoryRow(HistoryEntry entry, string currentInvoice)
        {
            var stale = _history.IsStale(entry, currentInvoice);
            var prevColor = GUI.color;
            if (stale) GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, 0.5f);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    FormatTimestamp(entry.TimestampUnix),
                    EditorStyles.miniLabel,
                    GUILayout.Width(95));

                var promptText = stale ? $"(stale) {entry.Prompt}" : entry.Prompt;
                EditorGUILayout.LabelField(
                    new GUIContent(promptText, entry.Prompt),
                    EditorStyles.label);

                var fbxExists = !string.IsNullOrEmpty(entry.FbxAssetPath)
                    && File.Exists(entry.FbxAssetPath);
                using (new EditorGUI.DisabledScope(!fbxExists))
                {
                    var locateContent = new GUIContent(
                        "Locate",
                        fbxExists ? "Reveal in Project" : "File no longer in project.");
                    if (GUILayout.Button(locateContent, EditorStyles.miniButton, GUILayout.Width(55)))
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(entry.FbxAssetPath);
                        if (asset != null) EditorGUIUtility.PingObject(asset);
                    }
                }

                if (!stale)
                {
                    if (GUILayout.Button(
                            new GUIContent("Re-gen", "Pre-fill prompt + duration"),
                            EditorStyles.miniButton,
                            GUILayout.Width(55)))
                    {
                        ApplyHistoryToForm(entry);
                    }
                }
                else
                {
                    GUILayout.Space(59); // 55 button + 4 default spacing — keeps the Locate column aligned
                }
            }

            GUI.color = prevColor;
        }

        private void ApplyHistoryToForm(HistoryEntry entry)
        {
            // Snapshot the values now — the entry list could change before the
            // delayed call fires.
            var prompt = entry.Prompt ?? string.Empty;
            var duration = Mathf.Clamp(entry.DurationSeconds, 1, 10);
            var seed = entry.Seed >= 0 ? entry.Seed : -1;

            // Defer to the next editor tick. Setting UI Toolkit field values
            // from inside an IMGUIContainer's GUI callback runs mid-frame: the
            // surrounding TextField / SliderInt / IntegerField have already
            // had their layout pass for this frame, so the visible elements
            // can stay out of sync with the new value. delayCall hands the
            // update to the next normal UI Toolkit update cycle.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (_promptField != null) _promptField.value = prompt;
                if (_durationSlider != null) _durationSlider.value = duration;
                if (_seedField != null) _seedField.value = seed;
                rootVisualElement?.MarkDirtyRepaint();
                Repaint();
            };
        }

        private static string FormatTimestamp(long unix)
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
            var now = DateTime.Now;
            if (dt.Date == now.Date) return dt.ToString("HH:mm 'today'");
            if (dt.Date == now.Date.AddDays(-1)) return dt.ToString("HH:mm 'yest.'");
            return dt.ToString("MMM d HH:mm");
        }

        // === Preview: viewport rendering ===

        private void DrawViewport()
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (Event.current.type != EventType.Repaint) return;

            if (_preview == null) return;
            if (!_preview.HasCharacter)
            {
                var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    { alignment = TextAnchor.MiddleCenter };
                GUI.Label(rect, Strings.Window.HintPreviewNoCharacter, style);
                return;
            }

            var tex = _preview.Render(rect);
            if (tex != null)
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, alphaBlend: false);

            if (!_preview.HasClip)
            {
                var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    { alignment = TextAnchor.LowerCenter };
                var hintRect = new Rect(rect.x, rect.yMax - 18, rect.width, 18);
                GUI.Label(hintRect, Strings.Window.HintPreviewNoClip, style);
            }
        }

        // === Preview: camera input (UI Toolkit events) ===

        private const float OrbitSensitivity = 0.4f;
        private const int LeftButton = 0;
        private const int RightButton = 1;

        private void OnViewportMouseDown(MouseDownEvent evt)
        {
            switch (evt.button)
            {
                case RightButton: _dragMode = CameraDragMode.Orbit; break;
                case LeftButton:  _dragMode = CameraDragMode.Pan;   break;
                default: return;
            }
            _lastMousePosition = evt.mousePosition;
            _previewViewport.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnViewportMouseUp(MouseUpEvent evt)
        {
            if (evt.button != LeftButton && evt.button != RightButton) return;
            _dragMode = CameraDragMode.None;
            _previewViewport.ReleaseMouse();
        }

        private void OnViewportMouseMove(MouseMoveEvent evt)
        {
            if (_dragMode == CameraDragMode.None) return;
            var delta = evt.mousePosition - _lastMousePosition;
            _lastMousePosition = evt.mousePosition;
            switch (_dragMode)
            {
                case CameraDragMode.Orbit:
                    _preview?.OrbitBy(-delta.x * OrbitSensitivity, -delta.y * OrbitSensitivity);
                    break;
                case CameraDragMode.Pan:
                    _preview?.PanBy(delta.x, delta.y);
                    break;
            }
            _previewViewport?.MarkDirtyRepaint();
        }

        private void OnViewportWheel(WheelEvent evt)
        {
            _preview?.ZoomBy(evt.delta.y);
            _previewViewport?.MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnResetCameraClicked()
        {
            _preview?.ResetView();
            _previewViewport?.MarkDirtyRepaint();
        }

        // === Preview: transport callbacks ===

        private void OnPlayPauseClicked()
        {
            if (_preview == null) return;
            _preview.TogglePlay();
            _lastEditorTime = EditorApplication.timeSinceStartup;
            UpdatePlayPauseIcon();
        }

        private void UpdatePlayPauseIcon()
        {
            if (_playPauseButton == null || _preview == null) return;
            _playPauseButton.text = _preview.IsPlaying ? "⏸" : "▶";
        }

        private void OnScrubChanged(ChangeEvent<float> evt)
        {
            if (_suppressScrubCallback) return;
            if (_preview == null || !_preview.HasClip) return;
            _preview.CurrentTime = evt.newValue;
            UpdateTimeLabel();
            _previewViewport?.MarkDirtyRepaint();
        }

        private void OnEditorUpdate()
        {
            // Drive auto-retry timer + progress bar.
            TickAutoRetry();
            TickProgressBar();

            // Drive preview playback.
            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - _lastEditorTime);
            _lastEditorTime = now;

            if (_preview == null) return;

            if (_preview.IsPlaying)
            {
                var wasPlaying = _preview.IsPlaying;
                _preview.Tick(dt);
                UpdateScrubSlider();
                UpdateTimeLabel();
                _previewViewport?.MarkDirtyRepaint();
                rootVisualElement?.MarkDirtyRepaint();
                if (wasPlaying && !_preview.IsPlaying)
                {
                    UpdatePlayPauseIcon();
                }
            }
        }

        private void UpdateScrubSlider()
        {
            if (_scrubSlider == null || _preview == null) return;
            if (_preview.ClipLength <= 0f) return;
            _suppressScrubCallback = true;
            _scrubSlider.SetValueWithoutNotify(_preview.CurrentTime);
            _suppressScrubCallback = false;
        }

        private string _lastTimeLabelText;

        private void UpdateTimeLabel()
        {
            if (_timeLabel == null || _preview == null) return;
            // Dedup to avoid TextCore concurrent-modification asserts on Unity 6.
            var newText = $"{_preview.CurrentTime:0.00}s / {_preview.ClipLength:0.00}s";
            if (newText == _lastTimeLabelText) return;
            _lastTimeLabelText = newText;
            _timeLabel.text = newText;
        }

        // === Validation ===

        /// <summary>
        /// Called by <see cref="PromptMotion.Shared.Humanoid.HumanoidAvatarFixer"/>
        /// when the bone-assist popup finishes and assigns an avatar
        /// asynchronously. Re-runs validation so the "no Avatar assigned"
        /// issue clears immediately without the user having to interact with
        /// the validator UI again. No-op if a different character finished
        /// (someone else's window is the source) or if our serialized
        /// character is stale.
        /// </summary>
        private void OnAsyncAvatarAssigned(GameObject character)
        {
            if (this == null) return;
            if (character == null) return;
            if (_serializedCharacter != character) return;
            RefreshValidation(_serializedCharacter);
            UpdateGenerateAvailability();
        }

        private void RefreshValidation(GameObject character)
        {
            if (_statusContainer == null || _issuesContainer == null) return;
            _statusContainer.Clear();
            _issuesContainer.Clear();

            if (character == null)
            {
                var hint = new Label(Strings.Window.HintAssignCharacter);
                hint.style.color = Theme.MutedText;
                hint.style.unityFontStyleAndWeight = FontStyle.Italic;
                _statusContainer.Add(hint);
                _isCharacterUsable = false;
                _preview?.SetCharacter(null);
                _previewViewport?.MarkDirtyRepaint();
                UpdateGenerateAvailability();
                return;
            }

            ValidationReport report;
            var sw = Stopwatch.StartNew();
            try
            {
                report = HumanoidValidator.Validate(character);
            }
            catch (Exception ex)
            {
                Log.Error($"Validation threw: {ex}");
                report = new ValidationReport(new[]
                {
                    new ValidationIssue(ValidationSeverity.Error, $"Validation failed: {ex.Message}")
                });
            }
            sw.Stop();

            _isCharacterUsable = report.IsUsable;
            _statusContainer.Add(CreateStatusLabel(report, sw.ElapsedMilliseconds));
            foreach (var issue in report.Issues)
            {
                _issuesContainer.Add(CreateIssueRow(issue));
            }

            _preview?.SetCharacter(_isCharacterUsable ? character : null);
            _previewViewport?.MarkDirtyRepaint();
            UpdateGenerateAvailability();
        }

        private void UpdateGenerateAvailability()
        {
            if (_generateButton == null) return;
            var ready = _state != null && _state.Auth == AuthState.Ready;
            _generateButton.SetEnabled(ready && _isCharacterUsable && !_isGenerating);
            _generateButton.style.display = _isGenerating ? DisplayStyle.None : DisplayStyle.Flex;
            UpdateApplyAvailability();
            UpdatePreviewSectionVisibility();
        }

        private void UpdatePreviewSectionVisibility()
        {
            if (_previewSectionContent == null || _previewPlaceholder == null) return;
            _previewSectionContent.style.display =
                _isCharacterUsable ? DisplayStyle.Flex : DisplayStyle.None;
            _previewPlaceholder.style.display =
                _isCharacterUsable ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void UpdateApplyAvailability()
        {
            if (_applyButton == null) return;
            _applyButton.SetEnabled(_isCharacterUsable && !_isGenerating && _serializedClip != null);
            _applyButton.style.display = _isGenerating ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // === Auth state machine ===

        private void RestoreFromState()
        {
            // If the last session ended Ready and we still have the email on
            // file, trust the cached state and render Ready immediately; refresh
            // the lifetime quota in the background via StartStatus (which only
            // attaches a banner error on failure and leaves Auth alone, so a
            // slow/offline backend on Unity startup can't silently sign the user
            // out and force them to re-activate every session).
            var stored = SecureStore.Load();
            if (!string.IsNullOrEmpty(stored))
            {
                // We have an activated email on file — trust it and refresh in
                // the background. StartStatus lands in Ready on success and only
                // attaches a banner error on failure (it never signs the user
                // out), so a persisted half-state (e.g. after "Use a different
                // email") or a slow/offline backend on startup recovers cleanly.
                RenderCurrentAuthState();
                StartStatus(stored);
                return;
            }

            // No stored email. A code-sent state can be resumed within the
            // code's 15-minute TTL; a transient Busy state falls back to the
            // email step.
            if (_state.Auth == AuthState.Busy)
            {
                _state.Auth = string.IsNullOrEmpty(_state.PendingEmail)
                    ? AuthState.NeedEmail
                    : AuthState.CodeSent;
            }

            RenderCurrentAuthState();
        }

        private void RenderCurrentAuthState()
        {
            if (_banner == null) return;
            UpdateGatedSectionVisibility();

            var error = MakeBannerError();
            switch (_state.Auth)
            {
                case AuthState.NeedEmail:
                    _banner.RenderNeedEmail(_state.PendingEmail, error);
                    break;
                case AuthState.CodeSent:
                    _banner.RenderCodeSent(_state.PendingEmail, error);
                    break;
                case AuthState.Busy:
                    _banner.RenderBusy("Working…");
                    break;
                case AuthState.Ready:
                    _banner.RenderReady(_state.PendingEmail, _state.UsedThisMonth, _state.MonthlyQuota);
                    break;
            }

            UpdateGenerateAvailability();
        }

        private void UpdateGatedSectionVisibility()
        {
            if (_gatedRoot == null) return;
            _gatedRoot.style.display =
                _state.Auth == AuthState.Ready ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private ApiError MakeBannerError()
        {
            if (string.IsNullOrEmpty(_state.LastErrorCode) &&
                string.IsNullOrEmpty(_state.LastErrorMessage)) return null;
            return new ApiError
            {
                CodeRaw = _state.LastErrorCode,
                Message = _state.LastErrorMessage,
            };
        }

        // === Auth event handlers ===

        private void OnSendCodeRequested(string email)
        {
            _state.PendingEmail = email;
            StartActivation(email);
        }

        private void OnResendRequested()
        {
            if (string.IsNullOrEmpty(_state.PendingEmail))
            {
                _state.Auth = AuthState.NeedEmail;
                _state.ClearError();
                _state.Save();
                RenderCurrentAuthState();
                return;
            }
            StartRequestCode(_state.PendingEmail);
        }

        private void OnActivateRequested(string code)
        {
            if (string.IsNullOrEmpty(_state.PendingEmail))
            {
                _state.Auth = AuthState.NeedEmail;
                RenderCurrentAuthState();
                return;
            }
            StartVerifyCode(_state.PendingEmail, code);
        }

        private void OnChangeEmailRequested()
        {
            _verifyCts?.Cancel();
            _state.Auth = AuthState.NeedEmail;
            _state.ClearError();
            _state.Save();
            RenderCurrentAuthState();
        }

        private void OnCancelBusyRequested()
        {
            _verifyCts?.Cancel();
            _state.Auth = _stateBeforeBusy;
            _state.ClearError();
            _state.Save();
            RenderCurrentAuthState();
        }

        // ResetToNeedEmailForTest is invoked by the "Window > PromptMotion >
        // Reset Activation" menu so an open window returns to the not-activated
        // banner state without a domain reload.
        internal void ResetToNeedEmailForTest()
        {
            _verifyCts?.Cancel();
            _state = new WindowState();
            _state.Save();
            RenderCurrentAuthState();
        }

        private void OnRefreshStatusRequested()
        {
            var email = SecureStore.Load();
            if (string.IsNullOrEmpty(email)) return;
            StartStatus(email);
        }

        // === Auth async flows ===

        /// <summary>
        /// Entry point for an email submitted in the banner. First asks the
        /// backend (via /prod/status) whether this email is ALREADY an activated
        /// trial — if so, restore it directly with no code. This covers
        /// re-entering the email after "Use a different email", a reinstall /
        /// new machine, or a cleared activation, none of which can re-run the
        /// code flow (the backend refuses TRIAL_ALREADY_USED). Only a genuinely
        /// new email proceeds to the email→code activation.
        /// </summary>
        private async void StartActivation(string email)
        {
            _stateBeforeBusy = AuthState.NeedEmail;
            _state.Auth = AuthState.Busy;
            _state.PendingEmail = email;
            _state.ClearError();
            _state.Save();
            _banner.RenderBusy("Checking your email…");

            var ct = NewVerifyCancellationToken();
            ApiResponse<StatusResult> status;
            try
            {
                status = await _apiClient.StatusAsync(email, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // Couldn't reach status — fall through to the normal code flow.
                status = null;
            }
            if (this == null) return;

            if (status != null && status.Ok && status.Data != null &&
                !string.IsNullOrEmpty(status.Data.Tier))
            {
                // Already activated → restore without a code.
                _state.ApplyStatusResult(status.Data);
                _state.Auth = AuthState.Ready;
                _state.PendingEmail = email;
                _state.ClearError();
                SecureStore.Save(email);
                _state.Save();
                RenderCurrentAuthState();
                return;
            }

            // New email → send an activation code.
            StartRequestCode(email);
        }

        private async void StartRequestCode(string email)
        {
            _stateBeforeBusy = AuthState.NeedEmail;
            _state.Auth = AuthState.Busy;
            _state.PendingEmail = email;
            _state.ClearError();
            _state.Save();
            _banner.RenderBusy("Sending your code…");

            var ct = NewVerifyCancellationToken();
            TrialRequestOutcome outcome;
            try
            {
                outcome = await _activator.RequestCodeAsync(
                    email, SecureStore.GetOrCreateActivationNonce(), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (this == null) return;
                FailBackTo(AuthState.NeedEmail, new ApiError
                {
                    CodeRaw = ApiErrorCode.NetworkError.ToString(),
                    Message = ex.Message,
                });
                return;
            }

            if (this == null) return;
            if (outcome.Ok)
            {
                _state.Auth = AuthState.CodeSent;
                _state.ClearError();
                _state.Save();
                RenderCurrentAuthState();
            }
            else
            {
                FailBackTo(AuthState.NeedEmail, outcome.Error);
            }
        }

        private async void StartVerifyCode(string email, string code)
        {
            _stateBeforeBusy = AuthState.CodeSent;
            _state.Auth = AuthState.Busy;
            _state.PendingEmail = email;
            _state.ClearError();
            _state.Save();
            _banner.RenderBusy("Activating…");

            var ct = NewVerifyCancellationToken();
            VerifierResult result;
            try
            {
                result = await _activator.VerifyCodeAsync(
                    email, code, SecureStore.GetOrCreateActivationNonce(), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (this == null) return;
                FailBackTo(AuthState.CodeSent, new ApiError
                {
                    CodeRaw = ApiErrorCode.NetworkError.ToString(),
                    Message = ex.Message,
                });
                return;
            }

            if (this == null) return;
            ApplyVerifierResult(result);
        }

        private async void StartStatus(string email)
        {
            var ct = NewVerifyCancellationToken();
            ApiResponse<StatusResult> resp;
            try
            {
                resp = await _apiClient.StatusAsync(email, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                if (this == null) return;
                _state.SetError(new ApiError
                {
                    CodeRaw = ApiErrorCode.NetworkError.ToString(),
                    Message = ex.Message,
                });
                _state.Save();
                RenderCurrentAuthState();
                return;
            }
            if (this == null) return;

            if (resp == null || !resp.Ok || resp.Error != null)
            {
                _state.SetError(resp?.Error);
                _state.Save();
                RenderCurrentAuthState();
                return;
            }

            // A successful status means this email is a valid activated trial,
            // so land in Ready (this also recovers a persisted half-state, e.g.
            // after the user tapped "Use a different email" and reopened).
            _state.ApplyStatusResult(resp.Data);
            _state.Auth = AuthState.Ready;
            _state.ClearError();
            _state.Save();
            RenderCurrentAuthState();
        }

        private void ApplyVerifierResult(VerifierResult result)
        {
            if (result == null)
            {
                FailBackTo(AuthState.CodeSent, new ApiError
                {
                    CodeRaw = ApiErrorCode.Unknown.ToString(),
                    Message = "Empty result.",
                });
                return;
            }

            switch (result.Outcome)
            {
                case VerifierOutcome.Verified:
                    _state.ApplyVerifyResult(result.Data);
                    _state.Auth = AuthState.Ready;
                    _state.PendingEmail = result.Data?.Invoice ?? _state.PendingEmail;
                    _state.ClearError();
                    SecureStore.Save(result.Data?.Invoice ?? _state.PendingEmail);
                    _state.Save();
                    RenderCurrentAuthState();
                    break;

                case VerifierOutcome.Failed:
                default:
                    FailBackTo(AuthState.CodeSent, result.Error);
                    break;
            }
        }

        private void FailBackTo(AuthState state, ApiError error)
        {
            _state.Auth = state;
            _state.SetError(error);
            _state.Save();
            RenderCurrentAuthState();
        }

        private CancellationToken NewVerifyCancellationToken()
        {
            _verifyCts?.Cancel();
            _verifyCts?.Dispose();
            _verifyCts = new CancellationTokenSource();
            return _verifyCts.Token;
        }

        // === Generation flow ===

        private async void OnGenerateClicked()
        {
            if (_isGenerating) return;
            if (_state == null || _state.Auth != AuthState.Ready)
            {
                SetStatus("Activate your free trial in the banner above first.", StatusColor.Error);
                return;
            }
            if (!_isCharacterUsable)
            {
                SetStatus(SharedStrings.Validation.NotUsable, StatusColor.Error);
                return;
            }

            var prompt = _promptField?.value?.Trim();
            if (string.IsNullOrEmpty(prompt))
            {
                SetStatus(Strings.Status.ErrorPromptEmpty, StatusColor.Error);
                return;
            }

            var invoice = SecureStore.Load();
            if (string.IsNullOrEmpty(invoice))
            {
                SetStatus("Activation missing. Use \"Use a different email\" in the banner and re-activate.", StatusColor.Error);
                return;
            }

            _pendingClipName = _clipNameField?.value;
            _pendingPrompt = prompt;
            _pendingDuration = _durationSlider.value;
            _pendingSeed = _seedField.value < 0 ? (int?)null : _seedField.value;
            CancelAutoRetry();
            _autoRetryAttempts = 0;
            await DoGenerate(invoice, prompt, _pendingDuration, _pendingSeed);
        }

        private async Task DoGenerate(string invoice, string prompt, int duration, int? seed)
        {
            if (_isGenerating) return;

            _generateCts?.Cancel();
            _generateCts?.Dispose();
            _generateCts = new CancellationTokenSource();
            _isGenerating = true;
            _generationStartTime = EditorApplication.timeSinceStartup;
            SetStatus(Strings.Status.Generating, StatusColor.Neutral);
            UpdateGenerateAvailability();
            ShowProgressBar();

            GenerationOrchestrator.Outcome outcome;
            try
            {
                outcome = await _orchestrator.RunAsync(invoice, prompt, duration, seed, _generateCts.Token);
            }
            catch (OperationCanceledException)
            {
                HideProgressBar();
                _isGenerating = false;
                if (this != null)
                {
                    SetStatus(Strings.Status.Cancelled, StatusColor.Neutral);
                    UpdateGenerateAvailability();
                }
                return;
            }
            catch (Exception ex)
            {
                Log.Error($"Generation threw: {ex}");
                HideProgressBar();
                _isGenerating = false;
                if (this == null) return;
                HandleGenerationError(new ApiError
                {
                    CodeRaw = ApiErrorCode.NetworkError.ToString(),
                    Message = ex.Message,
                });
                UpdateGenerateAvailability();
                return;
            }

            _isGenerating = false;
            if (this == null) { HideProgressBar(); return; }

            if (!outcome.Ok)
            {
                HideProgressBar();
                HandleGenerationError(outcome.Error);
                UpdateGenerateAvailability();
                return;
            }

            _currentPhase = "done";
            _targetProgress = 100f;
            _progressHideTime = EditorApplication.timeSinceStartup + 1.0;
            HandleGenerationSuccess(outcome);
            PushHistoryOnSuccess(invoice, prompt, duration, seed, outcome);
            // Refresh quota so the banner reflects generations_remaining + window_resets_at.
            StartStatus(invoice);
            UpdateGenerateAvailability();
        }

        private void PushHistoryOnSuccess(
            string invoice,
            string prompt,
            int duration,
            int? seed,
            GenerationOrchestrator.Outcome outcome)
        {
            // Cache hits don't represent new content — they'd already be in
            // history from the original generation, so skip them.
            if (_history == null || outcome == null || outcome.Cached) return;

            _history.Add(new HistoryEntry
            {
                TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                JobId = outcome.JobId,
                Prompt = prompt,
                DurationSeconds = duration,
                Seed = seed ?? -1,
                FbxAssetPath = outcome.FbxPath,
                InvoiceAtCreation = invoice,
            });
            _historyPanel?.MarkDirtyRepaint();
        }

        private void HandleGenerationSuccess(GenerationOrchestrator.Outcome outcome)
        {
            _lastError = null;
            _autoRetryAttempts = 0;

            var desiredName = !string.IsNullOrWhiteSpace(_pendingClipName)
                ? _pendingClipName
                : _pendingPrompt;
            RenameImportedClip(outcome.FbxPath, ClipApplier.Slugify(desiredName));

            var clip = ImportedAnimationLocator.GetClip(outcome.FbxPath);
            if (clip == null)
            {
                SetStatus(string.Format(Strings.Status.DoneNoClipFormat, outcome.FbxPath), StatusColor.Success);
                Log.Warn($"Generation produced FBX but no clip was extracted: {outcome.FbxPath}");
                return;
            }

            _serializedClip = clip;
            _preview?.SetClip(clip);
            if (_scrubSlider != null)
            {
                _scrubSlider.highValue = Mathf.Max(clip.length, 0.01f);
                _scrubSlider.SetValueWithoutNotify(0f);
            }
            _preview?.Play();
            _lastEditorTime = EditorApplication.timeSinceStartup;
            UpdatePlayPauseIcon();
            UpdateTimeLabel();
            UpdateScrubSlider();
            _previewViewport?.MarkDirtyRepaint();

            var cachedNote = outcome.Cached ? " (cached — no allowance used)" : string.Empty;
            SetStatus(
                $"Done — clip '{clip.name}' ({clip.length:0.00}s).{cachedNote}  Generations remaining: {outcome.GenerationsRemaining}.",
                StatusColor.Success);
            Log.Info($"Generation complete: {outcome.FbxPath} → clip '{clip.name}' (length={clip.length:0.00}s)");
        }

        private static void RenameImportedClip(string fbxPath, string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return;
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            if (clips[0].name == clipName) return;
            clips[0].name = clipName;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private void HandleGenerationError(ApiError error)
        {
            var presentation = ErrorPresenter.Map(error);
            _lastError = presentation;

            // Auto-retry once for RATE_LIMITED / SERVICE_UNAVAILABLE
            if (presentation.Action == ErrorAction.AutoRetry &&
                presentation.AutoRetrySeconds.HasValue &&
                _autoRetryAttempts == 0)
            {
                _autoRetryAttempts = 1;
                _autoRetryAt = DateTime.UtcNow.AddSeconds(presentation.AutoRetrySeconds.Value);
                SetStatus(presentation.Message, StatusColor.Error);
                return;
            }

            SetStatus(presentation.Message, StatusColor.Error);
            Log.Error($"Generation failed ({error?.CodeRaw}): {presentation.Message}");

            switch (presentation.Action)
            {
                case ErrorAction.EmailSupport:
                    if (EditorUtility.DisplayDialog(
                            "PromptMotion",
                            presentation.Message + "\n\nEmail support now?",
                            "Email support",
                            "Dismiss"))
                    {
                        Application.OpenURL(ErrorPresenter.SupportEmailUrl);
                    }
                    break;
                case ErrorAction.RefreshStatus:
                    var invoice = SecureStore.Load();
                    if (!string.IsNullOrEmpty(invoice)) StartStatus(invoice);
                    break;
            }
        }

        private void TickAutoRetry()
        {
            if (_lastError == null) return;
            if (_lastError.Action != ErrorAction.AutoRetry) return;
            if (_autoRetryAt == default) return;

            var remaining = (int)Math.Ceiling((_autoRetryAt - DateTime.UtcNow).TotalSeconds);
            if (remaining > 0)
            {
                var msg = $"{StripCountdown(_lastError.Message)} Retrying in {remaining}s…";
                if (_generationStatus != null && _generationStatus.text != msg)
                {
                    _generationStatus.text = msg;
                }
                return;
            }

            // Time to retry once.
            if (_isGenerating) return;
            var prompt = _pendingPrompt;
            var duration = _pendingDuration;
            var seed = _pendingSeed;
            CancelAutoRetry();
            if (string.IsNullOrEmpty(prompt)) return;
            var invoice = SecureStore.Load();
            if (string.IsNullOrEmpty(invoice)) return;
            _ = DoGenerate(invoice, prompt, duration, seed);
        }

        private void CancelAutoRetry()
        {
            _autoRetryAt = default;
        }

        private static string StripCountdown(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            var idx = message.IndexOf(" Trying again in ", StringComparison.Ordinal);
            if (idx < 0) idx = message.IndexOf(" Retrying in ", StringComparison.Ordinal);
            return idx < 0 ? message : message.Substring(0, idx);
        }

        private void OnCancelClicked()
        {
            if (_generateCts == null || _generateCts.IsCancellationRequested) return;
            Log.Info("User cancelled generation.");
            _generateCts.Cancel();
        }

        // === Apply (preserved from legacy) ===

        private void OnApplyClicked()
        {
            if (_serializedCharacter == null || _serializedClip == null) return;
            if (!_isCharacterUsable)
            {
                SetStatus(SharedStrings.Validation.NotUsable, StatusColor.Error);
                return;
            }

            // Resolve desired clip name: prefer the user's text-field value
            // captured at generate time; fall back to live field; final fallback
            // to the prompt (Slugify handles "" -> "clip").
            var desired = !string.IsNullOrWhiteSpace(_pendingClipName)
                ? _pendingClipName
                : _clipNameField?.value;
            if (string.IsNullOrWhiteSpace(desired))
            {
                desired = _promptField?.value;
            }

            ClipApplier.ApplyResult result;
            try
            {
                result = ClipApplier.Apply(_serializedCharacter, _serializedClip, desired);
            }
            catch (Exception ex)
            {
                Log.Error($"Apply threw: {ex}");
                SetStatus(string.Format(SharedStrings.Apply.ErrorGenericFormat, ex.Message), StatusColor.Error);
                return;
            }

            if (result.Success)
            {
                SetStatus(result.Message, StatusColor.Success);
                Log.Info(result.Message);
                if (result.Controller != null)
                {
                    EditorGUIUtility.PingObject(result.Controller);
                }
            }
            else
            {
                SetStatus(result.Message, StatusColor.Error);
                Log.Error(result.Message);
            }
        }

        // === Progress bar ===

        private void ShowProgressBar()
        {
            if (_progressContainer == null) return;
            _progressContainer.style.display = DisplayStyle.Flex;
            if (_generationStatus != null)
                _generationStatus.style.display = DisplayStyle.None;
            _progressBar.style.backgroundColor = Theme.ProgressBarTrack;
            var border = new StyleColor(Theme.Divider);
            _progressBar.style.borderTopColor = _progressBar.style.borderBottomColor =
                _progressBar.style.borderLeftColor = _progressBar.style.borderRightColor = border;
            _progressFill.style.backgroundColor = Theme.ProgressBarFill;
            _progressFill.style.width = new Length(0, LengthUnit.Percent);
            _phaseLabel.style.color = Theme.StatusNeutral;
            _displayedProgress = 0f;
            _targetProgress = 0f;
            _currentPhase = "queued";
            _progressHideTime = 0;
            _lastPhaseText = _lastPctText = _lastElapsedText = null;
            _lastProgressTickTime = EditorApplication.timeSinceStartup;
            _phaseLabel.text = PhaseDisplayLabel("queued");
            _progressLabel.text = "0%";
            _elapsedLabel.text = "0.0s";
        }

        private void HideProgressBar()
        {
            if (_progressContainer == null) return;
            _progressContainer.style.display = DisplayStyle.None;
            _progressHideTime = 0;
            if (_generationStatus != null)
                _generationStatus.style.display = DisplayStyle.Flex;
        }

        private void TickProgressBar()
        {
            if (_progressContainer == null ||
                _progressContainer.resolvedStyle.display == DisplayStyle.None) return;

            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - _lastProgressTickTime);
            _lastProgressTickTime = now;

            if (_isGenerating && _currentPhase != "downloading" &&
                _currentPhase != "importing" && _currentPhase != "done")
            {
                var elapsed = (float)(now - _generationStartTime);
                float target;
                string phase;
                if (elapsed < 0.5f)
                {
                    target = Mathf.Lerp(0f, 5f, elapsed / 0.5f);
                    phase = "queued";
                }
                else if (elapsed < 22.5f)
                {
                    target = Mathf.Lerp(5f, 78f, (elapsed - 0.5f) / 22f);
                    phase = "generating";
                }
                else if (elapsed < 24.5f)
                {
                    target = Mathf.Lerp(78f, 88f, (elapsed - 22.5f) / 2f);
                    phase = "retargeting";
                }
                else
                {
                    target = Mathf.Min(88f + (elapsed - 24.5f) * 0.12f, 95f);
                    phase = "retargeting";
                }
                _targetProgress = Mathf.Max(_targetProgress, target);
                _currentPhase = phase;
            }

            _displayedProgress = Mathf.MoveTowards(_displayedProgress, _targetProgress, 60f * dt);

            UpdateProgressUI();

            if (_progressHideTime > 0 && now >= _progressHideTime)
                HideProgressBar();
        }

        private void UpdateProgressUI()
        {
            if (_progressFill == null) return;
            _progressFill.style.width = new Length(_displayedProgress, LengthUnit.Percent);

            var phaseText = PhaseDisplayLabel(_currentPhase);
            if (phaseText != _lastPhaseText)
            {
                _lastPhaseText = phaseText;
                _phaseLabel.text = phaseText;
                _phaseLabel.style.color = _currentPhase == "done"
                    ? new StyleColor(Theme.StatusSuccess)
                    : new StyleColor(Theme.StatusNeutral);
            }

            var pctText = $"{Mathf.RoundToInt(_displayedProgress)}%";
            if (pctText != _lastPctText)
            {
                _lastPctText = pctText;
                _progressLabel.text = pctText;
            }

            var elapsed = EditorApplication.timeSinceStartup - _generationStartTime;
            var elapsedText = $"{elapsed:0.0}s";
            if (elapsedText != _lastElapsedText)
            {
                _lastElapsedText = elapsedText;
                _elapsedLabel.text = elapsedText;
            }
        }

        private static string PhaseDisplayLabel(string phase) => phase switch
        {
            "queued" => "Queued...",
            "generating" => "Generating motion...",
            "retargeting" => "Retargeting...",
            "downloading" => "Downloading animation...",
            "importing" => "Importing...",
            "done" => "Done!",
            _ => "Working...",
        };

        // === Helpers ===

        private enum StatusColor { Neutral, Success, Error }

        private void SetStatus(string message, StatusColor color)
        {
            if (_generationStatus == null) return;
            _generationStatus.text = message;
            _generationStatus.style.color = color switch
            {
                StatusColor.Success => Theme.StatusSuccess,
                StatusColor.Error => Theme.StatusError,
                _ => Theme.StatusNeutral
            };
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max - 1) + "…";
        }

        private Label CreateStatusLabel(ValidationReport report, long elapsedMs)
        {
            Label label;
            if (!report.IsUsable)
            {
                label = new Label(SharedStrings.Validation.NotUsable);
                label.style.color = Theme.StatusError;
            }
            else if (report.HasIssues)
            {
                label = new Label(SharedStrings.Validation.ReadyWithWarnings);
                label.style.color = Theme.StatusWarning;
            }
            else
            {
                var charName = _serializedCharacter != null ? _serializedCharacter.name : "character";
                label = new Label($"✓ Humanoid — '{charName}'");
                label.style.color = Theme.StatusSuccess;
            }
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.tooltip = $"Validated in {elapsedMs} ms";
            return label;
        }

        private VisualElement CreateIssueRow(ValidationIssue issue)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginTop = 2;
            row.style.marginBottom = 2;

            string iconName;
            switch (issue.Severity)
            {
                case ValidationSeverity.Error:
                    iconName = "console.erroricon";
                    break;
                case ValidationSeverity.Warning:
                    iconName = "console.warnicon";
                    break;
                default:
                    iconName = "console.infoicon";
                    break;
            }
            var iconContent = EditorGUIUtility.IconContent(iconName);
            var icon = new Image { image = iconContent != null ? iconContent.image : null };
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 6;
            icon.style.marginTop = 2;
            icon.style.flexShrink = 0;
            row.Add(icon);

            var msg = new Label(issue.Message);
            msg.style.whiteSpace = WhiteSpace.Normal;
            msg.style.flexGrow = 1;
            msg.style.flexShrink = 1;
            msg.style.flexBasis = 0;
            row.Add(msg);

            if (issue.HasFix)
            {
                var fixBtn = new Button();
                fixBtn.text = issue.FixLabel ?? Strings.Window.ButtonFix;
                fixBtn.clicked += () =>
                {
                    issue.Fix();
                    RefreshValidation(_serializedCharacter);
                };
                fixBtn.style.marginLeft = 6;
                fixBtn.style.flexShrink = 0;
                fixBtn.style.minWidth = 80;
                fixBtn.style.alignSelf = Align.Center;
                row.Add(fixBtn);
            }

            return row;
        }

        // === Test hooks ===

        internal WindowState StateForTest => _state;
        internal TrialAuthBanner BannerForTest => _banner;
        internal Button GenerateButtonForTest => _generateButton;
        internal bool IsGeneratingForTest => _isGenerating;
        internal Task DoGenerateForTest(string invoice, string prompt, int duration, int? seed)
            => DoGenerate(invoice, prompt, duration, seed);

        internal sealed class RecordingFbxSink : GenerationOrchestrator.IFbxSink
        {
            public int SaveCount;
            public string Save(string jobId, byte[] bytes)
            {
                SaveCount++;
                return $"Assets/PromptMotion/Generated/{jobId}.fbx";
            }
        }

        internal static PromptMotionWindow OpenForTest(IHttpTransport transport)
        {
            // Drop any leftover instance from a prior test so the new window
            // assembles its UI with our injected transport. Use DestroyImmediate
            // rather than Close() because windows we find here were
            // CreateInstance'd (never Show'd), and Close() on such an un-shown
            // window NREs inside Unity's internal hide/dock logic.
            var existing = Resources.FindObjectsOfTypeAll<PromptMotionWindow>();
            foreach (var w in existing)
            {
                if (w == null) continue;
                DestroyImmediate(w);
            }
            var window = CreateInstance<PromptMotionWindow>();
            window.TransportOverrideForTest = transport;
            var inMemorySettings = CreateInstance<PromptMotionSettings>();
            inMemorySettings.hideFlags = HideFlags.HideAndDontSave;
            window.SettingsOverrideForTest = inMemorySettings;
            window.SinkOverrideForTest = new RecordingFbxSink();
            window.titleContent = new GUIContent("PromptMotion (test)");
            window.minSize = new Vector2(480, 600);
            _ = window.rootVisualElement;
            window.CreateGUI();
            return window;
        }
    }
}
