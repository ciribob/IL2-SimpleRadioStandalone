using System;
using System.Windows.Input;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils
{
    public class DelegateCommand : ICommand
    {
        private readonly Action<object> _action;
        private readonly Func<object, bool> _canExecute;

        public DelegateCommand(Action action, Func<bool> canExecute = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
            _action = _ => action();

            if (canExecute == null)
            {
                _canExecute = _ => true;
            }
            else
            {
                _canExecute = _ => canExecute();
            }
        }

        public DelegateCommand(Action<object> action, Func<object, bool> canExecute = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
            _action = action;

            if (canExecute == null)
            {
                _canExecute = _ => true;
            }
            else
            {
                _canExecute = canExecute;
            }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _action(parameter);
        }

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            var tmp = CanExecuteChanged;
            tmp?.Invoke(this, EventArgs.Empty);
        }
    }
}