using System.Windows;

namespace Caro_game.ViewModels
{
    public partial class MainViewModel
    {
        private void SaveSettings()
        {
            ApplyTheme();
            MessageBox.Show("Cài đặt đã được áp dụng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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
}
