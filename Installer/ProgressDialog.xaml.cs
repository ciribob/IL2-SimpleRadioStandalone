using System.Windows;

namespace Installer
{
    public partial class ProgressBarDialog : Window
    {
        public ProgressBarDialog()
        {
            InitializeComponent();
        }

        public void UpdateProgress(bool finished, string text)
        {
            Dispatcher?.Invoke(() =>
            {
                Status.Text = text;
                if (finished)
                {
                    Close();
                }
            });
        }
    }
}