using Microsoft.Win32;
using System.IO;

namespace Blood_Alcohol.Services
{
    public interface IDialogService
    {
        string? SelectFolder(string title, string? initialDirectory, string? currentFolder);
    }

    public sealed class FolderDialogService : IDialogService
    {
        public string? SelectFolder(string title, string? initialDirectory, string? currentFolder)
        {
            OpenFolderDialog dialog = new OpenFolderDialog
            {
                Title = title,
                InitialDirectory = Directory.Exists(initialDirectory ?? string.Empty) ? initialDirectory : null,
                FolderName = currentFolder,
                Multiselect = false
            };

            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }
    }
}
