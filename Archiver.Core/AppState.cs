using System.Collections.Generic;
using System.IO.Compression;

namespace Archiver.Core
{
    /// <summary>
    /// Настройки и рабочее состояние программы для сохранения в JSON.
    /// </summary>
    public class AppState
    {
        public string LastOpenedArchive { get; set; } = string.Empty;
        public string LastExtractFolder { get; set; } = string.Empty;
        public CompressionLevel CompressionMode { get; set; } = CompressionLevel.Optimal;
        public bool IsDarkTheme { get; set; } = false;

        // Список файлов, добавленных пользователем в очередь на упаковку
        public List<string> FilesToPack { get; set; } = new List<string>();
    }
}
