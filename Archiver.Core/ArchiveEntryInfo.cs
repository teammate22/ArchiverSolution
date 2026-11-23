using System;

namespace Archiver.Core
{
    /// <summary>
    /// Информация об одном файле внутри архива.
    /// </summary>
    public class ArchiveEntryInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long OriginalSize { get; set; }
        public long CompressedSize { get; set; }
        public DateTime LastModified { get; set; }

        // Удобные свойства для красивого вывода в таблицу
        public string OriginalSizeText => FormatBytes(OriginalSize);
        public string CompressedSizeText => FormatBytes(CompressedSize);

        public string CompressionRatio
        {
            get
            {
                if (OriginalSize == 0) return "0%";
                double saved = (1.0 - (double)CompressedSize / OriginalSize) * 100;
                return $"{Math.Max(0, Math.Round(saved))}%";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} Б";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} КБ";
            return $"{bytes / (1024.0 * 1024.0):F2} МБ";
        }
    }
}
