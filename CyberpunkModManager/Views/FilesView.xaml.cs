using System.Windows.Controls;
using CyberpunkModManager.ViewModels;

namespace CyberpunkModManager.Views
{
    public partial class FilesView : UserControl
    {
        private readonly FilesViewModel _viewModel;

        public FilesView()
        {
            InitializeComponent();
            _viewModel = new FilesViewModel();
            DataContext = _viewModel;
        }

        public void RefreshFileList()
        {
            _viewModel.Reload();
        }
    }
}
