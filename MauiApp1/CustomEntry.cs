using Microsoft.Maui.Controls;
using System;

namespace TvojeNamespace
{
    public class CustomEntry : Entry
    {
        // můžeme sem přidat event pro backspace
        public event EventHandler BackspacePressed;

        public void SendBackspacePressed() => BackspacePressed?.Invoke(this, EventArgs.Empty);
    }
}