#if ANDROID
using Android.Views;
using AndroidX.AppCompat.Widget;
using Microsoft.Maui.Handlers;

public class CustomEntryHandler : EntryHandler
{
    protected override AppCompatEditText CreatePlatformView()
    {
        var editText = new AppCompatEditText(Context);
        editText.KeyPress += (sender, e) =>
        {
            if (e.Event.Action == KeyEventActions.Down && e.KeyCode == Keycode.Del)
            {
                // backspace stisknut
                // tady můžeš upravit Entry.Text, pokud chceš
                var entry = (IEntry)VirtualView;
                if (!string.IsNullOrEmpty(entry.Text))
                    entry.Text = entry.Text.Substring(0, entry.Text.Length - 1);
            }
        };
        return editText;
    }
}
#endif
