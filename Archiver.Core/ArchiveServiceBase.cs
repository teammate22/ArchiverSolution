using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Archiver.Core
{
    /// <summary>
    /// Базовый класс архиватора. Проверяет корректность путей и управляет событием прогресса.
    /// </summary>
    public abstract class ArchiveServiceBase : IArchiveService
    {
        public event EventHandler<ArchiveProgressEventArgs>? ProgressChanged;

        public List<ArchiveEntryInfo> GetEntries(string archivePath)
        {
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Файл архива не найден.", archivePath);

            return GetEntriesCore(archivePath);
        }

        public void CreateArchive(IEnumerable<string> sourcePaths, string archivePath, CompressionLevel level)
        {
            var pathsList = sourcePaths?.ToList() ?? new List<string>();
            if (pathsList.Count == 0)
                throw new ArgumentException("Не выбрано ни одного файла или папки для архивации.");

            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("Не указан путь для сохранения архива.");

            CreateArchiveCore(pathsList, archivePath, level);
        }

        public void ExtractArchive(string archivePath, string destinationFolder)
        {
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Файл архива не найден.", archivePath);

            if (string.IsNullOrWhiteSpace(destinationFolder))
                throw new ArgumentException("Не указана папка для распаковки.");

            ExtractArchiveCore(archivePath, destinationFolder);
        }

        // Метод для наследников: оповестить подписчиков о прогрессе
        protected void OnProgressChanged(string currentFile, int processed, int total)
        {
            ProgressChanged?.Invoke(this, new ArchiveProgressEventArgs(currentFile, processed, total));
        }

        protected abstract List<ArchiveEntryInfo> GetEntriesCore(string archivePath);
        protected abstract void CreateArchiveCore(List<string> sourcePaths, string archivePath, CompressionLevel level);
        protected abstract void ExtractArchiveCore(string archivePath, string destinationFolder);
    }
}
