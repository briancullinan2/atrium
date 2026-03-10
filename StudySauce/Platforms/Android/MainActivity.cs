using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace StudySauce
{
    [Activity(
        Theme = "@style/MainTheme.NoActionBar", // Point to your new style here
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // This "encourages" Android to hide the bars immediately upon creation
            if (Window != null)
            {
                // For modern Android (API 30+)
                if (OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    Window.InsetsController?.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
                    Window.InsetsController?.SystemBarsBehavior = AndroidX.Core.View.WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
                }
                else
                {
                    // Fallback for older devices
#pragma warning disable CS0618
                    Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                        SystemUiFlags.Fullscreen |
                        SystemUiFlags.HideNavigation |
                        SystemUiFlags.ImmersiveSticky);
#pragma warning restore CS0618
                }
            }
        }
    }
}
