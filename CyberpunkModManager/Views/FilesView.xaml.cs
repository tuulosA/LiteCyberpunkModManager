using System.Windows.Controls;
using CyberpunkModManager.ViewModels;

namespace CyberpunkModManager.Views
{
    public partial class FilesView : UserControl
    {
        public FilesView()
        {
            InitializeComponent();
            DataContext = new FilesViewModel(); // loads files on startup
        }
    }
}
