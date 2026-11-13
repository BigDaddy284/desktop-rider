#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopRider.Core.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>Tells the interface that every value may have changed.</summary>
        protected void OnAllPropertiesChanged() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
