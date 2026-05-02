using System.Windows;

namespace D2TxtImporter.client
{
    public partial class ConfirmDialog : Window
    {
        public ConfirmDialog(string message, string title = null)
        {
            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(title))
            {
                Title = title;
            }
            MessageText.Text = message;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
