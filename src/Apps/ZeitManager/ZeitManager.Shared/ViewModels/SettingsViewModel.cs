using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using ZeitManager.Models;
using ZeitManager.Services;

namespace ZeitManager.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localization;
    private readonly IPreferencesService _preferences;
    private readonly IAudioService _audioService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMidnightSelected))]
    [NotifyPropertyChangedFor(nameof(IsAuroraSelected))]
    [NotifyPropertyChangedFor(nameof(IsDaylightSelected))]
    [NotifyPropertyChangedFor(nameof(IsForestSelected))]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnglishSelected))]
    [NotifyPropertyChangedFor(nameof(IsGermanSelected))]
    [NotifyPropertyChangedFor(nameof(IsSpanishSelected))]
    [NotifyPropertyChangedFor(nameof(IsFrenchSelected))]
    [NotifyPropertyChangedFor(nameof(IsItalianSelected))]
    [NotifyPropertyChangedFor(nameof(IsPortugueseSelected))]
    private string _selectedLanguage;

    [ObservableProperty]
    private string _selectedTimerSound;

    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0"}";

    // Theme selection indicators
    public bool IsMidnightSelected => SelectedTheme == AppTheme.Midnight;
    public bool IsAuroraSelected => SelectedTheme == AppTheme.Aurora;
    public bool IsDaylightSelected => SelectedTheme == AppTheme.Daylight;
    public bool IsForestSelected => SelectedTheme == AppTheme.Forest;

    // Language selection indicators
    public bool IsEnglishSelected => SelectedLanguage == "en";
    public bool IsGermanSelected => SelectedLanguage == "de";
    public bool IsSpanishSelected => SelectedLanguage == "es";
    public bool IsFrenchSelected => SelectedLanguage == "fr";
    public bool IsItalianSelected => SelectedLanguage == "it";
    public bool IsPortugueseSelected => SelectedLanguage == "pt";

    // Localized strings
    public string SettingsTitle => _localization.GetString("SettingsTitle");
    public string ThemeText => _localization.GetString("Theme");
    public string LanguageText => _localization.GetString("Language");
    public string AboutText => _localization.GetString("About");
    public string VersionText => string.Format(_localization.GetString("VersionWithNumber"), AppVersion);
    public string SupportText => _localization.GetString("Support");
    public string FeedbackText => _localization.GetString("FeedbackButton");
    public string PrivacyPolicyText => _localization.GetString("PrivacyPolicy");
    public string TimerSoundText => _localization.GetString("TimerSound");
    public string TimerSoundDescriptionText => _localization.GetString("TimerSoundDescription");
    public string TestText => _localization.GetString("Test");
    public string PickFromDeviceText => _localization.GetString("PickFromDevice");

    // Theme names
    public string ThemeMidnightName => _localization.GetString("ThemeMidnight");
    public string ThemeAuroraName => _localization.GetString("ThemeAurora");
    public string ThemeDaylightName => _localization.GetString("ThemeDaylight");
    public string ThemeForestName => _localization.GetString("ThemeForest");

    [ObservableProperty]
    private IReadOnlyList<SoundItem> _timerSounds;

    private void RefreshTimerSounds() => TimerSounds = _audioService.AvailableSounds;

    public SettingsViewModel(
        IThemeService themeService,
        ILocalizationService localization,
        IPreferencesService preferences,
        IAudioService audioService)
    {
        _themeService = themeService;
        _localization = localization;
        _preferences = preferences;
        _audioService = audioService;
        _selectedTheme = _themeService.CurrentTheme;
        _selectedLanguage = _localization.CurrentLanguage;
        _selectedTimerSound = _preferences.Get("timer_sound", _audioService.DefaultTimerSound);
        _timerSounds = _audioService.AvailableSounds;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    partial void OnSelectedThemeChanged(AppTheme value) => _themeService.SetTheme(value);

    partial void OnSelectedTimerSoundChanged(string value) => _preferences.Set("timer_sound", value);

    [RelayCommand]
    private void SetTheme(AppTheme theme) => SelectedTheme = theme;

    [RelayCommand]
    private void SelectLanguage(string languageCode)
    {
        if (SelectedLanguage == languageCode) return;
        SelectedLanguage = languageCode;
        _localization.SetLanguage(languageCode);
    }

    [RelayCommand]
    private async Task PreviewTimerSound()
    {
        // Prüfen ob gewählter Sound eine URI hat (System/Custom Sound)
        var sound = TimerSounds.FirstOrDefault(s => s.Id == SelectedTimerSound);
        if (sound?.Uri != null)
            await _audioService.PlayUriAsync(sound.Uri);
        else
            await _audioService.PlayAsync(SelectedTimerSound);
    }

    [RelayCommand]
    private async Task PickTimerSound()
    {
        var picked = await _audioService.PickSoundAsync();
        if (picked != null)
        {
            RefreshTimerSounds();
            SelectedTimerSound = picked.Id;
        }
    }

    public event EventHandler<string>? MessageRequested;

    [RelayCommand]
    private void SendFeedback()
    {
        try
        {
            MeineApps.Core.Ava.Services.UriLauncher.OpenUri("mailto:info@rs-digital.org?subject=ZeitManager%20Feedback");
        }
        catch
        {
            MessageRequested?.Invoke(this, _localization.GetString("Error"));
        }
    }

    [RelayCommand]
    private void OpenPrivacyPolicy()
    {
        try
        {
            MeineApps.Core.Ava.Services.UriLauncher.OpenUri("https://rs-digital-studio.github.io/privacy/zeitmanager.html");
        }
        catch
        {
            MessageRequested?.Invoke(this, _localization.GetString("Error"));
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(string.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _localization.LanguageChanged -= OnLanguageChanged;

        GC.SuppressFinalize(this);
    }
}
