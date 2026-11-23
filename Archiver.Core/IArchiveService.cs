using System;
using System.Collections.Generic;
using System.IO.Compression;

namespace Archiver.Core
{
    /// <summary>
    /// Общий интерфейс для работы с архивами.
    /// </summary>
    public interface IArchiveService
    {
        // Событие для обновления ProgressBar в окне или вывода прогресса в консоль
        event EventHandler<ArchiveProgressEventArgs>? ProgressChanged;

        // Получить список файлов внутри архива
        List<ArchiveEntryInfo> GetEntries(string archivePath);

        // Упаковать выбранные файлы и папки в архив
        void CreateArchive(IEnumerable<string> sourcePaths, string archivePath, CompressionLevel level);

        // Распаковать архив в указанную папку
        void ExtractArchive(string archivePath, string destinationFolder);
    }
}
