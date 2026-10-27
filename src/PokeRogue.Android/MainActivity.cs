using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Microsoft.Xna.Framework;
using PokeRogue.Game;

namespace PokeRogue.Android;

/// <summary>
/// The app: on first launch (and after every update) it unpacks the game's content from the APK into its own folder,
/// showing how far it got, then starts the game full screen and sideways.
/// </summary>
[Activity(
    Label = "PokeRogue",
    MainLauncher = true,
    Icon = "@drawable/icon",
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout)]
public sealed class MainActivity : AndroidGameActivity
{
    private PokeRogueGame? _game;
    private TextView? _progress;

    protected override void OnCreate(Bundle? bundle)
    {
        base.OnCreate(bundle);
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        if (OperatingSystem.IsAndroidVersionAtLeast(28) && Window?.Attributes is { } attributes)
        {
            // Phones with a notch: the game may draw beside it, sideways; set before the window is laid out.
            attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
            Window.Attributes = attributes;
        }

        var root = Path.Combine(FilesDir!.AbsolutePath, "content");
        if (ContentUnpacker.IsCurrent(this, root))
        {
            StartGame(root);
            return;
        }

        // MonoGame starts its loop when the activity resumes, so the game must exist by then: the files are unpacked
        // first, behind a progress line, and the activity starts again to run the game.
        _progress = new TextView(this) { Text = "PokeRogue", Gravity = GravityFlags.Center, TextSize = 20 };
        _progress.SetTextColor(global::Android.Graphics.Color.White);
        _progress.SetBackgroundColor(global::Android.Graphics.Color.Rgb(10, 8, 16));
        SetContentView(_progress);
        HideSystemBars();

        Task.Run(() =>
        {
            try
            {
                ContentUnpacker.Unpack(this, root, percent => RunOnUiThread(() => _progress.Text = $"Preparazione dei file… {percent}%"));
                RunOnUiThread(Recreate);
            }
            catch (Exception exception)
            {
                RunOnUiThread(() => _progress.Text = "Impossibile preparare i file del gioco:\n" + exception.Message);
            }
        });
    }

    private void StartGame(string root)
    {
        _game = new PokeRogueGame([], root);
        var view = (View)_game.Services.GetService(typeof(View))!;
        var game = _game;
        view.LayoutChange += (_, e) => game.ViewLaidOut(e.Right - e.Left, e.Bottom - e.Top);
        SetContentView(view);
        HideSystemBars();
        _game.Run();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
            HideSystemBars();
    }

    /// <summary>The status and navigation bars stay hidden; a swipe from the edge shows them for a moment.</summary>
    private void HideSystemBars()
    {
        if (Window is not { } window)
            return;

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            if (window.InsetsController is { } controller)
            {
                controller.Hide(WindowInsets.Type.SystemBars());
                controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
        }
        else
        {
#pragma warning disable CA1422 // the old way, for Android 7–10
            window.DecorView.SystemUiFlags = SystemUiFlags.Fullscreen | SystemUiFlags.HideNavigation | SystemUiFlags.ImmersiveSticky | SystemUiFlags.LayoutStable
                                             | SystemUiFlags.LayoutFullscreen | SystemUiFlags.LayoutHideNavigation;
#pragma warning restore CA1422
        }
    }
}
