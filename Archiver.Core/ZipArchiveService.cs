using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Archiver.Core
{
    /// <summary>
    /// Реализация архиватора для формата ZIP на базе стандартного System.IO.Compression.
    /// </summary>
    public class ZipArchiveService : ArchiveServiceBase
    {
        protected override List<ArchiveEntryInfo> GetEntriesCore(string archivePath)
        {
            var result = new List<ArchiveEntryInfo>();

            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var entry in archive.Entries)
            {
                // Пропускаем пустые записи, которые обозначают только папки
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                result.Add(new ArchiveEntryInfo
                {
                    Name = entry.Name,
                    FullPath = entry.FullName,
                    OriginalSize = entry.Length,
                    CompressedSize = entry.CompressedLength,
                    LastModified = entry.LastWriteTime.DateTime
                });
            }

            return result;
        }

        protected override void CreateArchiveCore(List<string> sourcePaths, string archivePath, CompressionLevel level)
        {
            // Собираем список всех файлов (если передана папка — берём все файлы внутри неё)
            var filesToPack = new List<(string FullPath, string EntryName)>();

            foreach (var path in sourcePaths)
            {
                if (File.Exists(path))
                {
                    filesToPack.Add((path, Path.GetFileName(path)));
                }
                else if (Directory.Exists(path))
                {
                    string rootFolderName = new DirectoryInfo(path).Name;
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = Path.GetRelativePath(path, file);
                        string entryName = Path.Combine(rootFolderName, relativePath).Replace('\\', '/');
                        filesToPack.Add((file, entryName));
                    }
                }
            }

            if (filesToPack.Count == 0)
                throw new InvalidOperationException("В выбранных папках нет файлов для упаковки.");

            // Если архив с таким именем уже есть — перезаписываем
            if (File.Exists(archivePath))
                File.Delete(archivePath);

            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
            for (int i = 0; i < filesToPack.Count; i++)
            {
                var (fullPath, entryName) = filesToPack[i];
                archive.CreateEntryFromFile(fullPath, entryName, level);
                OnProgressChanged(entryName, i + 1, filesToPack.Count);
            }
        }

        protected override void ExtractArchiveCore(string archivePath, string destinationFolder)
        {
            Directory.CreateDirectory(destinationFolder);
            string fullDestRoot = Path.GetFullPath(destinationFolder);

            using var archive = ZipFile.OpenRead(archivePath);
            int total = archive.Entries.Count;

            for (int i = 0; i < total; i++)
            {
                var entry = archive.Entries[i];
                string targetPath = Path.GetFullPath(Path.Combine(fullDestRoot, entry.FullName));

                // Защита от выхода за пределы целевой папки
                if (!targetPath.StartsWith(fullDestRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(targetPath);
                }
                else
                {
                    string? parentDir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(parentDir))
                        Directory.CreateDirectory(parentDir);

                    entry.ExtractToFile(targetPath, overwrite: true);
                }

                OnProgressChanged(entry.FullName, i + 1, total);
            }
        }
    }
}
