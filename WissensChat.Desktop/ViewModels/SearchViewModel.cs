using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WissensChat.Core;

namespace WissensChat.Desktop.ViewModels;

public partial class SearchViewModel(RagService rag) : ObservableObject
{
    public ObservableCollection<SearchHit> Results { get; } = [];

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SearchCommand))] private string _query = "";
    [ObservableProperty] private string _status = "";

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        Results.Clear();
        Status = "Suche läuft …";
        try
        {
            var hits = await rag.SearchAsync(Query.Trim(), 8);
            foreach (var h in hits) Results.Add(h);
            Status = hits.Count == 0 ? "Keine Treffer (Bibliothek leer?)" : $"{hits.Count} Treffer";
        }
        catch (Exception ex) { Status = $"Fehler: {ex.Message} (läuft Ollama?)"; }
    }
    private bool CanSearch() => !string.IsNullOrWhiteSpace(Query);
}
