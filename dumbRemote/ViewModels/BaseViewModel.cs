/*
 * =========================================================================================
 * File: BaseViewModel.cs
 * Namespace: dumbRemote.ViewModels
 * Author: Radim Kopunec
 * Description: Base class for ViewModels implementing INotifyPropertyChanged.
 * Simplifies UI data binding updates.
 * =========================================================================================
 */

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace dumbRemote.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels. Implements INotifyPropertyChanged to support data binding.
    /// </summary>
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Updates the property value and notifies the UI if the value has changed.
        /// </summary>
        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}