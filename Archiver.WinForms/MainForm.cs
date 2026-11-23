using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using Archiver.Core;

namespace Archiver.WinForms
{
    public class MainForm : Form
    {
        private readonly IArchiveService _archiveService;
        private AppState _state;
        private readonly string _defaultStatePath;

        // Элементы интерфейса
        private MenuStrip _menuStrip = null!;
        private ToolStripMenuItem _modeOptimalItem = null!;
        private ToolStripMenuItem _modeFastestItem = null!;
        private ToolStripMenuItem _modeNoneItem = null!;
        private ToolStripMenuItem _darkThemeItem = null!;

        private Label _lblArchiveInfo = null!;
        private DataGridView _dgvEntries = null!;
        private Button _btnOpenArchive = null!;
        private Button _btnExtractArchive = null!;

        private ListBox _lstFilesToPack = null!;
        private Button _btnAddFiles = null!;
        private Button _btnAddFolder = null!;
        private Button _btnClearPackList = null!;
        private Button _btnCreateArchive = null!;

        private ProgressBar _progressBar = null!;
        private Label _lblStatus = null!;

        public MainForm(string? initialArchivePath = null)
        {
            _archiveService = new ZipArchiveService();
            _archiveService.ProgressChanged += ArchiveService_ProgressChanged;

            _defaultStatePath = Path.Combine(AppContext.BaseDirectory, "state.json");
            _state = StateStorage.Load(_defaultStatePath);

            InitializeUi();
            ApplyStateToUi();

            if (!string.IsNullOrWhiteSpace(initialArchivePath) && File.Exists(initialArchivePath))
            {
                OpenArchive(initialArchivePath);
            }
            else if (!string.IsNullOrWhiteSpace(_state.LastOpenedArchive) && File.Exists(_state.LastOpenedArchive))
            {
                OpenArchive(_state.LastOpenedArchive);
            }
        }

        private void InitializeUi()
        {
            Text = "ZIP Архиватор — Просмотр, упаковка и распаковка";
            Size = new Size(1050, 620);
            MinimumSize = new Size(850, 500);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            // 1. Главное меню
            _menuStrip = new MenuStrip();

            var fileMenu = new ToolStripMenuItem("Файл");
            fileMenu.DropDownItems.Add("Открыть архив... (Ctrl+O)", null, (_, _) => SelectAndOpenArchive());
            fileMenu.DropDownItems.Add("Создать архив... (Ctrl+N)", null, (_, _) => CreateNewArchive());
            fileMenu.DropDownItems.Add("Распаковать текущий... (Ctrl+E)", null, (_, _) => ExtractCurrentArchive());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Сохранить состояние в JSON... (Ctrl+S)", null, (_, _) => SaveStateAs());
            fileMenu.DropDownItems.Add("Загрузить состояние из JSON...", null, (_, _) => LoadStateFrom());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Выход", null, (_, _) => Close());

            var modeMenu = new ToolStripMenuItem("Режим сжатия");
            _modeOptimalItem = new ToolStripMenuItem("Оптимальное (Optimal)", null, (_, _) => SetCompressionMode(CompressionLevel.Optimal));
            _modeFastestItem = new ToolStripMenuItem("Быстрое (Fastest)", null, (_, _) => SetCompressionMode(CompressionLevel.Fastest));
            _modeNoneItem = new ToolStripMenuItem("Без сжатия (NoCompression)", null, (_, _) => SetCompressionMode(CompressionLevel.NoCompression));
            modeMenu.DropDownItems.AddRange(new ToolStripItem[] { _modeOptimalItem, _modeFastestItem, _modeNoneItem });

            var viewMenu = new ToolStripMenuItem("Вид");
            _darkThemeItem = new ToolStripMenuItem("Тёмная тема") { CheckOnClick = true };
            _darkThemeItem.CheckedChanged += (_, _) =>
            {
                _state.IsDarkTheme = _darkThemeItem.Checked;
                ApplyTheme(_state.IsDarkTheme);
            };
            viewMenu.DropDownItems.Add(_darkThemeItem);

            var helpMenu = new ToolStripMenuItem("Справка");
            helpMenu.DropDownItems.Add("Управление и командная строка (F1)", null, (_, _) => ShowHelpDialog());

            _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, modeMenu, viewMenu, helpMenu });
            MainMenuStrip = _menuStrip;
            Controls.Add(_menuStrip);

            // 2. Разделение экрана на 2 части: слева просмотр архива, справа создание архива
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 640,
                Padding = new Padding(10)
            };

            // Левая панель (Просмотр и распаковка)
            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

            _lblArchiveInfo = new Label
            {
                Text = "Архив не открыт. Нажмите «Открыть архив» или перетащите .zip файл.",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            _dgvEntries = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Имя файла", DataPropertyName = nameof(ArchiveEntryInfo.Name), Width = 170 });
            _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Путь в архиве", DataPropertyName = nameof(ArchiveEntryInfo.FullPath), Width = 190 });
            _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Исходный", DataPropertyName = nameof(ArchiveEntryInfo.OriginalSizeText), Width = 85 });
            _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Сжатый", DataPropertyName = nameof(ArchiveEntryInfo.CompressedSizeText), Width = 85 });
            _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Сжатие", DataPropertyName = nameof(ArchiveEntryInfo.CompressionRatio), Width = 65 });

            var leftBottomButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(0, 6, 0, 0) };
            _btnOpenArchive = new Button { Text = "Открыть архив...", Width = 140, Height = 30 };
            _btnOpenArchive.Click += (_, _) => SelectAndOpenArchive();

            _btnExtractArchive = new Button { Text = "Распаковать всё...", Width = 150, Height = 30 };
            _btnExtractArchive.Click += (_, _) => ExtractCurrentArchive();

            leftBottomButtons.Controls.AddRange(new Control[] { _btnOpenArchive, _btnExtractArchive });

            leftPanel.Controls.Add(_dgvEntries);
            leftPanel.Controls.Add(leftBottomButtons);
            leftPanel.Controls.Add(_lblArchiveInfo);
            split.Panel1.Controls.Add(leftPanel);

            // Правая панель (Упаковка в новый архив)
            var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

            var lblPackHeader = new Label
            {
                Text = "Файлы и папки для нового архива:",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            _lstFilesToPack = new ListBox
            {
                Dock = DockStyle.Fill,
                HorizontalScrollbar = true
            };

            var rightButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(0, 6, 0, 0) };
            _btnAddFiles = new Button { Text = "+ Файлы", Width = 110, Height = 30 };
            _btnAddFiles.Click += (_, _) => AddFilesToPack();

            _btnAddFolder = new Button { Text = "+ Папку", Width = 110, Height = 30 };
            _btnAddFolder.Click += (_, _) => AddFolderToPack();

            _btnClearPackList = new Button { Text = "Очистить", Width = 110, Height = 30 };
            _btnClearPackList.Click += (_, _) =>
            {
                _state.FilesToPack.Clear();
                RefreshPackList();
            };

            _btnCreateArchive = new Button
            {
                Text = "Создать ZIP архив...",
                Width = 226,
                Height = 32,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _btnCreateArchive.Click += (_, _) => CreateNewArchive();

            rightButtons.Controls.AddRange(new Control[] { _btnAddFiles, _btnAddFolder, _btnClearPackList, _btnCreateArchive });

            rightPanel.Controls.Add(_lstFilesToPack);
            rightPanel.Controls.Add(rightButtons);
            rightPanel.Controls.Add(lblPackHeader);
            split.Panel2.Controls.Add(rightPanel);

            // 3. Нижняя панель статуса и прогресса
            var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(10, 4, 10, 4) };
            _progressBar = new ProgressBar { Dock = DockStyle.Right, Width = 250 };
            _lblStatus = new Label { Text = "Готово к работе. Нажмите F1 для вызова справки.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            statusPanel.Controls.Add(_lblStatus);
            statusPanel.Controls.Add(_progressBar);

            // Порядок добавления важен для корректного Dock
            Controls.Add(split);
            Controls.Add(statusPanel);
            Controls.Add(_menuStrip);

            // Поддержка Drag & Drop файлов прямо в окно
            AllowDrop = true;
            DragEnter += (_, e) =>
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                    e.Effect = DragDropEffects.Copy;
            };
            DragDrop += MainForm_DragDrop;

            // Горячие клавиши и автосохранение при закрытии
            KeyDown += MainForm_KeyDown;
            FormClosing += (_, _) => StateStorage.Save(_defaultStatePath, _state);
        }

        private void ApplyStateToUi()
        {
            SetCompressionMode(_state.CompressionMode);
            _darkThemeItem.Checked = _state.IsDarkTheme;
            ApplyTheme(_state.IsDarkTheme);
            RefreshPackList();
        }

        private void SetCompressionMode(CompressionLevel level)
        {
            _state.CompressionMode = level;
            _modeOptimalItem.Checked = level == CompressionLevel.Optimal;
            _modeFastestItem.Checked = level == CompressionLevel.Fastest;
            _modeNoneItem.Checked = level == CompressionLevel.NoCompression;
            _lblStatus.Text = $"Режим сжатия: {level}";
        }

        private void ApplyTheme(bool isDark)
        {
            Color backColor = isDark ? Color.FromArgb(32, 32, 36) : SystemColors.Control;
            Color panelColor = isDark ? Color.FromArgb(45, 45, 50) : Color.White;
            Color textColor = isDark ? Color.WhiteSmoke : Color.Black;

            BackColor = backColor;
            ForeColor = textColor;

            _dgvEntries.BackgroundColor = panelColor;
            _dgvEntries.DefaultCellStyle.BackColor = panelColor;
            _dgvEntries.DefaultCellStyle.ForeColor = textColor;
            _dgvEntries.ColumnHeadersDefaultCellStyle.BackColor = backColor;
            _dgvEntries.ColumnHeadersDefaultCellStyle.ForeColor = textColor;
            _dgvEntries.EnableHeadersVisualStyles = false;

            _lstFilesToPack.BackColor = panelColor;
            _lstFilesToPack.ForeColor = textColor;

            foreach (var btn in new[] { _btnOpenArchive, _btnExtractArchive, _btnAddFiles, _btnAddFolder, _btnClearPackList, _btnCreateArchive })
            {
                btn.BackColor = isDark ? Color.FromArgb(60, 60, 68) : SystemColors.ControlLight;
                btn.ForeColor = textColor;
                btn.FlatStyle = FlatStyle.Flat;
            }
        }

        private void SelectAndOpenArchive()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Выберите ZIP-архив",
                Filter = "ZIP архивы (*.zip)|*.zip|Все файлы (*.*)|*.*"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                OpenArchive(dialog.FileName);
            }
        }

        private void OpenArchive(string path)
        {
            try
            {
                var entries = _archiveService.GetEntries(path);
                _dgvEntries.DataSource = entries;
                _state.LastOpenedArchive = path;

                long totalOriginal = entries.Sum(x => x.OriginalSize);
                long totalCompressed = entries.Sum(x => x.CompressedSize);
                _lblArchiveInfo.Text = $"Архив: {Path.GetFileName(path)} | Файлов: {entries.Count} | Размер: {totalCompressed / 1024.0:F1} КБ (было {totalOriginal / 1024.0:F1} КБ)";
                _lblStatus.Text = $"Открыт архив: {path}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть архив:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExtractCurrentArchive()
        {
            if (string.IsNullOrWhiteSpace(_state.LastOpenedArchive) || !File.Exists(_state.LastOpenedArchive))
            {
                MessageBox.Show("Сначала откройте ZIP-архив для распаковки.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new FolderBrowserDialog
            {
                Description = "Выберите папку для распаковки файлов",
                SelectedPath = _state.LastExtractFolder
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _progressBar.Value = 0;
                    _archiveService.ExtractArchive(_state.LastOpenedArchive, dialog.SelectedPath);
                    _state.LastExtractFolder = dialog.SelectedPath;
                    _lblStatus.Text = $"Архив успешно распакован в: {dialog.SelectedPath}";
                    MessageBox.Show("Распаковка успешно завершена!", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при распаковке:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void AddFilesToPack()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Выберите файлы для добавления в архив",
                Multiselect = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!_state.FilesToPack.Contains(file))
                        _state.FilesToPack.Add(file);
                }
                RefreshPackList();
            }
        }

        private void AddFolderToPack()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Выберите папку для добавления в архив"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (!_state.FilesToPack.Contains(dialog.SelectedPath))
                    _state.FilesToPack.Add(dialog.SelectedPath);
                RefreshPackList();
            }
        }

        private void RefreshPackList()
        {
            _lstFilesToPack.Items.Clear();
            foreach (var item in _state.FilesToPack)
            {
                _lstFilesToPack.Items.Add(item);
            }
        }

        private void CreateNewArchive()
        {
            if (_state.FilesToPack.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы один файл или папку в список справа.", "Список пуст", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Title = "Сохранить новый ZIP-архив",
                Filter = "ZIP архив (*.zip)|*.zip",
                FileName = "archive.zip"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _progressBar.Value = 0;
                    _archiveService.CreateArchive(_state.FilesToPack, dialog.FileName, _state.CompressionMode);
                    _lblStatus.Text = $"Архив создан: {dialog.FileName}";
                    OpenArchive(dialog.FileName);
                    MessageBox.Show("Архив успешно создан!", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при создании архива:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveStateAs()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Сохранить состояние программы",
                Filter = "JSON файл (*.json)|*.json",
                FileName = "archiver_state.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                StateStorage.Save(dialog.FileName, _state);
                _lblStatus.Text = $"Состояние сохранено в {dialog.FileName}";
            }
        }

        private void LoadStateFrom()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Загрузить состояние программы",
                Filter = "JSON файл (*.json)|*.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _state = StateStorage.Load(dialog.FileName);
                ApplyStateToUi();
                if (!string.IsNullOrWhiteSpace(_state.LastOpenedArchive) && File.Exists(_state.LastOpenedArchive))
                    OpenArchive(_state.LastOpenedArchive);
                _lblStatus.Text = $"Состояние загружено из {dialog.FileName}";
            }
        }

        private void ArchiveService_ProgressChanged(object? sender, ArchiveProgressEventArgs e)
        {
            _progressBar.Value = Math.Clamp(e.Percent, 0, 100);
            _lblStatus.Text = $"Обработка ({e.ProcessedCount}/{e.TotalCount}): {e.CurrentFile}";
            Application.DoEvents();
        }

        private void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is not string[] droppedPaths || droppedPaths.Length == 0)
                return;

            // Если перетащили один .zip файл — сразу открываем его на просмотр
            if (droppedPaths.Length == 1 && File.Exists(droppedPaths[0]) && droppedPaths[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                OpenArchive(droppedPaths[0]);
                return;
            }

            // Иначе добавляем перетащенные файлы/папки в список на упаковку
            foreach (var path in droppedPaths)
            {
                if (!_state.FilesToPack.Contains(path))
                    _state.FilesToPack.Add(path);
            }
            RefreshPackList();
        }

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
            {
                ShowHelpDialog();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.O)
            {
                SelectAndOpenArchive();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.N)
            {
                CreateNewArchive();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.E)
            {
                ExtractCurrentArchive();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                SaveStateAs();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete && _lstFilesToPack.Focused && _lstFilesToPack.SelectedIndex >= 0)
            {
                _state.FilesToPack.RemoveAt(_lstFilesToPack.SelectedIndex);
                RefreshPackList();
                e.Handled = true;
            }
        }

        private static void ShowHelpDialog()
        {
            string helpText =
                "ГОРЯЧИЕ КЛАВИШИ:\n" +
                "  F1 — Справка по управлению и командной строке\n" +
                "  Ctrl + O — Открыть ZIP-архив для просмотра\n" +
                "  Ctrl + N — Создать новый ZIP-архив из списка\n" +
                "  Ctrl + E — Распаковать открытый архив\n" +
                "  Ctrl + S — Сохранить текущее состояние в JSON-файл\n" +
                "  Del — Удалить выбранный файл из списка упаковки\n" +
                "  Drag & Drop — Перетащите .zip в окно для просмотра или файлы для упаковки\n\n" +
                "ПАРАМЕТРЫ КОМАНДНОЙ СТРОКИ:\n" +
                "  Archiver.WinForms.exe <файл.zip>\n" +
                "      Открывает указанный архив сразу в окне программы.\n\n" +
                "  Archiver.WinForms.exe pack <источник> <архив.zip> [optimal|fastest|none]\n" +
                "      Упаковывает файл или папку в ZIP-архив.\n\n" +
                "  Archiver.WinForms.exe unpack <архив.zip> <папка_назначения>\n" +
                "      Распаковывает архив в указанную папку.\n\n" +
                "  Archiver.WinForms.exe list <архив.zip>\n" +
                "      Выводит список файлов внутри архива в консоль.";

            MessageBox.Show(helpText, "Справка — ZIP Архиватор", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
