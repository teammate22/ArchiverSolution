using System;

namespace Archiver.Core
{
    /// <summary>
    /// Данные события, которые отправляются при упаковке или распаковке каждого файла.
    /// </summary>
    public class ArchiveProgressEventArgs : EventArgs
    {
        public string CurrentFile { get; }
        public int ProcessedCount { get; }
        public int TotalCount { get; }
        public int Percent => TotalCount == 0 ? 100 : (int)((double)ProcessedCount / TotalCount * 100);

        public ArchiveProgressEventArgs(string currentFile, int processedCount, int totalCount)
        {
            CurrentFile = currentFile;
            ProcessedCount = processedCount;
            TotalCount = totalCount;
        }
    }
}
