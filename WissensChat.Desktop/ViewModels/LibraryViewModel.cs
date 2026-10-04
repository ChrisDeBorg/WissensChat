using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WissensChat.Core;
using WissensChat.Desktop.Services;

namespace WissensChat.Desktop.ViewModels;

public partial class LibraryViewModel(ChunkStore store, IngestionService ingestion, FileDialogService dialogs) : ObservableObject
{
    public ObservableCollection<DocumentInfo> Documents { get; } = [];

    [ObservableProperty] private string _status = "Bereit.";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ImportCommand))] private bool _isBusy;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(DeleteCommand))] private DocumentInfo? _selectedDocument;

    public void Refresh()
    {
        Documents.Clear();
        foreach (var d in store.List()) Documents.Add(d);
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        var files = await dialogs.PickDocumentsAsync();
        if (files.Count == 0) return;

        IsBusy = true;
        try
        {
            var progress = new Progress<string>(s => Status = s);
            foreach (var f in files)
            {
                try { Status = await ingestion.IngestAsync(f, progress); }
                catch (Exception ex) { Status = $"{Path.GetFileName(f)}: Fehler – {ex.Message}"; }
            }
            Refresh();
        }
        finally { IsBusy = false; }
    }
    private bool CanImport() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (SelectedDocument is null) return;
        store.Delete(SelectedDocument.Id);
        Refresh();
    }
    private bool CanDelete() => SelectedDocument is not null;
}
