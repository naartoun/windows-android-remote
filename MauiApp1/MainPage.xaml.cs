using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using MauiApp1.Popups;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.ApplicationModel;
using System.Numerics;

#if ANDROID
using Android.App;
using Android.Content;
using global::Android.Views;
using Android.Views.InputMethods;
#elif IOS
using UIKit;
#endif


namespace MauiApp1
{
    public partial class MainPage : ContentPage
    {
        public readonly WebSocketClient wsClient = new WebSocketClient();
        bool isConnected;
        CancellationTokenSource upCts, downCts, leftCts, rightCts, moveCts;

        Point touchpadCenter;
        Point panStart;
        double knobStartX, knobStartY;
        double maxKnobDistance;
        bool knobGrabbed = false;

        public MainPage()
        {
            InitializeComponent();
            wsClient.Disconnected += (_, __) =>
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    isConnected = false;
                    ConnectButton.Text = "Odpojeno";
                    ConnectButton.BackgroundColor = Colors.LightGray;
                });

            TouchpadLayout.SizeChanged += (_, __) => UpdateMaxKnobGeometry();
            Knob.SizeChanged += (_, __) => UpdateMaxKnobGeometry();
            TouchpadArea.SizeChanged += (_, __) => UpdateMaxKnobGeometry();

            var pan = new PanGestureRecognizer();
            pan.PanUpdated += OnTouchpadPanUpdated;
            TouchpadLayout.GestureRecognizers.Add(pan);

            var tap = new TapGestureRecognizer();
            tap.Tapped += OnTouchpadTapped;
            TouchpadLayout.GestureRecognizers.Add(tap);

            var knobTap = new TapGestureRecognizer();
            knobTap.Tapped += (s, e) =>
            {
                _ = wsClient.SendMessageAsync("CLICK:MOUSELEFT");
            };
            Knob.GestureRecognizers.Add(knobTap);

            /*KeyboardEntry.BackspacePressed += (s, e) =>
            {
                _ = wsClient.SendMessageAsync("BACKSPACE");
            };*/
        }

        protected override void OnSizeAllocated(double w, double h)
        {
            base.OnSizeAllocated(w, h);
            const double dw = 400, dh = 800;
            var scale = Math.Min(w / dw, h / dh);
            RootLayout.Scale = scale;
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();
            UpdateMaxKnobGeometry();
        }

        async void OnConnectClicked(object s, EventArgs e)
        {
            if (!isConnected)
            {
                try
                {
                    //toto je moje tady ip await wsClient.ConnectAsync("ws://192.168.2.145:8080/ws/");
                    await wsClient.ConnectAsync("ws://192.168.0.83:8080/ws/"); //toto je ip na dumbPC notebook
                    isConnected = true;
                    ConnectButton.Text = "Připojeno";
                    ConnectButton.BackgroundColor = Colors.LightGreen;
                }
                catch (Exception ex) { await DisplayAlert("Chyba", ex.Message, "OK"); }
            }
            else
            {
                await wsClient.DisconnectAsync();
            }
        }

        // TOUCHPAD
        void UpdateMaxKnobGeometry()
        {
            if (TouchpadLayout.Width <= 0 || TouchpadLayout.Height <= 0 || Knob.Width <= 0 || Knob.Height <= 0)
                return;

            // střed touchpadu
            touchpadCenter = new Point(TouchpadLayout.Width / 2d, TouchpadLayout.Height / 2d);

            // bezpečný okraj, aby byl knob vždy celý uvnitř (v DIPs)
            const double safeMargin = 10d;

            // max vzdálenost středu knobu od středu touchpadu
            maxKnobDistance = ((TouchpadLayout.Width - Knob.Width) / 2d) - safeMargin;
            if (maxKnobDistance < 0) maxKnobDistance = 0;

            // po změně geometrie případně zacentrej (pokud zrovna netaháš)
            if (!knobGrabbed)
                CenterKnob();
        }

        void EnsureMaxUpToDate()
        {
            // pro jistotu přepočítej těsně před použitím
            if (maxKnobDistance <= 0 || Knob.Width <= 0)
                UpdateMaxKnobGeometry();
        }

        void OnTouchpadClicked(object _, EventArgs __)
        {
            ControlWheel.IsVisible = !ControlWheel.IsVisible;
            TouchpadArea.IsVisible = !TouchpadArea.IsVisible;
            if (TouchpadArea.IsVisible)
            {
                KeyboardEntry.IsVisible = false;
            }
        }

        void CenterKnob()
        {
            StopMoveLoop();
            Knob.TranslationX = 0;
            Knob.TranslationY = 0;
            _ = wsClient.SendMessageAsync("MOVE:0:0");
        }

        void EnsureMoveLoop()
        {
            if (moveCts != null && !moveCts.IsCancellationRequested) return;

            moveCts = new CancellationTokenSource();
            var token = moveCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var offset = new Vector2((float)Knob.TranslationX, (float)Knob.TranslationY);
                    if (Math.Abs(offset.X) < 0.5 && Math.Abs(offset.Y) < 0.5)
                    {
                        await Task.Delay(50, token);
                        continue;
                    }
                    var normalized = offset / (float)maxKnobDistance;
                    normalized = Vector2.Clamp(normalized, new Vector2(-1, -1), new Vector2(1, 1));
                    int dx = (int)(normalized.X * 100);
                    int dy = (int)(normalized.Y * 100);
                    await wsClient.SendMessageAsync($"MOVE:{dx}:{dy}");

                    await Task.Delay(50, token); // interval opakování
                }
            }, token);
        }

        void StopMoveLoop()
        {
            try { moveCts?.Cancel(); } catch { }
        }


        bool IsPointInKnob(Point point)
        {
            var layout = (Layout)TouchpadArea.Content;

            double knobCenterX = layout.Width / 2 + Knob.TranslationX;
            double knobCenterY = layout.Height / 2 + Knob.TranslationY;

            double dx = point.X - knobCenterX;
            double dy = point.Y - knobCenterY;

            double radius = Knob.Width / 2 + 10;

            return dx * dx + dy * dy <= radius * radius;
        }

        void MoveKnobTo(Point p)
        {
            EnsureMaxUpToDate();

            Vector2 offset = new((float)(p.X - touchpadCenter.X), (float)(p.Y - touchpadCenter.Y));
            var max = (float)maxKnobDistance;

            if (offset.Length() > max && max > 0)
                offset = Vector2.Normalize(offset) * max;

            Knob.TranslationX = offset.X;
            Knob.TranslationY = offset.Y;

            SendMove(offset);
        }


        void SendMove(Vector2 offset)
        {
            var normalized = offset / (float)maxKnobDistance;

            // klipujeme na [-1,1] pro rychlost
            normalized = Vector2.Clamp(normalized, new Vector2(-1, -1), new Vector2(1, 1));

            int dx = (int)(normalized.X * 100);  // procenta rychlosti
            int dy = (int)(normalized.Y * 100);
            _ = wsClient.SendMessageAsync($"MOVE:{dx}:{dy}");
        }

        void OnTouchpadTapped(object sender, TappedEventArgs e)
        {
            Point pos = (Microsoft.Maui.Graphics.Point)e.GetPosition(TouchpadLayout);
            if (IsPointInKnob(pos))
            {
                _ = wsClient.SendMessageAsync("CLICK:MOUSELEFT");
                return;
            }

            MoveKnobTo(pos);
            CenterKnob(); // simulace puštění
        }

        void OnKnobPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    knobStartX = Knob.TranslationX;
                    knobStartY = Knob.TranslationY;
                    break;

                case GestureStatus.Running:
                    EnsureMaxUpToDate();
                    double newX = knobStartX + e.TotalX;
                    double newY = knobStartY + e.TotalY;

                    double max = maxKnobDistance;
                    newX = Math.Max(-max, Math.Min(max, newX));
                    newY = Math.Max(-max, Math.Min(max, newY));

                    Knob.TranslationX = newX;
                    Knob.TranslationY = newY;

                    if (Math.Abs(newX) > 0.5 || Math.Abs(newY) > 0.5)
                        EnsureMoveLoop();
                    else
                        StopMoveLoop();
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    StopMoveLoop();
                    CenterKnob();
                    break;
            }
        }


        void OnTouchpadPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            var layout = (Layout)TouchpadArea.Content;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    knobStartX = Knob.TranslationX;
                    knobStartY = Knob.TranslationY;

                    // relativní pozice dotyku uvnitř layoutu
                    var relPoint = new Point(e.TotalX + touchpadCenter.X, e.TotalY + touchpadCenter.Y);
                    knobGrabbed = IsPointInKnob(relPoint);
                    break;

                case GestureStatus.Running:
                    if (!knobGrabbed) return;
                    EnsureMaxUpToDate();

                    double newX = knobStartX + e.TotalX;
                    double newY = knobStartY + e.TotalY;

                    double max = maxKnobDistance;
                    newX = Math.Max(-max, Math.Min(max, newX));
                    newY = Math.Max(-max, Math.Min(max, newY));

                    Knob.TranslationX = newX;
                    Knob.TranslationY = newY;

                    SendMove(new Vector2((float)newX, (float)newY));
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    knobGrabbed = false;
                    CenterKnob();
                    break;
            }
        }

        // MOUSE LEFT/RIGHT
        async void OnMouseLeftClicked(object s, EventArgs e) =>
            await wsClient.SendMessageAsync("CLICK:MOUSELEFT");
        async void OnMouseRightClicked(object s, EventArgs e) =>
            await wsClient.SendMessageAsync("CLICK:MOUSERIGHT");

        // CURSOR HOLD
        async Task Repeat(string cmd, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await wsClient.SendMessageAsync(cmd);
                await Task.Delay(100, token);
            }
        }

        void OnUpPressed(object _, EventArgs __) { upCts = new(); _ = Repeat("CLICK:UP", upCts.Token); }
        void OnUpReleased(object _, EventArgs __) => upCts?.Cancel();
        void OnDownPressed(object _, EventArgs __) { downCts = new(); _ = Repeat("CLICK:DOWN", downCts.Token); }
        void OnDownReleased(object _, EventArgs __) => downCts?.Cancel();
        void OnLeftPressed(object _, EventArgs __) { leftCts = new(); _ = Repeat("CLICK:LEFT", leftCts.Token); }
        void OnLeftReleased(object _, EventArgs __) => leftCts?.Cancel();
        void OnRightPressed(object _, EventArgs __) { rightCts = new(); _ = Repeat("CLICK:RIGHT", rightCts.Token); }
        void OnRightReleased(object _, EventArgs __) => rightCts?.Cancel();

        // BACK / HOME
        async void OnBackClicked(object _, EventArgs __) =>
            await wsClient.SendMessageAsync("CLICK:BACK");
        async void OnHomeClicked(object _, EventArgs __) =>
            await wsClient.SendMessageAsync("CLICK:HOME");


        // KEYBOARD
        bool keyboardVisible = false;

        void OnKeyboardClicked(object _, EventArgs __)
        {
            if (!keyboardVisible)
            {
                KeyboardEntry.Focus();
                KeyboardEntry.IsVisible = true;
                keyboardVisible = true;
            }
            else
            {
                KeyboardEntry.Unfocus();
#if ANDROID
                if (KeyboardEntry.Handler.PlatformView is global::Android.Views.View nativeView)
                {
                    var activity = Platform.CurrentActivity as Activity;
                    var imm = activity?.GetSystemService(Context.InputMethodService) as InputMethodManager;
                    imm?.HideSoftInputFromWindow(nativeView.WindowToken, HideSoftInputFlags.None);
                }
#elif IOS
                        UIApplication.SharedApplication.KeyWindow.EndEditing(true);
#endif
                KeyboardEntry.IsVisible = false;
                keyboardVisible = false;
                KeyboardEntry.Text = "                                                                                                                                                                                                                                                       ";
            }
        }
        private void KeyboardEntry_Completed(object sender, EventArgs e)
        {
            KeyboardEntry.IsVisible = false;
            keyboardVisible = false;
            _ = wsClient.SendMessageAsync("TYPE:\n");//mozna se ma dat na prvni misto v teto funkci
            KeyboardEntry.Text = "                                                                                                                                                                                                                                                       ";
        }


        void KeyboardEntry_TextChanged(object s, TextChangedEventArgs e)
        {
            var newText = e.NewTextValue ?? "";
            var oldText = e.OldTextValue ?? "";

            int added = newText.Length - oldText.Length;

            if (added > 0)
            {
                var c = newText.Last();
                _ = wsClient.SendMessageAsync($"TYPE:{c}");
            }
            else if (added < 0)
            {
                _ = wsClient.SendMessageAsync("BACKSPACE");
            }
        }

        // VOLUME
        async void OnMuteClicked(object _, EventArgs __) =>
            await wsClient.SendMessageAsync("CLICK:MUTE");
        async void OnVolumeDownClicked(object _, EventArgs __) =>
            await wsClient.SendMessageAsync("CLICK:VOLDOWN");
        async void OnVolumeUpClicked(object _, EventArgs __) =>
            await wsClient.SendMessageAsync("CLICK:VOLUP");
    }
}
