namespace Atrium.Services
{
    public class AppSettingsService
    {
        public string ThemeColor { get; set; } = "Blue";

        // Notify the UI when a remote change happens
        public event Action? OnSettingsChanged;

        public void UpdateSettings(string newColor)
        {
            ThemeColor = newColor;
            OnSettingsChanged?.Invoke();
        }
    }
}
