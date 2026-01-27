using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;
using TvojeNamespace;
#if ANDROID
using AndroidX.Core.View;
using Android.Content.Res;
using Android.OS;
using Android.Views;
#endif

namespace MauiApp1
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
#if ANDROID
                .ConfigureMauiHandlers(handlers =>
                {
                    handlers.AddHandler(typeof(CustomEntry), typeof(CustomEntryHandler));
                })
#endif
                ;
#if ANDROID
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddAndroid(android =>
                    android.OnCreate((activity, bundle) =>
                    {
                        StatusBarHelper.UpdateStatusBarColors(Application.Current.RequestedTheme);
                    }));
            });
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }

    public static class StatusBarHelper
    {
        public static void UpdateStatusBarColors(AppTheme theme)
        {
#if ANDROID
            var activity = Platform.CurrentActivity;
            bool isDark = theme == AppTheme.Dark;
            var mauiColor = isDark
                ? Microsoft.Maui.Graphics.Colors.Black
                : Microsoft.Maui.Graphics.Colors.White;
            var window = activity.Window;
            window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
            window.ClearFlags(WindowManagerFlags.TranslucentStatus);
            window.SetStatusBarColor(mauiColor.ToPlatform());

            var controller = WindowCompat.GetInsetsController(window, window.DecorView);
            controller.AppearanceLightStatusBars = !isDark;
#endif
        }
    }

}
