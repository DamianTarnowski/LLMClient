using LLMClient.Models;
using LLMClient.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace LLMClient.ViewModels
{
    public class MemoryPageViewModel : INotifyPropertyChanged
    {
        private readonly IMemoryService _memoryService;

        private ObservableCollection<Memory> _memories = new();
        private ObservableCollection<Memory> _filteredMemories = new();
        private ObservableCollection<string> _categories = new();
        private string _searchTerm = string.Empty;
        private string _selectedCategory = string.Empty;
        private bool _isEmpty;

        public ObservableCollection<Memory> Memories
        {
            get => _memories;
            set
            {
                _memories = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Memory> FilteredMemories
        {
            get => _filteredMemories;
            set
            {
                _filteredMemories = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> Categories
        {
            get => _categories;
            set
            {
                _categories = value;
                OnPropertyChanged();
            }
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                _searchTerm = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
            }
        }

        public bool IsEmpty
        {
            get => _isEmpty;
            set
            {
                _isEmpty = value;
                OnPropertyChanged();
            }
        }

        public ICommand BackToMainCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddMemoryCommand { get; }
        public ICommand EditMemoryCommand { get; }
        public ICommand DeleteMemoryCommand { get; }
        public ICommand FilterByCategoryCommand { get; }
        public ICommand DebugTestCommand { get; }

        public MemoryPageViewModel(IMemoryService memoryService)
        {
            _memoryService = memoryService;
            
            BackToMainCommand = new Command(async () => await BackToMainAsync());
            RefreshCommand = new Command(async () => await RefreshAsync());
            AddMemoryCommand = new Command(async () => await AddMemoryAsync());
            EditMemoryCommand = new Command<Memory>(async (memory) => await EditMemoryAsync(memory));
            DeleteMemoryCommand = new Command<Memory>(async (memory) => await DeleteMemoryAsync(memory));
            FilterByCategoryCommand = new Command<string>((category) => FilterByCategory(category));
            DebugTestCommand = new Command(async () => await RunDebugTestAsync());
        }

        public async Task InitializeAsync()
        {
            System.Diagnostics.Debug.WriteLine("[MemoryPageViewModel] InitializeAsync called");
            await RefreshAsync();
            System.Diagnostics.Debug.WriteLine("[MemoryPageViewModel] InitializeAsync completed");
        }

        private async Task BackToMainAsync()
        {
            await Shell.Current.GoToAsync("///MainPage");
        }

        private async Task RefreshAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MemoryPageViewModel] RefreshAsync started");
                
                var memoryList = await _memoryService.GetAllMemoriesAsync();
                System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] Retrieved {memoryList.Count} memories from service");
                
                Memories.Clear();
                foreach (var memory in memoryList)
                {
                    System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] Adding memory to UI: {memory.Key} = {memory.Value}");
                    Memories.Add(memory);
                }

                System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] Memories collection now has {Memories.Count} items");

                var categoryList = await _memoryService.GetCategoriesAsync();
                Categories.Clear();
                foreach (var category in categoryList)
                {
                    Categories.Add(category);
                }

                ApplyFilters();
                System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] FilteredMemories collection has {FilteredMemories.Count} items");
                System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] IsEmpty is set to: {IsEmpty}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] ERROR in RefreshAsync: {ex.Message}");
                await ShowErrorAsync("Błąd podczas odświeżania", ex.Message);
            }
        }

        private async Task AddMemoryAsync()
        {
            try
            {
                var result = await Application.Current.MainPage.DisplayPromptAsync(
                    "Nowe wspomnienie",
                    "Podaj klucz (identyfikator):",
                    "OK", "Anuluj",
                    placeholder: "np. imie_uzytkownika");

                if (string.IsNullOrWhiteSpace(result))
                    return;

                var key = result.Trim();

                var value = await Application.Current.MainPage.DisplayPromptAsync(
                    "Nowe wspomnienie",
                    $"Podaj wartość dla '{key}':",
                    "OK", "Anuluj",
                    placeholder: "Wartość do zapamiętania");

                if (string.IsNullOrWhiteSpace(value))
                    return;

                var category = await Application.Current.MainPage.DisplayPromptAsync(
                    "Kategoria (opcjonalna)",
                    "Podaj kategorię:",
                    "OK", "Pomiń",
                    placeholder: "np. osobiste, preferencje") ?? string.Empty;

                var tags = await Application.Current.MainPage.DisplayPromptAsync(
                    "Tagi (opcjonalne)",
                    "Podaj tagi oddzielone przecinkami:",
                    "OK", "Pomiń",
                    placeholder: "tag1, tag2, tag3") ?? string.Empty;

                var isImportant = await Application.Current.MainPage.DisplayAlert(
                    "Ważność",
                    "Czy ta informacja jest szczególnie ważna?",
                    "Tak", "Nie");

                await _memoryService.UpsertMemoryAsync(key, value.Trim(), category.Trim(), tags.Trim(), isImportant);
                await RefreshAsync();

                await ShowSuccessAsync("Dodano wspomnienie", $"Zapamiętano: {key} = {value.Trim()}");
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Błąd podczas dodawania", ex.Message);
            }
        }

        private async Task EditMemoryAsync(Memory memory)
        {
            try
            {
                var newValue = await Application.Current.MainPage.DisplayPromptAsync(
                    "Edytuj wspomnienie",
                    $"Edytuj wartość dla '{memory.Key}':",
                    "OK", "Anuluj",
                    initialValue: memory.Value);

                if (string.IsNullOrWhiteSpace(newValue) || newValue == memory.Value)
                    return;

                var category = await Application.Current.MainPage.DisplayPromptAsync(
                    "Kategoria",
                    "Edytuj kategorię:",
                    "OK", "Anuluj",
                    initialValue: memory.Category) ?? memory.Category;

                var tags = await Application.Current.MainPage.DisplayPromptAsync(
                    "Tagi",
                    "Edytuj tagi (oddzielone przecinkami):",
                    "OK", "Anuluj",
                    initialValue: memory.Tags) ?? memory.Tags;

                var isImportant = await Application.Current.MainPage.DisplayAlert(
                    "Ważność",
                    "Czy ta informacja jest szczególnie ważna?",
                    "Tak", "Nie");

                await _memoryService.UpsertMemoryAsync(memory.Key, newValue.Trim(), category.Trim(), tags.Trim(), isImportant);
                await RefreshAsync();

                await ShowSuccessAsync("Zaktualizowano", $"Zaktualizowano: {memory.Key} = {newValue.Trim()}");
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Błąd podczas edycji", ex.Message);
            }
        }

        private async Task DeleteMemoryAsync(Memory memory)
        {
            try
            {
                var confirm = await Application.Current.MainPage.DisplayAlert(
                    "Usuń wspomnienie",
                    $"Czy na pewno chcesz usunąć '{memory.Key}'?",
                    "Usuń", "Anuluj");

                if (!confirm)
                    return;

                await _memoryService.DeleteMemoryAsync(memory.Id);
                await RefreshAsync();

                await ShowSuccessAsync("Usunięto", $"Usunięto wspomnienie: {memory.Key}");
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Błąd podczas usuwania", ex.Message);
            }
        }

        private void FilterByCategory(string category)
        {
            SelectedCategory = category ?? string.Empty;
            ApplyFilters();
        }


        private void ApplyFilters()
        {
            System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] ApplyFilters called - Starting with {Memories.Count} memories");
            
            var filtered = Memories.AsEnumerable();

            // Filtruj po kategorii
            if (!string.IsNullOrWhiteSpace(SelectedCategory))
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] Filtering by category: {SelectedCategory}");
                filtered = filtered.Where(m => m.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
            }

            // Filtruj po wyszukiwaniu
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] Filtering by search term: {SearchTerm}");
                var searchLower = SearchTerm.ToLower();
                filtered = filtered.Where(m =>
                    m.Key.ToLower().Contains(searchLower) ||
                    m.Value.ToLower().Contains(searchLower) ||
                    m.Category.ToLower().Contains(searchLower) ||
                    m.Tags.ToLower().Contains(searchLower));
            }

            // Sortuj po dacie aktualizacji (najnowsze pierwsze)
            filtered = filtered.OrderByDescending(m => m.UpdatedAt);

            var filteredList = filtered.ToList();
            System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] After filtering: {filteredList.Count} memories remain");

            FilteredMemories.Clear();
            foreach (var memory in filteredList)
            {
                System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] Adding to FilteredMemories: {memory.Key}");
                FilteredMemories.Add(memory);
            }

            IsEmpty = !FilteredMemories.Any();
            System.Diagnostics.Debug.WriteLine($"[MemoryPageViewModel] ApplyFilters completed - IsEmpty: {IsEmpty}, FilteredMemories.Count: {FilteredMemories.Count}");
        }

        private async Task ShowErrorAsync(string title, string message)
        {
            await Application.Current.MainPage.DisplayAlert(title, message, "OK");
        }

        private async Task ShowSuccessAsync(string title, string message)
        {
            await Application.Current.MainPage.DisplayAlert(title, message, "OK");
        }

        private async Task RunDebugTestAsync()
        {
            try
            {
                var result = await DatabaseDebugHelper.TestMemoryPersistenceAsync(_memoryService);
                await Application.Current.MainPage.DisplayAlert("Debug Test Results", result, "OK");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Debug Test Error", ex.Message);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}