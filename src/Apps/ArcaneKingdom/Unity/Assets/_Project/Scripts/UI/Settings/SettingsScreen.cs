#nullable enable
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Settings
{
    /// <summary>
    /// Einstellungs-Screen: Audio (Musik + Effekte + Mute), Sprache, Account-Aktionen,
    /// Gameplay (Vibration + Notifications + Tutorial-Reset), About.
    /// Persistierung erfolgt aktuell nicht — kommt mit PreferencesService-Anbindung.
    /// </summary>
    public sealed class SettingsScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ToastService _toast;
        private readonly IAuthService _auth;
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAudioService _audio;

        private Button _backBtn = null!;
        private Slider _musicSlider = null!;
        private Slider _sfxSlider = null!;
        private Label _musicValue = null!;
        private Label _sfxValue = null!;
        private Toggle _muteToggle = null!;
        private DropdownField _languageDropdown = null!;
        private Label _accountId = null!;
        private Label _accountServer = null!;
        private Button _cloudLinkBtn = null!;
        private Button _logoutBtn = null!;
        private Toggle _hapticsToggle = null!;
        private Toggle _notificationsToggle = null!;
        private Button _resetTutorialBtn = null!;
        private Label _versionLabel = null!;
        private Button _privacyBtn = null!;

        public override string Id => ScreenId.Settings;
        protected override string UxmlPath => "UI/SettingsScreen";

        public SettingsScreen(ScreenManager screenManager,
                              ToastService toast,
                              IAuthService auth,
                              ISaveService<PlayerSave> save,
                              IAudioService audio)
        {
            _screenManager = screenManager;
            _toast = toast;
            _auth = auth;
            _save = save;
            _audio = audio;
        }

        protected override void BindElements(VisualElement root)
        {
            _backBtn             = Q<Button>("settings-back-button");
            _musicSlider         = Q<Slider>("settings-music-slider");
            _sfxSlider           = Q<Slider>("settings-sfx-slider");
            _musicValue          = Q<Label>("settings-music-value");
            _sfxValue            = Q<Label>("settings-sfx-value");
            _muteToggle          = Q<Toggle>("settings-mute-toggle");
            _languageDropdown    = Q<DropdownField>("settings-language-dropdown");
            _accountId           = Q<Label>("settings-account-id");
            _accountServer       = Q<Label>("settings-account-server");
            _cloudLinkBtn        = Q<Button>("settings-cloud-link");
            _logoutBtn           = Q<Button>("settings-logout");
            _hapticsToggle       = Q<Toggle>("settings-haptics-toggle");
            _notificationsToggle = Q<Toggle>("settings-notifications-toggle");
            _resetTutorialBtn    = Q<Button>("settings-reset-tutorial");
            _versionLabel        = Q<Label>("settings-version");
            _privacyBtn          = Q<Button>("settings-open-privacy");

            _languageDropdown.choices = new List<string> { "Deutsch", "English", "Espanol", "Francais", "Italiano", "Portugues" };
            _languageDropdown.index = 0;

            _backBtn.clicked += () => _screenManager.PopAsync().Forget();

            _musicSlider.RegisterValueChangedCallback(evt =>
            {
                _musicValue.text = $"{(int)evt.newValue} %";
                if (_audio != null) _audio.MusicVolume = evt.newValue / 100f;
            });
            _sfxSlider.RegisterValueChangedCallback(evt =>
            {
                _sfxValue.text = $"{(int)evt.newValue} %";
                if (_audio != null) _audio.SfxVolume = evt.newValue / 100f;
            });
            _muteToggle.RegisterValueChangedCallback(evt =>
            {
                if (_audio != null) _audio.MasterVolume = evt.newValue ? 0f : 1f;
            });

            _languageDropdown.RegisterValueChangedCallback(evt =>
                _toast.Show($"Sprache: {evt.newValue} (Localization-Switch folgt).", ToastKind.Info));

            _cloudLinkBtn.clicked += () =>
                _toast.Show("Cloud-Sign-In folgt mit Firebase Auth.", ToastKind.Info);
            _logoutBtn.clicked += OnLogout;

            _hapticsToggle.RegisterValueChangedCallback(_ =>
                _toast.Show("Haptik-Setting gespeichert.", ToastKind.Success));
            _notificationsToggle.RegisterValueChangedCallback(_ =>
                _toast.Show("Notifications-Setting gespeichert.", ToastKind.Success));
            _resetTutorialBtn.clicked += OnResetTutorial;
            _privacyBtn.clicked += () =>
                Application.OpenURL("https://meineapps.example.com/privacy");
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            _accountId.text = $"User: {_auth.CurrentUserId ?? "anonymous"}";

            var result = await _save.LoadAsync(ct);
            if (result.IsSuccess && result.Value != null)
            {
                _accountServer.text = $"Server: {result.Value.Profile.Server}";
            }

            _versionLabel.text = $"ArcaneKingdom v{Application.version}";
        }

        private void OnLogout()
        {
            _toast.Show("Logout-Flow folgt mit Auth-Backend.", ToastKind.Info);
            // TODO: _auth.SignOutAsync + zurueck zum Login-Screen
            // _screenManager.ReplaceAsync(ScreenId.Login).Forget();
        }

        private void OnResetTutorial()
        {
            ResetTutorialAsync().Forget();
        }

        private async UniTask ResetTutorialAsync()
        {
            var loadResult = await _save.LoadAsync();
            if (!loadResult.IsSuccess || loadResult.Value == null) return;

            await _save.MutateAsync(save =>
            {
                save.Tutorial = new ArcaneKingdom.Domain.Tutorial.TutorialProgress();
                return save;
            });
            _toast.Show("Tutorial wird beim naechsten Hub-Open neu gestartet.", ToastKind.Success);
            GameLogger.Info("Settings", "Tutorial-Fortschritt zurueckgesetzt.");
        }
    }
}
