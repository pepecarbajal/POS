using System.Windows;
using System.Windows.Input;

namespace POS.Ventanas
{
    public partial class PasswordDialog : Window
    {
        private const string ADMIN_PASSWORD = "";

        public bool IsAuthenticated { get; private set; } = false;

        public PasswordDialog()
        {
            InitializeComponent();
            PasswordInput.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ValidatePassword();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsAuthenticated = false;
            Close();
        }

        private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ValidatePassword();
            }
        }

        private void ValidatePassword()
        {
            if (PasswordInput.Password == ADMIN_PASSWORD)
            {
                IsAuthenticated = true;
                Close();
            }
            else
            {
                MessageBox.Show("Clave incorrecta. Intente nuevamente.",
                                "Error de Autenticación",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                PasswordInput.Clear();
                PasswordInput.Focus();
            }
        }
    }
}