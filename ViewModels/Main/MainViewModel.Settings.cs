using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Caro_game.ViewModels;

public partial class MainViewModel
{
    private const string SettingsFileName = "settings.json";

    private class AppSettings
    {
        public string? Player1Name { get; set; }
        public string? Player1Avatar { get; set; }
        public string? Player2Name { get; set; }
        public string? Player2Avatar { get; set; }
        public string? Theme { get; set; }
        public bool IsSoundEnabled { get; set; }
        public bool IsMusicEnabled { get; set; }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                Player1Name = Player1.Name,
                Player1Avatar = Player1.AvatarPath,
                Player2Name = Player2.Name,
                Player2Avatar = Player2.AvatarPath,
                Theme = SelectedTheme,
                IsSoundEnabled = IsSoundEnabled,
                IsMusicEnabled = IsMusicEnabled
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            File.WriteAllText(settingsPath, json);

            ApplyTheme();
            MessageBox.Show("Cài đặt đã được áp dụng và lưu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể lưu cài đặt.\nChi tiết: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadSettings()
    {
        try
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            if (!File.Exists(settingsPath))
            {
                return;
            }

            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings != null)
            {
                if (!string.IsNullOrEmpty(settings.Player1Name))
                {
                    Player1.Name = settings.Player1Name;
                }
                if (!string.IsNullOrEmpty(settings.Player1Avatar) && File.Exists(settings.Player1Avatar))
                {
                    Player1.AvatarPath = settings.Player1Avatar;
                }
                if (!string.IsNullOrEmpty(settings.Player2Name))
                {
                    Player2.Name = settings.Player2Name;
                }
                if (!string.IsNullOrEmpty(settings.Player2Avatar) && File.Exists(settings.Player2Avatar))
                {
                    Player2.AvatarPath = settings.Player2Avatar;
                }
                if (!string.IsNullOrEmpty(settings.Theme))
                {
                    SelectedTheme = settings.Theme;
                }
                IsSoundEnabled = settings.IsSoundEnabled;
                IsMusicEnabled = settings.IsMusicEnabled;
            }
        }
        catch (Exception)
        {
        }
    }

    private void ApplyTheme()
    {
        var themeUri = SelectedTheme == "Light" ? LightThemeUri : DarkThemeUri;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        ResourceDictionary? currentTheme = null;

        foreach (var dictionary in dictionaries)
        {
            if (dictionary.Source != null && dictionary.Source.OriginalString.Contains("Resources/Themes"))
            {
                currentTheme = dictionary;
                break;
            }
        }

        if (currentTheme != null && currentTheme.Source == themeUri)
        {
            return;
        }

        if (currentTheme != null)
        {
            dictionaries.Remove(currentTheme);
        }

        dictionaries.Add(new ResourceDictionary { Source = themeUri });
    }
}
