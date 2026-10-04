using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace WissensChat.Desktop.Services;

public sealed class FileDialogService
{
    public Window? Owner { get; set; }

    public async Task<IReadOnlyList<string>> PickDocumentsAsync()
    {
        // Avalonia 12: TopLevel immer über GetTopLevel ermitteln
        var top = TopLevel.GetTopLevel(Owner);
        if (top is null) return [];

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Dokumente importieren",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("PDF und Word") { Patterns = ["*.pdf", "*.docx"] }]
        });

        return files.Select(f => f.TryGetLocalPath()).Where(p => p is not null).Select(p => p!).ToList();
    }
}
