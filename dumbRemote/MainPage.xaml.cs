/*
 * =========================================================================================
 * File: MainPage.xaml.cs
 * Namespace: dumbRemote
 * Author: Radim Kopunec
 * Description: Code-behind for the main UI.
 * Handles specialized UI gestures (Touchpad, Hold-to-repeat) and delegates logic to ViewModel.
 * =========================================================================================
 */

using System.Numerics;
using dumbRemote.ViewModels;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Platform;

#if ANDROID
using Android.App;
using Android.Content;
using Android.Views.InputMethods;
#elif IOS
using UIKit;
#endif

namespace dumbRemote
{
    public partial class MainPage : ContentPage
    {
        // Reference to the ViewModel (Logic)
        private readonly MainViewModel _viewModel;

        // --- Touchpad State Variables ---
        private CancellationTokenSource? _repeatCts;
        private Point _touchpadCenter;
        private double _knobStartX, _knobStartY;
        private double _maxKnobDistance;
        private bool _knobGrabbed = false;
        private CancellationTokenSource? _moveLoopCts;

        // --- Keyboard State ---
        private bool _keyboardVisible = false;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();

            // Wire up ViewModel
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Setup Touchpad Geometry Events
            TouchpadLayout.SizeChanged += (_, __) => UpdateMaxKnobGeometry();
            Knob.SizeChanged += (_, __) => UpdateMaxKnobGeometry();
            TouchpadArea.SizeChanged += (_, __) => UpdateMaxKnobGeometry();

            // Setup Touchpad Gestures
            var pan = new PanGestureRecognizer();
            pan.PanUpdated += OnTouchpadPanUpdated;
            TouchpadLayout.GestureRecognizers.Add(pan);

            var tap = new TapGestureRecognizer();
            tap.Tapped += OnTouchpadTapped;
            TouchpadLayout.GestureRecognizers.Add(tap);

            // Setup Knob Click
            var knobTap = new TapGestureRecognizer();
            knobTap.Tapped += async (s, e) => _viewModel.SendCommand.Execute("CLICK:MOUSELEFT");
            Knob.GestureRecognizers.Add(knobTap);
        }

        protected override void OnSizeAllocated(double w, double h)
        {
            base.OnSizeAllocated(w, h);
            // Simple responsive scaling
            const double dw = 400, dh = 800;
            var scale = Math.Min(w / dw, h / dh);
            RootLayout.Scale = scale;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            UpdateMaxKnobGeometry();
        }

        #region Touchpad Logic

        void UpdateMaxKnobGeometry()
        {
            if (TouchpadLayout.Width <= 0 || TouchpadLayout.Height <= 0 || Knob.Width <= 0) return;

            _touchpadCenter = new Point(TouchpadLayout.Width / 2d, TouchpadLayout.Height / 2d);
            const double safeMargin = 10d;

            _maxKnobDistance = ((TouchpadLayout.Width - Knob.Width) / 2d) - safeMargin;
            if (_maxKnobDistance < 0) _maxKnobDistance = 0;

            if (!_knobGrabbed) CenterKnob();
        }

        void OnTouchpadClicked(object sender, EventArgs e)
        {
            // Toggle between D-Pad and Touchpad
            bool isTouchpadVisible = !TouchpadArea.IsVisible;
            ControlWheel.IsVisible = !isTouchpadVisible;
            TouchpadArea.IsVisible = isTouchpadVisible;
        }

        void CenterKnob()
        {
            StopMoveLoop();
            Knob.TranslationX = 0;
            Knob.TranslationY = 0;
            // Stop movement on server
            _ = _viewModel.SendMove(0, 0);
        }

        void OnTouchpadTapped(object? sender, TappedEventArgs e)
        {
            Point pos = (Point)e.GetPosition(TouchpadLayout);

            // Check if tapped directly on knob
            if (IsPointInKnob(pos))
            {
                _viewModel.SendCommand.Execute("CLICK:MOUSELEFT");
                return;
            }

            // Otherwise move knob there momentarily
            MoveKnobTo(pos);
            CenterKnob();
        }

        bool IsPointInKnob(Point point)
        {
            var layout = (Layout)TouchpadArea.Content;
            double knobCenterX = layout.Width / 2 + Knob.TranslationX;
            double knobCenterY = layout.Height / 2 + Knob.TranslationY;
            double dx = point.X - knobCenterX;
            double dy = point.Y - knobCenterY;
            double radius = Knob.Width / 2 + 10; // Hitbox slightly larger
            return dx * dx + dy * dy <= radius * radius;
        }

        void MoveKnobTo(Point p)
        {
            if (_maxKnobDistance <= 0) UpdateMaxKnobGeometry();

            Vector2 offset = new((float)(p.X - _touchpadCenter.X), (float)(p.Y - _touchpadCenter.Y));
            var max = (float)_maxKnobDistance;

            if (offset.Length() > max && max > 0)
                offset = Vector2.Normalize(offset) * max;

            Knob.TranslationX = offset.X;
            Knob.TranslationY = offset.Y;

            SendMoveFromOffset(offset);
        }

        void OnKnobPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(e, true);
        void OnTouchpadPanUpdated(object? sender, PanUpdatedEventArgs e) => HandlePan(e, false);

        void HandlePan(PanUpdatedEventArgs e, bool isKnobDirect)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _knobStartX = Knob.TranslationX;
                    _knobStartY = Knob.TranslationY;

                    if (!isKnobDirect)
                    {
                        var relPoint = new Point(e.TotalX + _touchpadCenter.X, e.TotalY + _touchpadCenter.Y);
                        _knobGrabbed = IsPointInKnob(relPoint);
                    }
                    else _knobGrabbed = true;
                    break;

                case GestureStatus.Running:
                    if (!_knobGrabbed) return;

                    double newX = _knobStartX + e.TotalX;
                    double newY = _knobStartY + e.TotalY;

                    double max = _maxKnobDistance;
                    // Clamp to circular area (simple box clamp for now is fast)
                    newX = Math.Max(-max, Math.Min(max, newX));
                    newY = Math.Max(-max, Math.Min(max, newY));

                    Knob.TranslationX = newX;
                    Knob.TranslationY = newY;

                    if (isKnobDirect)
                    {
                        // Joystick Mode (Continuous movement)
                        if (Math.Abs(newX) > 0.5 || Math.Abs(newY) > 0.5) EnsureMoveLoop();
                        else StopMoveLoop();
                    }
                    else
                    {
                        // Mouse Pad Mode (Relative movement)
                        SendMoveFromOffset(new Vector2((float)newX, (float)newY));
                    }
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    _knobGrabbed = false;
                    StopMoveLoop();
                    CenterKnob();
                    break;
            }
        }

        void SendMoveFromOffset(Vector2 offset)
        {
            var normalized = offset / (float)_maxKnobDistance;
            normalized = Vector2.Clamp(normalized, new Vector2(-1, -1), new Vector2(1, 1));

            int dx = (int)(normalized.X * 100);
            int dy = (int)(normalized.Y * 100);

            _ = _viewModel.SendMove(dx, dy);
        }

        void EnsureMoveLoop()
        {
            if (_moveLoopCts != null && !_moveLoopCts.IsCancellationRequested) return;

            _moveLoopCts = new CancellationTokenSource();
            var token = _moveLoopCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var offset = new Vector2((float)Knob.TranslationX, (float)Knob.TranslationY);
                    if (Math.Abs(offset.X) > 0.5 || Math.Abs(offset.Y) > 0.5)
                    {
                        SendMoveFromOffset(offset);
                    }
                    await Task.Delay(50, token);
                }
            }, token);
        }

        void StopMoveLoop()
        {
            try { _moveLoopCts?.Cancel(); } catch { }
            _moveLoopCts = null;
        }

        #endregion

        #region Connect button

        // --- Connect Button Long Press Logic ---
        private CancellationTokenSource? _longPressCts;
        private bool _isLongPressActionTriggered;

        private async void OnConnectPressed(object sender, EventArgs e)
        {
            _isLongPressActionTriggered = false;
            _longPressCts = new CancellationTokenSource();

            try
            {
                await Task.Delay(800, _longPressCts.Token);

                _isLongPressActionTriggered = true;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try { HapticFeedback.Perform(HapticFeedbackType.LongPress); } catch { }

                    _viewModel.ToggleIpEntryCommand.Execute(null);
                });
            }
            catch (TaskCanceledException) { }
        }

        private void OnConnectReleased(object sender, EventArgs e)
        {
            _longPressCts?.Cancel();

            if (!_isLongPressActionTriggered)
            {
                if (_viewModel.IsIpEntryVisible)
                {
                    _viewModel.ToggleIpEntryCommand.Execute(null);
                    _viewModel.ConnectCommand.Execute(null);
                }
                else
                {
                    _viewModel.ConnectCommand.Execute(null);
                }
            }
        }

        #endregion

        #region Buttons Hold-to-Repeat

        async Task RepeatCommand(string cmd, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _viewModel.SendCommand.Execute(cmd);
                await Task.Delay(100, token); // Repeat rate
            }
        }

        void OnUpPressed(object s, EventArgs e) { _repeatCts = new(); _ = RepeatCommand("CLICK:UP", _repeatCts.Token); }
        void OnUpReleased(object s, EventArgs e) => _repeatCts?.Cancel();

        void OnDownPressed(object s, EventArgs e) { _repeatCts = new(); _ = RepeatCommand("CLICK:DOWN", _repeatCts.Token); }
        void OnDownReleased(object s, EventArgs e) => _repeatCts?.Cancel();

        void OnLeftPressed(object s, EventArgs e) { _repeatCts = new(); _ = RepeatCommand("CLICK:LEFT", _repeatCts.Token); }
        void OnLeftReleased(object s, EventArgs e) => _repeatCts?.Cancel();

        void OnRightPressed(object s, EventArgs e) { _repeatCts = new(); _ = RepeatCommand("CLICK:RIGHT", _repeatCts.Token); }
        void OnRightReleased(object s, EventArgs e) => _repeatCts?.Cancel();

        #endregion

        #region Keyboard

        void OnKeyboardClicked(object sender, EventArgs e)
        {
            if (!_keyboardVisible)
            {
                KeyboardEntry.Focus();
                _keyboardVisible = true;
            }
            else
            {
                KeyboardEntry.Unfocus();
                HideKeyboard();
                _keyboardVisible = false;
                KeyboardEntry.Text = "";
            }
        }

        private void HideKeyboard()
        {
#if ANDROID
            if (KeyboardEntry.Handler?.PlatformView is Android.Views.View nativeView)
            {
                var activity = Platform.CurrentActivity as Activity;
                var imm = activity?.GetSystemService(Context.InputMethodService) as InputMethodManager;
                imm?.HideSoftInputFromWindow(nativeView.WindowToken, HideSoftInputFlags.None);
            }
#elif IOS
            UIApplication.SharedApplication.KeyWindow?.EndEditing(true);
#endif
        }

        private void KeyboardEntry_Completed(object sender, EventArgs e)
        {
            _keyboardVisible = false;
            _viewModel.TypeTextCommand.Execute("\n");
            KeyboardEntry.Text = "";
        }

        void KeyboardEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            var newText = e.NewTextValue ?? "";
            var oldText = e.OldTextValue ?? "";

            int added = newText.Length - oldText.Length;

            if (added > 0)
            {
                var c = newText.Last().ToString();
                _viewModel.TypeTextCommand.Execute(c);
            }
            else if (added < 0)
            {
                _viewModel.SendCommand.Execute("BACKSPACE");
            }
        }

        #endregion
    }
}