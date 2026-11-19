#nullable enable

using System;
using System.Windows.Input;

namespace DesktopRider.Core.ViewModels
{
    /// <summary>A menu or button action that is always available.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) =>
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
