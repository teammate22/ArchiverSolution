using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Archiver.Core;

namespace Archiver.WinForms
{
    internal static class Program
    {
        // Позволяет выводить текст в терминал (CMD / PowerShell / Jupyter), если программа запущена из консоли
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        static void Main(string[] args)
        {
            // Если переданы аргументы командной строки (и это не просто двойной клик по .zip файлу)
            if (args.Length > 0 && !(args.Length == 1 && File.Exists(args[0]) && args[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                if (OperatingSystem.IsWindows())
                {
                    AttachConsole(ATTACH_PARENT_PROCESS);
                }

                RunCli(args);
                return;
            }

            // Обычный запуск графического интерфейса
            ApplicationConfiguration.Initialize();
            string? initialArchive = args.Length == 1 ? args[0] : null;
            Application.Run(new MainForm(initialArchive));
        }

        private static void RunCli(string[] args)
        {
            IArchiveService service = new ZipArchiveService();
            service.ProgressChanged += (_, e) =>
            {
                Console.WriteLine($"[{e.Percent,3}%] {e.CurrentFile}");
            };

            string command = args[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "pack":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Ошибка: укажите источник и путь к архиву. Пример: pack C:\\Docs archive.zip [optimal|fastest|none]");
                            return;
                        }
                        CompressionLevel level = args.Length >= 4 ? ParseCompression(args[3]) : CompressionLevel.Optimal;
                        service.CreateArchive(new[] { args[1] }, args[2], level);
                        Console.WriteLine($"Готово! Архив создан: {Path.GetFullPath(args[2])}");
                        break;

                    case "unpack":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Ошибка: укажите архив и папку назначения. Пример: unpack archive.zip C:\\Extracted");
                            return;
                        }
                        service.ExtractArchive(args[1], args[2]);
                        Console.WriteLine($"Готово! Файлы распакованы в: {Path.GetFullPath(args[2])}");
                        break;

                    case "list":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Ошибка: укажите путь к архиву. Пример: list archive.zip");
                            return;
                        }
                        var entries = service.GetEntries(args[1]);
                        Console.WriteLine($"Содержимое архива {Path.GetFileName(args[1])} (файлов: {entries.Count}):");
                        foreach (var item in entries)
                        {
                            Console.WriteLine($" - {item.FullPath,-35} | Было: {item.OriginalSizeText,10} | Стало: {item.CompressedSizeText,10} | Сжатие: {item.CompressionRatio}");
                        }
                        break;

                    case "help":
                    case "--help":
                    case "-h":
                    case "/?":
                        PrintCliHelp();
                        break;

                    default:
                        Console.WriteLine($"Неизвестная команда: {command}");
                        PrintCliHelp();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка выполнения: {ex.Message}");
            }
        }

        private static CompressionLevel ParseCompression(string value) => value.ToLowerInvariant() switch
        {
            "fastest" => CompressionLevel.Fastest,
            "none" or "nocompression" => CompressionLevel.NoCompression,
            _ => CompressionLevel.Optimal
        };

        private static void PrintCliHelp()
        {
            Console.WriteLine("=== Справка по параметрам командной строки ===");
            Console.WriteLine("  Archiver.WinForms.exe                                     - Запуск графического интерфейса");
            Console.WriteLine("  Archiver.WinForms.exe <файл.zip>                          - Открыть архив в графическом окне");
            Console.WriteLine("  Archiver.WinForms.exe pack <путь> <архив.zip> [режим]     - Упаковать файл/папку (режимы: optimal, fastest, none)");
            Console.WriteLine("  Archiver.WinForms.exe unpack <архив.zip> <папка>          - Распаковать архив");
            Console.WriteLine("  Archiver.WinForms.exe list <архив.zip>                    - Вывести список файлов внутри архива");
        }
    }
}
