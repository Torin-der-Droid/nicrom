using System;
using PromptMotion.Auth;
using PromptMotion.Http;
using PromptMotion.Windows;
using PromptMotion.Shared.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace PromptMotion.Views
{
    /// <summary>
    /// Top section of the Lite window. Drives the free-trial activation flow:
    /// NeedEmail → CodeSent → Busy → Ready. Pure presentation + event surface —
    /// the window owns business logic and calls the right Render method based on
    /// its state machine. When the lifetime quota is exhausted the Ready state
    /// surfaces an upsell to the paid editions.
    /// </summary>
    internal sealed class TrialAuthBanner : VisualElement
    {
        // Public events — the owning window subscribes to drive transitions.
        public event Action<string> SendCodeRequested;   // arg = typed email
        public event Action<string> ActivateRequested;   // arg = typed code
        public event Action ResendRequested;
        public event Action ChangeEmailRequested;
        public event Action CancelRequested;
        public event Action RefreshStatusRequested;
        public event Action UpgradeStandardRequested;
        public event Action UpgradeProRequested;

        private readonly Label _heading = new Label();
        private readonly Label _body = new Label();
        private readonly Label _errorLabel = new Label();
        private readonly TextField _emailField;
        private readonly TextField _codeField;
        private readonly Button _resendLink;
        private readonly Button _primaryButton;
        private readonly Button _secondaryButton;
        private readonly VisualElement _upsellBox;

        private AuthState _currentState = AuthState.NeedEmail;

        public TrialAuthBanner()
        {
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.paddingLeft = 10;
            style.paddingRight = 10;
            style.marginBottom = 8;
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 4;

            _heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            _heading.style.fontSize = 14;
            _heading.style.marginBottom = 6;
            _heading.style.whiteSpace = WhiteSpace.Normal;
            Add(_heading);

            _body.style.whiteSpace = WhiteSpace.Normal;
            _body.style.marginBottom = 8;
            Add(_body);

            _errorLabel.style.whiteSpace = WhiteSpace.Normal;
            _errorLabel.style.marginBottom = 8;
            _errorLabel.style.color = Theme.StatusError;
            _errorLabel.style.display = DisplayStyle.None;
            Add(_errorLabel);

            _emailField = new TextField("Email")
            {
                value = string.Empty,
                tooltip = "We'll email you a 6-digit activation code. Your address is used only to grant the free trial — not for marketing.",
            };
            _emailField.style.marginBottom = 6;
            _emailField.style.display = DisplayStyle.None;
            Add(_emailField);

            _codeField = new TextField("6-digit code")
            {
                value = string.Empty,
                maxLength = 6,
                tooltip = "Enter the code from the email we just sent you.",
            };
            _codeField.style.marginBottom = 6;
            _codeField.style.display = DisplayStyle.None;
            Add(_codeField);

            _resendLink = MakeLinkButton("Resend code / use a different email", () =>
            {
                // The resend link doubles as "I didn't get it / wrong address":
                // a single click re-sends, but we also expose Change email as the
                // secondary button when in CodeSent.
                ResendRequested?.Invoke();
            });
            _resendLink.style.display = DisplayStyle.None;
            Add(_resendLink);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 4;

            _primaryButton = new Button { text = "Send code" };
            _primaryButton.style.flexGrow = 1;
            _primaryButton.style.height = 26;
            buttonRow.Add(_primaryButton);

            _secondaryButton = new Button { text = "Cancel" };
            _secondaryButton.style.marginLeft = 6;
            _secondaryButton.style.height = 26;
            _secondaryButton.style.display = DisplayStyle.None;
            buttonRow.Add(_secondaryButton);

            Add(buttonRow);

            _upsellBox = BuildUpsellBox();
            _upsellBox.style.display = DisplayStyle.None;
            Add(_upsellBox);

            _emailField.RegisterValueChangedCallback(_ => UpdatePrimaryButtonEnabled());
            _codeField.RegisterValueChangedCallback(_ => UpdatePrimaryButtonEnabled());
            _primaryButton.clicked += OnPrimaryClicked;
        }

        // === Render states ===

        public void RenderNeedEmail(string pendingEmail, ApiError error)
        {
            _currentState = AuthState.NeedEmail;
            ApplyBackground(BannerSeverity.Info);

            _heading.text = "PromptMotion Lite — 5 free generations";
            _body.text = "No purchase or account needed. Enter your email and we'll send a " +
                         "6-digit code to unlock 5 lifetime AI animation generations.";

            _emailField.style.display = DisplayStyle.Flex;
            if (!string.IsNullOrEmpty(pendingEmail))
                _emailField.SetValueWithoutNotify(pendingEmail);
            _codeField.style.display = DisplayStyle.None;
            _resendLink.style.display = DisplayStyle.None;
            // Surface the upgrade options up-front, before activation.
            _upsellBox.style.display = DisplayStyle.Flex;

            _primaryButton.text = "Send code";
            _primaryButton.style.display = DisplayStyle.Flex;
            _secondaryButton.style.display = DisplayStyle.None;

            UpdatePrimaryButtonEnabled();
            ShowError(error);
        }

        public void RenderCodeSent(string email, ApiError error)
        {
            _currentState = AuthState.CodeSent;
            ApplyBackground(BannerSeverity.Info);

            _heading.text = "Check your email";
            _body.text = string.IsNullOrEmpty(email)
                ? "Enter the 6-digit code we emailed you. It expires in 15 minutes."
                : $"We emailed a 6-digit code to {email}. Enter it below to activate — it expires in 15 minutes.";

            _emailField.style.display = DisplayStyle.None;
            _codeField.style.display = DisplayStyle.Flex;
            _codeField.SetValueWithoutNotify(string.Empty);
            _resendLink.style.display = DisplayStyle.Flex;
            _upsellBox.style.display = DisplayStyle.None;

            _primaryButton.text = "Activate";
            _primaryButton.style.display = DisplayStyle.Flex;
            _secondaryButton.text = "Change email";
            _secondaryButton.style.display = DisplayStyle.Flex;
            WireSecondary(() => ChangeEmailRequested?.Invoke());

            UpdatePrimaryButtonEnabled();
            ShowError(error);
        }

        public void RenderBusy(string message)
        {
            _currentState = AuthState.Busy;
            ApplyBackground(BannerSeverity.Info);

            _heading.text = message;
            _body.text = string.Empty;
            _emailField.style.display = DisplayStyle.None;
            _codeField.style.display = DisplayStyle.None;
            _resendLink.style.display = DisplayStyle.None;
            _upsellBox.style.display = DisplayStyle.None;

            _primaryButton.style.display = DisplayStyle.None;
            _secondaryButton.text = "Cancel";
            _secondaryButton.style.display = DisplayStyle.Flex;
            WireSecondary(() => CancelRequested?.Invoke());

            ShowError(null);
        }

        public void RenderReady(string email, int used, int quota)
        {
            _currentState = AuthState.Ready;

            var remaining = Math.Max(0, quota - used);
            var exhausted = quota > 0 && remaining == 0;
            ApplyBackground(exhausted ? BannerSeverity.Warning : BannerSeverity.Success);

            _emailField.style.display = DisplayStyle.None;
            _codeField.style.display = DisplayStyle.None;
            _resendLink.style.display = DisplayStyle.None;

            _heading.text = exhausted ? "Free trial used up" : "✓ PromptMotion Lite activated";

            var who = string.IsNullOrEmpty(email) ? string.Empty : $" ({email})";
            _body.text = exhausted
                ? $"You've used all {quota} free generations{who}. Upgrade for a monthly allowance — your already-generated clips stay yours."
                : $"{remaining} of {quota} free generations remaining{who}. Cached prompts don't count.";
            _body.style.color = exhausted
                ? new StyleColor(Theme.StatusWarning)
                : new StyleColor(StyleKeyword.Null);

            _upsellBox.style.display = exhausted ? DisplayStyle.Flex : DisplayStyle.None;

            _primaryButton.text = "Refresh";
            _primaryButton.style.display = DisplayStyle.Flex;
            _primaryButton.SetEnabled(true);

            _secondaryButton.text = "Use a different email";
            _secondaryButton.style.display = DisplayStyle.Flex;
            WireSecondary(() => ChangeEmailRequested?.Invoke());

            ShowError(null);
        }

        // === Upsell ===

        private VisualElement BuildUpsellBox()
        {
            var box = new VisualElement();
            box.style.marginTop = 8;
            box.style.paddingTop = 8;
            box.style.borderTopWidth = 1;
            box.style.borderTopColor = Theme.Divider;

            var heading = new Label("Need more than 5 generations?");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = 4;
            box.Add(heading);

            var blurb = new Label(
                "PromptMotion (Standard) gives you 50 generations every month. " +
                "PromptMotion Pro adds 150/month plus the Pro animation toolset.");
            blurb.style.whiteSpace = WhiteSpace.Normal;
            blurb.style.color = Theme.MutedText;
            blurb.style.marginBottom = 6;
            box.Add(blurb);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var standard = new Button(() => UpgradeStandardRequested?.Invoke()) { text = "Get Standard (50/mo)" };
            standard.style.flexGrow = 1;
            standard.style.height = 26;
            row.Add(standard);

            var pro = new Button(() => UpgradeProRequested?.Invoke()) { text = "Get Pro (150/mo)" };
            pro.style.flexGrow = 1;
            pro.style.height = 26;
            pro.style.marginLeft = 6;
            row.Add(pro);

            box.Add(row);
            return box;
        }

        // === Button plumbing ===

        private void UpdatePrimaryButtonEnabled()
        {
            switch (_currentState)
            {
                case AuthState.NeedEmail:
                    _primaryButton.SetEnabled(LooksLikeEmail(_emailField.value));
                    break;
                case AuthState.CodeSent:
                    _primaryButton.SetEnabled(LooksLikeCode(_codeField.value));
                    break;
                default:
                    _primaryButton.SetEnabled(true);
                    break;
            }
        }

        private void OnPrimaryClicked()
        {
            switch (_currentState)
            {
                case AuthState.NeedEmail:
                    var email = _emailField.value?.Trim();
                    if (LooksLikeEmail(email)) SendCodeRequested?.Invoke(email);
                    break;
                case AuthState.CodeSent:
                    var code = _codeField.value?.Trim();
                    if (LooksLikeCode(code)) ActivateRequested?.Invoke(code);
                    break;
                case AuthState.Ready:
                    RefreshStatusRequested?.Invoke();
                    break;
            }
        }

        private Action _secondaryHandler;

        private void WireSecondary(Action handler)
        {
            // Replace the click handler each render so a stale binding from a
            // previous state doesn't fire a different event.
            _secondaryHandler = handler;
            _secondaryButton.clickable = new Clickable(() => _secondaryHandler?.Invoke());
        }

        private static Button MakeLinkButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.alignSelf = Align.FlexStart;
            b.style.marginBottom = 8;
            b.style.backgroundColor = new StyleColor(StyleKeyword.None);
            b.style.borderTopWidth = b.style.borderBottomWidth =
                b.style.borderLeftWidth = b.style.borderRightWidth = 0;
            b.style.color = Theme.MutedText;
            return b;
        }

        private static bool LooksLikeEmail(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            var at = s.IndexOf('@');
            return at > 0 && at < s.Length - 1 && s.IndexOf('.', at) > at;
        }

        private static bool LooksLikeCode(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.Length != 6) return false;
            foreach (var c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        private void ShowError(ApiError error)
        {
            if (error == null)
            {
                _errorLabel.style.display = DisplayStyle.None;
                _errorLabel.text = string.Empty;
                return;
            }
            _errorLabel.text = ErrorMessages.ForBanner(error);
            _errorLabel.style.display = DisplayStyle.Flex;
        }

        private enum BannerSeverity { Info, Success, Warning }

        private void ApplyBackground(BannerSeverity severity)
        {
            Color bg;
            StyleColor border;
            switch (severity)
            {
                case BannerSeverity.Success:
                    bg = new Color(0.20f, 0.35f, 0.20f, 0.25f);
                    border = Theme.StatusSuccess;
                    break;
                case BannerSeverity.Warning:
                    bg = new Color(0.40f, 0.32f, 0.15f, 0.25f);
                    border = Theme.StatusWarning;
                    break;
                default:
                    bg = new Color(0.20f, 0.25f, 0.40f, 0.20f);
                    border = Theme.MutedText;
                    break;
            }

            style.backgroundColor = bg;
            style.borderTopColor = border;
            style.borderBottomColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
        }

        // === Test-only access ===
        internal string PrimaryButtonText => _primaryButton.text;
        internal bool PrimaryButtonEnabled => _primaryButton.enabledSelf;
        internal string ErrorTextForTest => _errorLabel.text;
        internal bool ErrorVisibleForTest => _errorLabel.style.display.value == DisplayStyle.Flex;
        internal void SetEmailTextForTest(string value)
        {
            _emailField.value = value;
            UpdatePrimaryButtonEnabled();
        }
        internal void SetCodeTextForTest(string value)
        {
            _codeField.value = value;
            UpdatePrimaryButtonEnabled();
        }
        internal void ClickPrimaryForTest() => OnPrimaryClicked();
        internal void ClickSecondaryForTest() => _secondaryHandler?.Invoke();
        internal AuthState StateForTest => _currentState;
    }

    internal static class ErrorMessages
    {
        public static string ForBanner(ApiError error)
        {
            if (error == null) return string.Empty;
            switch (error.Code)
            {
                case ApiErrorCode.InvalidEmail:
                    return "That email address doesn't look right. Please check it and try again.";
                case ApiErrorCode.DisposableEmail:
                    return "Disposable / throwaway email providers aren't supported. Please use a personal or work email.";
                case ApiErrorCode.TrialAlreadyUsed:
                    return "This email has already used its free trial. Use a different email, or upgrade for a monthly allowance.";
                case ApiErrorCode.TrialNonceConflict:
                    return "This machine's free trial is already linked to its original email. Re-enter that email to keep using it, or upgrade to Standard/Pro for a monthly allowance.";
                case ApiErrorCode.IpRateLimited:
                    return "This network has already requested a trial recently. Please try again later.";
                case ApiErrorCode.TrialCodeExpired:
                    return "That code has expired. Click \"Resend code\" to get a new one.";
                case ApiErrorCode.TrialCodeInvalid:
                    return "That code isn't valid. Double-check the digits from your email and try again.";
                case ApiErrorCode.NetworkError:
                case ApiErrorCode.Timeout:
                    return "Couldn't reach the server. Check your internet connection and try again.";
                case ApiErrorCode.BadRequest:
                    return "Something's wrong with the request. Please try again.";
                case ApiErrorCode.ServerError:
                case ApiErrorCode.ServiceUnavailable:
                case ApiErrorCode.UpstreamError:
                    return "Server is temporarily unavailable (this includes our email service). Try again in a few minutes.";
                default:
                    return string.IsNullOrEmpty(error.Message)
                        ? "An error occurred."
                        : "An error occurred. " + error.Message;
            }
        }
    }
}
