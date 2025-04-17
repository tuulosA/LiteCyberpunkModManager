using CyberpunkModManager.Models;
using CyberpunkModManager.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CyberpunkModManager.ViewModels
{
    public class ModListViewModel : INotifyPropertyChanged
    {
        private readonly NexusApiService _apiService;
        private string _statusMessage = "Ready.";

        public ObservableCollection<ModDisplay> Mods { get; set; } = new();
        public ListCollectionView ModsGrouped { get; set; }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public void RefreshModList()
        {
            // Notify the UI to re-read properties from items (especially for Status updates)
            ModsGrouped.Refresh();
        }

        public ModListViewModel(NexusApiService apiService)
        {
            _apiService = apiService;
            ModsGrouped = new ListCollectionView(Mods);
            ModsGrouped.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        }

        public async Task LoadTrackedModsAsync()
        {
            StatusMessage = "Fetching tracked mods...";
            Mods.Clear();

            var mods = await _apiService.GetTrackedModsAsync();

            if (mods.Count == 0)
            {
                StatusMessage = "No mods found or error loading mods.";
                return;
            }

            foreach (var mod in mods.OrderBy(m => m.Category).ThenBy(m => m.Name))
            {
                Mods.Add(new ModDisplay
                {
                    Name = mod.Name,
                    ModId = mod.ModId,
                    Category = mod.Category,
                    Status = "Not Downloaded" // You could hook in real status logic later
                });
            }

            StatusMessage = "Mods loaded.";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ModDisplay
    {
        public int ModId { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "Uncategorized";
        public string Status { get; set; } = "Unknown";
    }
}
