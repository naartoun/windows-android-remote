using System;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;  // pro LoadFromXaml

namespace MauiApp1.Popups
{
    public partial class MenuPopup : Popup
    {
        public MenuPopup()
        {
            // Načte odpovídající XAML podle typu
            this.LoadFromXaml(typeof(MenuPopup));
        }

        private void OnAdvancedClicked(object sender, EventArgs e)
        {
            Close("advanced");
        }

        private void OnShutdownClicked(object sender, EventArgs e)
        {
            Close("shutdown");
        }
    }
}
