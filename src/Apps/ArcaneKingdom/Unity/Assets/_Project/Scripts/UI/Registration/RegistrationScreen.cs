#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Registration
{
    /// <summary>
    /// Registrierungs-Screen (Spielplan v5 Kap. 2 + Login-Hub-Plan Kap. 4).
    /// Erst-Spieler-Onboarding mit E-Mail/Passwort + Geburtsdatum + AGB-Checkboxen.
    /// Nach Erfolg: Account anlegen, Token speichern, weiter zur RaceSelection.
    /// </summary>
    public sealed class RegistrationScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly IAuthService _auth;
        private readonly ILocalizationService _loc;
        private readonly ToastService _toast;

        private TextField _displayNameField = null!;
        private TextField _emailField = null!;
        private TextField _emailConfirmField = null!;
        private TextField _passwordField = null!;
        private TextField _passwordConfirmField = null!;
        private Toggle _agbToggle = null!;
        private Toggle _privacyToggle = null!;
        private Toggle _verhaltenToggle = null!;
        private Button _submitButton = null!;
        private Button _backToLoginButton = null!;
        private Label _errorLabel = null!;
        private VisualElement _passwordHintBox = null!;

        public override string Id => ScreenId.Registration;
        protected override string UxmlPath => "UI/RegistrationScreen";

        public RegistrationScreen(ScreenManager screenManager,
                                   IAuthService auth,
                                   ILocalizationService loc,
                                   ToastService toast)
        {
            _screenManager = screenManager;
            _auth = auth;
            _loc = loc;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _displayNameField     = Q<TextField>("register-displayname");
            _emailField           = Q<TextField>("register-email");
            _emailConfirmField    = Q<TextField>("register-email-confirm");
            _passwordField        = Q<TextField>("register-password");
            _passwordConfirmField = Q<TextField>("register-password-confirm");
            _agbToggle            = Q<Toggle>("register-agb-toggle");
            _privacyToggle        = Q<Toggle>("register-privacy-toggle");
            _verhaltenToggle      = Q<Toggle>("register-verhalten-toggle");
            _submitButton         = Q<Button>("register-submit");
            _backToLoginButton    = Q<Button>("register-back-to-login");
            _errorLabel           = Q<Label>("register-error");
            _passwordHintBox      = Q<VisualElement>("register-password-hint");

            _passwordField.isPasswordField = true;
            _passwordConfirmField.isPasswordField = true;

            _submitButton.clicked += OnSubmitClicked;
            _backToLoginButton.clicked += OnBackToLoginClicked;

            _errorLabel.text = string.Empty;
            _errorLabel.style.display = DisplayStyle.None;
        }

        private void OnBackToLoginClicked()
        {
            _screenManager.ReplaceAsync(ScreenId.Login).Forget();
        }

        private void OnSubmitClicked()
        {
            RegisterAsync(default).Forget();
        }

        private async UniTask RegisterAsync(CancellationToken ct)
        {
            _errorLabel.style.display = DisplayStyle.None;
            _submitButton.SetEnabled(false);

            var validation = Validate();
            if (validation != null)
            {
                ShowError(validation);
                _submitButton.SetEnabled(true);
                return;
            }

            _submitButton.text = _loc.Get("register.submitting", "Registriere...");

            var result = await _auth.RegisterAsync(_emailField.value, _passwordField.value, _displayNameField.value, ct);
            _submitButton.SetEnabled(true);
            _submitButton.text = _loc.Get("register.submit", "Konto erstellen");

            if (!result.IsSuccess)
            {
                ShowError(result.ErrorMessage ?? _loc.Get("register.error.unknown", "Registrierung fehlgeschlagen."));
                _toast.Show(result.ErrorMessage ?? "Fehler", ToastKind.Danger, 5f);
                return;
            }

            // E-Mail lokal speichern (fuer Splash-Auto-Login-Erkennung)
            PlayerPrefs.SetString("last_user_email", _emailField.value);
            PlayerPrefs.SetString("auth_token", result.Value ?? string.Empty);
            PlayerPrefs.Save();

            _toast.Show(_loc.Get("register.success", "Konto erstellt!"), ToastKind.Success, 3f);

            // Erst-Spieler-Flow: direkt zur RaceSelection
            if (_screenManager.IsRegistered(ScreenId.RaceSelection))
                await _screenManager.ReplaceAsync(ScreenId.RaceSelection, ct);
            else
                await _screenManager.ReplaceAsync(ScreenId.Hub, ct);
        }

        private string? Validate()
        {
            if (string.IsNullOrWhiteSpace(_displayNameField.value))
                return _loc.Get("register.error.displayname", "Spielername erforderlich.");
            if (_displayNameField.value.Length < 3 || _displayNameField.value.Length > 20)
                return _loc.Get("register.error.displayname.length", "Spielername 3-20 Zeichen.");

            if (!IsValidEmail(_emailField.value))
                return _loc.Get("register.error.email", "Ungueltige E-Mail-Adresse.");
            if (_emailField.value != _emailConfirmField.value)
                return _loc.Get("register.error.email.mismatch", "E-Mail-Adressen stimmen nicht ueberein.");

            var passwordError = ValidatePassword(_passwordField.value);
            if (passwordError != null) return passwordError;
            if (_passwordField.value != _passwordConfirmField.value)
                return _loc.Get("register.error.password.mismatch", "Passwoerter stimmen nicht ueberein.");

            if (!_agbToggle.value)
                return _loc.Get("register.error.agb", "Nutzungsbedingungen muessen akzeptiert werden.");
            if (!_privacyToggle.value)
                return _loc.Get("register.error.privacy", "Datenschutz muss akzeptiert werden.");
            if (!_verhaltenToggle.value)
                return _loc.Get("register.error.verhalten", "Verhaltensregeln muessen akzeptiert werden.");

            return null;
        }

        private string? ValidatePassword(string pwd)
        {
            if (string.IsNullOrEmpty(pwd) || pwd.Length < 8)
                return _loc.Get("register.error.password.length", "Mind. 8 Zeichen.");
            var hasUpper = false; var hasLower = false; var hasDigit = false;
            foreach (var c in pwd)
            {
                if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsLower(c)) hasLower = true;
                else if (char.IsDigit(c)) hasDigit = true;
            }
            if (!hasUpper) return _loc.Get("register.error.password.upper", "Mind. ein Grossbuchstabe.");
            if (!hasLower) return _loc.Get("register.error.password.lower", "Mind. ein Kleinbuchstabe.");
            if (!hasDigit) return _loc.Get("register.error.password.digit", "Mind. eine Zahl.");
            if (pwd.Contains(_emailField.value, StringComparison.OrdinalIgnoreCase))
                return _loc.Get("register.error.password.email", "Passwort darf nicht die E-Mail enthalten.");
            if (!string.IsNullOrEmpty(_displayNameField.value)
                && pwd.Contains(_displayNameField.value, StringComparison.OrdinalIgnoreCase))
                return _loc.Get("register.error.password.name", "Passwort darf nicht den Namen enthalten.");
            // Keine 3-fache Wiederholung
            for (var i = 0; i < pwd.Length - 2; i++)
            {
                if (pwd[i] == pwd[i + 1] && pwd[i + 1] == pwd[i + 2])
                    return _loc.Get("register.error.password.repeat", "Keine 3-fache Zeichen-Wiederholung erlaubt.");
            }
            return null;
        }

        private static bool IsValidEmail(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            try { _ = new System.Net.Mail.MailAddress(value); return true; }
            catch { return false; }
        }

        private void ShowError(string msg)
        {
            _errorLabel.text = msg;
            _errorLabel.style.display = DisplayStyle.Flex;
            GameLogger.Warning("Registration", msg);
        }
    }
}
