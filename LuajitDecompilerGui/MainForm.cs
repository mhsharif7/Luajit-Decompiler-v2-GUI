using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using LuajitDecompilerGui.Models;
using LuajitDecompilerGui.Services;
using System.Runtime.InteropServices;

namespace LuajitDecompilerGui;

public sealed class MainForm : Form
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(
    IntPtr hWnd,
    string? pszSubAppName,
    string? pszSubIdList);

    private enum PreferredAppMode
    {
        Default = 0,
        AllowDark = 1,
        ForceDark = 2,
        ForceLight = 3
    }

    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern PreferredAppMode SetPreferredAppMode(PreferredAppMode appMode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();

    private bool _darkMode;

    private Color AppBackground => _darkMode ? Color.FromArgb(17, 24, 39) : Color.FromArgb(245, 247, 250);
    private Color CardBackground => _darkMode ? Color.FromArgb(31, 41, 55) : Color.White;
    private Color BorderColor => _darkMode ? Color.FromArgb(55, 65, 81) : Color.FromArgb(222, 226, 230);
    private Color TextPrimary => _darkMode ? Color.FromArgb(243, 244, 246) : Color.FromArgb(31, 41, 55);
    private Color TextSecondary => _darkMode ? Color.FromArgb(156, 163, 175) : Color.FromArgb(107, 114, 128);
    private static readonly Color Accent = Color.FromArgb(0, 120, 212);
    private static readonly Color AccentHover = Color.FromArgb(0, 99, 177);
    private static readonly Color Success = Color.FromArgb(16, 124, 16);
    private static readonly Color Warning = Color.FromArgb(157, 93, 0);
    private static readonly Color Danger = Color.FromArgb(196, 43, 28);

    private readonly LuaJitBytecodeDetector _detector = new();
    private readonly FileScanner _scanner;
    private readonly OutputPathService _outputPathService = new();
    private readonly DecompilerService _decompiler;
    private readonly BatchDecompilerService _batchDecompiler;
    private readonly AppSettingsService _settingsService = new();
    private readonly BindingList<InputFile> _files = [];

    private CancellationTokenSource? _batchCancellation;
    private CancellationTokenSource? _scanCancellation;

    private readonly DataGridView dgvFiles = new();
    private readonly TextBox txtOutputFolder = new();
    private readonly CheckBox chkPreserveDirectories = new() { Text = "Preserve folders", Checked = true, AutoSize = true };
    private readonly CheckBox chkForceOverwrite = new() { Text = "Force overwrite (-f)", AutoSize = true };
    private readonly CheckBox chkSilentAssertions = new() { Text = "Silent assertions (-s)", Checked = true, AutoSize = true };
    private readonly CheckBox chkIgnoreDebugInfo = new() { Text = "Ignore debug info (-i)", AutoSize = true };
    private readonly CheckBox chkMinimizeDiffs = new() { Text = "Minimize diffs (-m)", AutoSize = true };
    private readonly CheckBox chkUnrestrictedAscii = new() { Text = "Unrestricted ASCII (-u)", AutoSize = true };
    private readonly TextBox txtExtensionFilter = new() { Width = 72 };
    private readonly TextBox txtPreview = new();
    private readonly TextBox txtLog = new();
    private readonly ThemedProgressBar progressBar = new();

    private readonly Label lblProgress = new() { AutoSize = true, Text = "Ready" };
    private readonly Label lblOperation = new() { AutoSize = true, Text = "Ready" };
    private readonly Label lblDecompiler = new() { AutoSize = true };
    private readonly Label lblFileCount = new() { AutoSize = true, Text = "0 files" };
    private readonly Label lblVersion = new() { AutoSize = true };

    private readonly Button btnAddFiles;
    private readonly Button btnAddFolder;
    private readonly Button btnRemove;
    private readonly Button btnClear;
    private readonly Button btnBrowseOutput;
    private readonly Button btnOpenOutput;
    private readonly Button btnDecompile;
    private readonly Button btnCancel;
    private readonly Button btnTheme;

    private readonly ToolTip toolTip = new();

    public MainForm()
    {
        _scanner = new FileScanner(_detector);
        _decompiler = new DecompilerService(_outputPathService);
        _batchDecompiler = new BatchDecompilerService(_decompiler);

        TrySetPreferredAppMode(false);

        btnAddFiles = CreateButton("Add files");
        btnAddFolder = CreateButton("Add folder");
        btnRemove = CreateButton("Remove");
        btnClear = CreateButton("Clear");
        btnBrowseOutput = CreateButton("Browse");
        btnOpenOutput = CreateButton("Open");
        btnDecompile = CreateButton("Decompile", primary: true);
        btnCancel = CreateButton("Cancel", danger: true);
        btnTheme = CreateButton("☾ Dark");
        btnTheme.Height = 28;
        btnTheme.Padding = new Padding(8, 0, 8, 0);
        btnCancel.Enabled = false;

        Text = "LuaJIT Decompiler v2 GUI";
        Width = 1360;
        Height = 860;
        MinimumSize = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        BackColor = AppBackground;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        ConfigureTextAreas();
        BuildUi();
        ConfigureGrid();
        ConfigureToolTips();

        DragEnter += MainForm_DragEnter;
        DragDrop += MainForm_DragDrop;
        dgvFiles.SelectionChanged += dgvFiles_SelectionChanged;
        dgvFiles.CellFormatting += dgvFiles_CellFormatting;
        dgvFiles.CellDoubleClick += dgvFiles_CellDoubleClick;
        dgvFiles.CellMouseDown += dgvFiles_CellMouseDown;
        btnDecompile.Click += btnDecompile_Click;
        btnCancel.Click += btnCancel_Click;
        _files.ListChanged += (_, _) => UpdateFileCount();

        ConfigureFileContextMenu();
        RestoreSettings();
        FormClosing += MainForm_FormClosing;
        Shown += (_, _) => ApplyNativeControlTheme();

        ApplyTheme();
        ApplyNativeControlTheme();
        UpdateDecompilerStatus();
        UpdateVersionLabel();
        UpdateFileCount();
    }

    private void ConfigureTextAreas()
    {
        foreach (TextBox box in new[] { txtPreview, txtLog })
        {
            box.Multiline = true;
            box.ScrollBars = ScrollBars.Both;
            box.ReadOnly = true;
            box.WordWrap = false;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = CardBackground;
            box.ForeColor = TextPrimary;
            box.Font = new Font("Consolas", 9.5F);
            box.Dock = DockStyle.Fill;
        }
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
            ColumnCount = 1,
            Padding = new Padding(14),
            BackColor = AppBackground
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildToolbar(), 0, 1);
        root.Controls.Add(BuildFilesCard(), 0, 2);
        root.Controls.Add(BuildOptionsCard(), 0, 3);
        root.Controls.Add(BuildRunCard(), 0, 4);
        root.Controls.Add(BuildOutputArea(), 0, 5);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = AppBackground
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        var titlePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty
        };

        var title = new Label
        {
            AutoSize = true,
            Text = "LuaJIT Decompiler v2",
            Font = new Font("Segoe UI Semibold", 16F),
            ForeColor = TextPrimary,
            Margin = Padding.Empty
        };

        var subtitle = new Label
        {
            AutoSize = true,
            Text = "Batch decompile LuaJIT 2.0 / 2.1 bytecode",
            ForeColor = TextSecondary,
            Margin = new Padding(1, 2, 0, 0)
        };

        titlePanel.Controls.Add(title);
        titlePanel.Controls.Add(subtitle);
        header.Controls.Add(titlePanel, 0, 0);

        var statusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };

        lblDecompiler.Font = new Font("Segoe UI Semibold", 9F);
        lblDecompiler.Margin = new Padding(14, 4, 0, 0);
        lblVersion.ForeColor = TextSecondary;
        lblVersion.Margin = new Padding(14, 4, 0, 0);

        var aboutLink = new LinkLabel
        {
            Text = "About",
            AutoSize = true,
            LinkColor = Accent,
            ActiveLinkColor = AccentHover,
            VisitedLinkColor = Accent,
            Margin = new Padding(14, 4, 0, 0)
        };
        aboutLink.LinkClicked += (_, _) => new AboutForm().ShowDialog(this);

        btnTheme.Margin = new Padding(14, 0, 0, 0);
        btnTheme.Click += (_, _) =>
        {
            _darkMode = !_darkMode;
            ApplyTheme();
        };

        statusPanel.Controls.Add(aboutLink);
        statusPanel.Controls.Add(btnTheme);
        statusPanel.Controls.Add(lblDecompiler);
        statusPanel.Controls.Add(lblVersion);
        header.Controls.Add(statusPanel, 1, 0);

        return header;
    }

    private void ApplyTheme()
    {
        SuspendLayout();

        BackColor = AppBackground;
        ForeColor = TextPrimary;
        ApplyThemeRecursive(this, false);

        foreach (TextBox box in new[] { txtOutputFolder, txtExtensionFilter, txtPreview, txtLog })
        {
            box.BackColor = _darkMode ? Color.FromArgb(17, 24, 39) : Color.White;
            box.ForeColor = TextPrimary;
        }

        dgvFiles.BackgroundColor = CardBackground;
        dgvFiles.GridColor = _darkMode ? Color.FromArgb(55, 65, 81) : Color.FromArgb(235, 237, 240);
        dgvFiles.ColumnHeadersDefaultCellStyle.BackColor = _darkMode ? Color.FromArgb(31, 41, 55) : Color.FromArgb(248, 249, 251);
        dgvFiles.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
        dgvFiles.ColumnHeadersDefaultCellStyle.SelectionBackColor = dgvFiles.ColumnHeadersDefaultCellStyle.BackColor;
        dgvFiles.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextSecondary;
        dgvFiles.DefaultCellStyle.BackColor = _darkMode ? Color.FromArgb(17, 24, 39) : Color.White;
        dgvFiles.DefaultCellStyle.ForeColor = TextPrimary;
        dgvFiles.DefaultCellStyle.SelectionBackColor = _darkMode ? Color.FromArgb(30, 64, 105) : Color.FromArgb(226, 239, 252);
        dgvFiles.DefaultCellStyle.SelectionForeColor = TextPrimary;
        dgvFiles.AlternatingRowsDefaultCellStyle.BackColor = _darkMode ? Color.FromArgb(24, 32, 46) : Color.FromArgb(252, 252, 253);
        dgvFiles.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
        dgvFiles.AlternatingRowsDefaultCellStyle.SelectionBackColor = dgvFiles.DefaultCellStyle.SelectionBackColor;
        dgvFiles.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextPrimary;

        btnTheme.Text = _darkMode ? "☀ Light" : "☾ Dark";
        foreach (Button button in new[] { btnAddFiles, btnAddFolder, btnRemove, btnClear, btnBrowseOutput, btnOpenOutput, btnTheme })
            StyleButton(button);
        StyleButton(btnDecompile, primary: true);
        StyleButton(btnCancel, danger: true);

        if (dgvFiles.ContextMenuStrip is { } menu)
        {
            menu.BackColor = CardBackground;
            menu.ForeColor = TextPrimary;
            menu.RenderMode = ToolStripRenderMode.System;
            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = CardBackground;
                item.ForeColor = TextPrimary;
            }
        }

        lblVersion.ForeColor = TextSecondary;
        lblFileCount.ForeColor = TextSecondary;
        lblProgress.ForeColor = TextSecondary;
        UpdateDecompilerStatus();

        TrySetPreferredAppMode(_darkMode);
        ApplyNativeControlTheme();
        progressBar.DarkMode = _darkMode;

        dgvFiles.Invalidate();
        Invalidate(true);
        ResumeLayout(true);
    }

    private static void TrySetPreferredAppMode(bool darkMode)
    {
        try
        {
            SetPreferredAppMode(darkMode ? PreferredAppMode.AllowDark : PreferredAppMode.ForceLight);
            FlushMenuThemes();
        }
        catch (DllNotFoundException)
        {
            // Older Windows build: fall back to per-control theming.
        }
        catch (EntryPointNotFoundException)
        {
            // Older Windows build: fall back to per-control theming.
        }
    }

    private void ApplyNativeControlTheme()
    {
        string theme = _darkMode ? "DarkMode_Explorer" : "Explorer";

        foreach (Control control in new Control[]
        {
            txtPreview, txtLog, txtOutputFolder, txtExtensionFilter
        })
        {
            if (!control.IsHandleCreated)
                _ = control.Handle;

            SetWindowTheme(control.Handle, theme, null);
            control.Invalidate();
        }

        progressBar.DarkMode = _darkMode;
        progressBar.Invalidate();
    }

    private void ApplyThemeRecursive(Control parent, bool insideCard)
    {
        foreach (Control control in parent.Controls)
        {
            bool isCard = Equals(control.Tag, "Card");
            bool cardContext = insideCard || isCard;

            switch (control)
            {
                case SplitContainer split:
                    split.BackColor = AppBackground;
                    break;
                case Panel:
                    control.BackColor = cardContext ? CardBackground : AppBackground;
                    break;
                case Label label when label != lblDecompiler:
                    label.ForeColor = TextPrimary;
                    break;
                case LinkLabel link:
                    link.LinkColor = Accent;
                    link.ActiveLinkColor = AccentHover;
                    link.VisitedLinkColor = Accent;
                    break;
                case CheckBox checkBox:
                    checkBox.BackColor = CardBackground;
                    checkBox.ForeColor = TextPrimary;
                    break;
            }

            if (control.HasChildren)
                ApplyThemeRecursive(control, cardContext);
        }
    }

    private void StyleButton(Button button, bool primary = false, bool danger = false)
    {
        Color background = primary
            ? Accent
            : danger
                ? (_darkMode ? Color.FromArgb(69, 26, 26) : Color.FromArgb(253, 242, 242))
                : (_darkMode ? Color.FromArgb(31, 41, 55) : Color.White);

        button.BackColor = background;
        button.ForeColor = primary
            ? Color.White
            : danger
                ? (_darkMode ? Color.FromArgb(248, 113, 113) : Danger)
                : TextPrimary;
        button.FlatAppearance.BorderColor = primary
            ? Accent
            : danger
                ? (_darkMode ? Color.FromArgb(127, 29, 29) : Color.FromArgb(239, 180, 174))
                : (_darkMode ? Color.FromArgb(75, 85, 99) : Color.FromArgb(209, 213, 219));
        button.FlatAppearance.MouseOverBackColor = primary
            ? AccentHover
            : danger
                ? (_darkMode ? Color.FromArgb(90, 30, 30) : Color.FromArgb(252, 231, 229))
                : (_darkMode ? Color.FromArgb(55, 65, 81) : Color.FromArgb(247, 248, 250));
        button.FlatAppearance.MouseDownBackColor = primary
            ? Color.FromArgb(0, 90, 158)
            : (_darkMode ? Color.FromArgb(75, 85, 99) : Color.FromArgb(238, 240, 243));
    }

    private Control BuildToolbar()
    {
        Panel card = CreateCard();
        card.Margin = new Padding(0, 0, 0, 10);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 7, 10, 7),
            WrapContents = false,
            BackColor = CardBackground
        };

        toolbar.Controls.AddRange([btnAddFiles, btnAddFolder, btnRemove, btnClear]);

        var spacer = new Panel { Width = 12, Height = 1, Margin = Padding.Empty };
        toolbar.Controls.Add(spacer);

        lblFileCount.ForeColor = TextSecondary;
        lblFileCount.Margin = new Padding(4, 9, 0, 0);
        toolbar.Controls.Add(lblFileCount);

        card.Controls.Add(toolbar);

        btnAddFiles.Click += async (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "All files|*.*",
                Title = "Add LuaJIT bytecode files"
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
                await AddPathsAsync(dlg.FileNames);
        };

        btnAddFolder.Click += async (_, _) =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select a folder to scan recursively",
                UseDescriptionForTitle = true
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
                await AddPathsAsync([dlg.SelectedPath]);
        };

        btnRemove.Click += (_, _) => RemoveSelectedFiles();

        btnClear.Click += (_, _) =>
        {
            _files.Clear();
            txtPreview.Clear();
            SetProgressComplete("Ready", "0 / 0", 0, 1);
        };

        return card;
    }

    private Control BuildFilesCard()
    {
        Panel card = CreateCard();
        card.Margin = new Padding(0, 0, 0, 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = CardBackground,
            Padding = new Padding(10, 7, 10, 10)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Label
        {
            Text = "Input files",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = TextPrimary,
            Margin = new Padding(1, 3, 0, 0)
        };

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(dgvFiles, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildOptionsCard()
    {
        Panel card = CreateCard();
        card.Margin = new Padding(0, 0, 0, 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(12, 9, 12, 8),
            BackColor = CardBackground
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var outputRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Margin = Padding.Empty
        };
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        var outputLabel = new Label
        {
            Text = "Output folder",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 9, 8, 0)
        };

        txtOutputFolder.Dock = DockStyle.Fill;
        txtOutputFolder.Margin = new Padding(0, 4, 8, 6);
        txtOutputFolder.BorderStyle = BorderStyle.FixedSingle;
        txtOutputFolder.BackColor = Color.White;

        btnBrowseOutput.Margin = new Padding(0, 2, 8, 4);
        btnOpenOutput.Margin = new Padding(0, 2, 8, 4);
        chkPreserveDirectories.Margin = new Padding(4, 8, 0, 0);

        outputRow.Controls.Add(outputLabel, 0, 0);
        outputRow.Controls.Add(txtOutputFolder, 1, 0);
        outputRow.Controls.Add(btnBrowseOutput, 2, 0);
        outputRow.Controls.Add(btnOpenOutput, 3, 0);
        outputRow.Controls.Add(chkPreserveDirectories, 4, 0);
        layout.Controls.Add(outputRow, 0, 0);

        var optionsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = false,
            Padding = new Padding(0, 7, 0, 0),
            Margin = Padding.Empty,
            BackColor = CardBackground
        };

        foreach (CheckBox box in new[]
        {
            chkForceOverwrite,
            chkSilentAssertions,
            chkIgnoreDebugInfo,
            chkMinimizeDiffs,
            chkUnrestrictedAscii
        })
        {
            box.ForeColor = TextPrimary;
            box.Margin = new Padding(0, 3, 22, 0);
            optionsFlow.Controls.Add(box);
        }

        var extLabel = new Label
        {
            Text = "Extension filter (-e)",
            AutoSize = true,
            ForeColor = TextSecondary,
            Margin = new Padding(0, 5, 6, 0)
        };

        txtExtensionFilter.Margin = new Padding(0, 1, 0, 0);
        txtExtensionFilter.BorderStyle = BorderStyle.FixedSingle;

        optionsFlow.Controls.Add(extLabel);
        optionsFlow.Controls.Add(txtExtensionFilter);
        layout.Controls.Add(optionsFlow, 0, 1);

        card.Controls.Add(layout);

        btnBrowseOutput.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select the folder for decompiled Lua files",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(txtOutputFolder.Text) ? txtOutputFolder.Text : string.Empty
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
                txtOutputFolder.Text = dlg.SelectedPath;
        };

        btnOpenOutput.Click += (_, _) => OpenOutputFolder();

        return card;
    }

    private Control BuildRunCard()
    {
        Panel card = CreateCard();
        card.Margin = new Padding(0, 0, 0, 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10, 10, 10, 10),
            BackColor = CardBackground,
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 210));

        layout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100));

        // Buttons
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        btnDecompile.Width = 105;
        btnCancel.Width = 82;

        buttons.Controls.Add(btnDecompile);
        buttons.Controls.Add(btnCancel);

        layout.Controls.Add(buttons, 0, 0);

        // Progress/status area
        var progressLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(8, 0, 0, 0),
            Padding = Padding.Empty
        };

        progressLayout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 70));

        progressLayout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 30));

        progressLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 25));

        progressLayout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100));

        // Left status, e.g. Ready / Scanning / Decompiling
        lblOperation.Font =
            new Font("Segoe UI Semibold", 9F);

        lblOperation.ForeColor = TextPrimary;

        lblOperation.AutoSize = false;
        lblOperation.Dock = DockStyle.Fill;
        lblOperation.TextAlign =
            ContentAlignment.MiddleLeft;

        lblOperation.Margin = Padding.Empty;

        // Right progress detail, e.g. 320 / 548
        lblProgress.AutoSize = false;
        lblProgress.Dock = DockStyle.Fill;
        lblProgress.TextAlign =
            ContentAlignment.MiddleRight;

        lblProgress.ForeColor = TextSecondary;
        lblProgress.Margin = Padding.Empty;

        // Progress bar
        progressBar.Dock = DockStyle.Fill;
        progressBar.Style =
            ProgressBarStyle.Continuous;

        progressBar.Minimum = 0;
        progressBar.Maximum = 1;
        progressBar.Value = 0;

        progressBar.Margin =
            new Padding(0, 3, 0, 0);

        progressLayout.Controls.Add(
            lblOperation, 0, 0);

        progressLayout.Controls.Add(
            lblProgress, 1, 0);

        progressLayout.Controls.Add(
            progressBar, 0, 1);

        // Progress bar spans both columns.
        progressLayout.SetColumnSpan(
            progressBar, 2);

        layout.Controls.Add(
            progressLayout, 1, 0);

        card.Controls.Add(layout);

        return card;
    }

    private Control BuildOutputArea()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 760,
            SplitterWidth = 8,
            BackColor = AppBackground,
            Margin = Padding.Empty
        };

        split.Panel1.Padding = new Padding(0, 0, 4, 0);
        split.Panel2.Padding = new Padding(4, 0, 0, 0);
        split.Panel1.Controls.Add(BuildTextCard("Lua preview", txtPreview));
        split.Panel2.Controls.Add(BuildTextCard("Log & errors", txtLog));
        return split;
    }

    private Control BuildTextCard(string title, Control content)
    {
        Panel card = CreateCard();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(10, 7, 10, 10),
            BackColor = CardBackground
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = TextPrimary,
            Margin = new Padding(1, 3, 0, 0)
        }, 0, 0);

        layout.Controls.Add(content, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Panel CreateCard()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CardBackground,
            Padding = new Padding(1),
            Tag = "Card"
        };

        panel.Paint += (_, e) =>
        {
            ControlPaint.DrawBorder(
                e.Graphics,
                panel.ClientRectangle,
                BorderColor,
                ButtonBorderStyle.Solid);
        };

        return panel;
    }

    private Button CreateButton(string text, bool primary = false, bool danger = false)
    {
        Color background = primary ? Accent : danger ? Color.FromArgb(253, 242, 242) : Color.White;
        Color foreground = primary ? Color.White : danger ? Danger : TextPrimary;
        Color border = primary ? Accent : danger ? Color.FromArgb(239, 180, 174) : Color.FromArgb(209, 213, 219);

        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 32,
            Padding = new Padding(11, 0, 11, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = background,
            ForeColor = foreground,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Margin = new Padding(0, 0, 8, 0),
            Font = new Font("Segoe UI", 9F)
        };

        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.MouseOverBackColor = primary
            ? AccentHover
            : danger
                ? Color.FromArgb(252, 231, 229)
                : Color.FromArgb(247, 248, 250);

        button.FlatAppearance.MouseDownBackColor = primary
            ? Color.FromArgb(0, 90, 158)
            : Color.FromArgb(238, 240, 243);

        return button;
    }

    private void ConfigureGrid()
    {
        dgvFiles.AutoGenerateColumns = false;
        dgvFiles.DataSource = _files;
        dgvFiles.Dock = DockStyle.Fill;
        dgvFiles.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvFiles.MultiSelect = true;
        dgvFiles.AllowUserToAddRows = false;
        dgvFiles.AllowUserToDeleteRows = false;
        dgvFiles.AllowUserToResizeRows = false;
        dgvFiles.ReadOnly = true;
        dgvFiles.RowHeadersVisible = false;
        dgvFiles.BorderStyle = BorderStyle.None;
        dgvFiles.BackgroundColor = CardBackground;
        dgvFiles.GridColor = Color.FromArgb(235, 237, 240);
        dgvFiles.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        dgvFiles.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        dgvFiles.EnableHeadersVisualStyles = false;
        dgvFiles.RowTemplate.Height = 31;

        dgvFiles.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 249, 251),
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Semibold", 9F),
            SelectionBackColor = Color.FromArgb(248, 249, 251),
            SelectionForeColor = TextSecondary,
            Padding = new Padding(6, 0, 0, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        dgvFiles.ColumnHeadersHeight = 34;

        dgvFiles.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(226, 239, 252),
            SelectionForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9F),
            Padding = new Padding(6, 0, 0, 0)
        };

        dgvFiles.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(252, 252, 253),
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(226, 239, 252),
            SelectionForeColor = TextPrimary
        };

        dgvFiles.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "File",
            DataPropertyName = nameof(InputFile.RelativePath),
            FillWeight = 42,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 260
        });

        dgvFiles.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Format",
            DataPropertyName = nameof(InputFile.FormatDisplay),
            Width = 190
        });

        dgvFiles.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Status",
            DataPropertyName = nameof(InputFile.Status),
            Width = 110,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI Semibold", 9F)
            }
        });

        dgvFiles.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Message",
            DataPropertyName = nameof(InputFile.Message),
            FillWeight = 36,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 220
        });
    }

    private void ConfigureToolTips()
    {
        toolTip.AutoPopDelay = 8000;
        toolTip.InitialDelay = 350;
        toolTip.ReshowDelay = 100;

        toolTip.SetToolTip(chkPreserveDirectories, "Recreate the input folder structure inside the selected output folder.");
        toolTip.SetToolTip(chkForceOverwrite, "-f: overwrite an existing generated Lua file.");
        toolTip.SetToolTip(chkSilentAssertions, "-s: suppress native assertion dialogs and skip decompiler failures. Recommended for batches.");
        toolTip.SetToolTip(chkIgnoreDebugInfo, "-i: ignore embedded debug information.");
        toolTip.SetToolTip(chkMinimizeDiffs, "-m: minimize differences in generated output where supported by the native decompiler.");
        toolTip.SetToolTip(chkUnrestrictedAscii, "-u: use unrestricted ASCII output behavior from the native decompiler.");
        toolTip.SetToolTip(txtExtensionFilter, "-e: process only files matching this extension, for example .lua.");
    }

    private async Task AddPathsAsync(IEnumerable<string> paths)
    {
        if (_scanCancellation != null || _batchCancellation != null)
            return;

        _scanCancellation = new CancellationTokenSource();
        SetBusyState(true);
        txtPreview.Clear();
        AppendLog("Scanning input...");

        int originalCount = _files.Count;

        try
        {
            Progress<ScanProgress> scanProgress = new(UpdateScanProgress);

            List<InputFile> scanned = await _scanner.ScanAsync(
                paths,
                scanProgress,
                _scanCancellation.Token);

            HashSet<string> existing = new(
                _files.Select(x => x.SourcePath),
                StringComparer.OrdinalIgnoreCase);

            int added = 0;
            foreach (InputFile item in scanned)
            {
                if (existing.Add(item.SourcePath))
                {
                    _files.Add(item);
                    added++;
                }
            }

            AppendLog($"Scan complete. {scanned.Count:N0} found, {added:N0} added, {_files.Count:N0} total listed.");
            SetProgressComplete("Scan complete", $"{added:N0} added · {_files.Count:N0} total", 1, 1);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Scan cancelled.");
            SetProgressComplete("Scan cancelled", $"{_files.Count:N0} files listed", 0, 1);
        }
        catch (Exception ex)
        {
            AppendLog($"Scan error: {ex.Message}");
            SetProgressComplete("Scan failed", ex.Message, 0, 1);
        }
        finally
        {
            _scanCancellation.Dispose();
            _scanCancellation = null;
            SetBusyState(false);

            if (_files.Count != originalCount)
                dgvFiles.Refresh();
        }
    }

    private void UpdateScanProgress(ScanProgress progress)
    {
        if (progress.Phase == ScanPhase.Discovering)
        {
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.MarqueeAnimationSpeed = 28;
            lblOperation.Text = "Discovering files";
            lblProgress.Text = progress.Current == 0
                ? "Scanning folders..."
                : $"{progress.Current:N0} found";
            return;
        }

        progressBar.MarqueeAnimationSpeed = 0;
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Minimum = 0;
        progressBar.Maximum = Math.Max(1, progress.Total);
        progressBar.Value = Math.Min(progress.Current, progressBar.Maximum);
        lblOperation.Text = "Analyzing bytecode";
        lblProgress.Text = $"{progress.Current:N0} / {progress.Total:N0}";
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (_scanCancellation == null &&
            _batchCancellation == null &&
            e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
    }

    private async void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
            await AddPathsAsync(paths);
    }

    private async void btnDecompile_Click(object? sender, EventArgs e)
    {
        if (_batchCancellation != null || _scanCancellation != null)
            return;

        if (!_decompiler.IsDecompilerAvailable)
        {
            MessageBox.Show(
                this,
                $"Decompiler not found:\n\n{_decompiler.DecompilerPath}",
                "Decompiler Missing",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        if (_files.Count == 0)
        {
            MessageBox.Show(
                this,
                "Add at least one LuaJIT bytecode file.",
                "Nothing to Decompile",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(txtOutputFolder.Text))
        {
            MessageBox.Show(
                this,
                "Select an output folder.",
                "Output Folder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Directory.CreateDirectory(txtOutputFolder.Text);

        var options = new DecompileOptions
        {
            ForceOverwrite = chkForceOverwrite.Checked,
            SilentAssertions = chkSilentAssertions.Checked,
            IgnoreDebugInfo = chkIgnoreDebugInfo.Checked,
            MinimizeDiffs = chkMinimizeDiffs.Checked,
            UnrestrictedAscii = chkUnrestrictedAscii.Checked,
            ExtensionFilter = txtExtensionFilter.Text
        };

        _batchCancellation = new CancellationTokenSource();
        SetBusyState(true);
        AppendLog($"Starting batch: {_files.Count:N0} files");

        lblOperation.Text = "Decompiling";
        lblProgress.Text = $"0 / {_files.Count:N0}";
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Minimum = 0;
        progressBar.Maximum = Math.Max(1, _files.Count);
        progressBar.Value = 0;

        try
        {
            Progress<BatchProgress> progress = new(p =>
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Maximum = Math.Max(1, p.Total);
                progressBar.Value = Math.Min(p.Completed, progressBar.Maximum);
                lblOperation.Text = "Decompiling";
                lblProgress.Text = $"{p.Completed:N0} / {p.Total:N0}";
                dgvFiles.Refresh();
            });

            await _batchDecompiler.RunAsync(
                _files.ToList(),
                txtOutputFolder.Text,
                chkPreserveDirectories.Checked,
                options,
                progress,
                AppendLog,
                _batchCancellation.Token);

            AppendLog("Batch completed.");
            SetProgressComplete(
                "Batch complete",
                $"{_files.Count(x => x.Status == FileStatus.Success):N0} successful",
                1,
                1);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Batch cancelled.");
            SetProgressComplete("Batch cancelled", "Cancelled", 0, 1);
        }
        catch (Exception ex)
        {
            AppendLog($"Batch error: {ex.Message}");
            SetProgressComplete("Batch failed", ex.Message, 0, 1);
            MessageBox.Show(this, ex.Message, "Batch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _batchCancellation.Dispose();
            _batchCancellation = null;
            SetBusyState(false);
            dgvFiles.Refresh();
        }
    }

    private void btnCancel_Click(object? sender, EventArgs e)
    {
        if (_scanCancellation != null)
        {
            AppendLog("Scan cancellation requested...");
            btnCancel.Enabled = false;
            _scanCancellation.Cancel();
            return;
        }

        if (_batchCancellation != null)
        {
            AppendLog("Cancellation requested...");
            btnCancel.Enabled = false;
            _batchCancellation.Cancel();
        }
    }

    private async void dgvFiles_SelectionChanged(object? sender, EventArgs e)
    {
        await PreviewCurrentFileAsync();
    }

    private void dgvFiles_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            dgvFiles.Columns[e.ColumnIndex].DataPropertyName != nameof(InputFile.Status))
        {
            return;
        }

        if (dgvFiles.Rows[e.RowIndex].DataBoundItem is not InputFile item)
            return;

        DataGridViewCellStyle? cellStyle = e.CellStyle;
        if (cellStyle is null)
            return;

        cellStyle.ForeColor = item.Status switch
        {
            FileStatus.Success => Success,
            FileStatus.Ready => Accent,
            FileStatus.Processing => Accent,
            FileStatus.Failed => Danger,
            FileStatus.Unsupported => TextSecondary,
            FileStatus.Skipped => Warning,
            FileStatus.Cancelled => Warning,
            _ => TextPrimary
        };
    }

    private void dgvFiles_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
            return;

        if (dgvFiles.Rows[e.RowIndex].DataBoundItem is InputFile item &&
            item.Status == FileStatus.Success &&
            !string.IsNullOrWhiteSpace(item.OutputPath) &&
            File.Exists(item.OutputPath))
        {
            OpenPath(item.OutputPath);
        }
    }

    private void dgvFiles_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0)
            return;

        if (!dgvFiles.Rows[e.RowIndex].Selected)
        {
            dgvFiles.ClearSelection();
            dgvFiles.Rows[e.RowIndex].Selected = true;
        }

        dgvFiles.CurrentCell = dgvFiles.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
    }

    private static async Task<string> ReadPreviewAsync(string filePath)
    {
        byte[] data = await File.ReadAllBytesAsync(filePath);

        try
        {
            UTF8Encoding strictUtf8 = new(false, true);
            return strictUtf8.GetString(data);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(data);
        }
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }

        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        txtLog.AppendText(line + Environment.NewLine);
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }

    private void ConfigureFileContextMenu()
    {
        var menu = new ContextMenuStrip();
        var previewItem = new ToolStripMenuItem("Preview");
        var openItem = new ToolStripMenuItem("Open generated Lua");
        var showItem = new ToolStripMenuItem("Show in output folder");
        var copySourceItem = new ToolStripMenuItem("Copy source path");
        var copyOutputItem = new ToolStripMenuItem("Copy output path");
        var removeItem = new ToolStripMenuItem("Remove from list");

        menu.Items.AddRange([
            previewItem,
            openItem,
            showItem,
            new ToolStripSeparator(),
            copySourceItem,
            copyOutputItem,
            new ToolStripSeparator(),
            removeItem
        ]);

        menu.Opening += (_, e) =>
        {
            InputFile? item = GetCurrentFile();
            if (item is null)
            {
                e.Cancel = true;
                return;
            }

            bool hasOutput = !string.IsNullOrWhiteSpace(item.OutputPath) && File.Exists(item.OutputPath);
            previewItem.Enabled = hasOutput;
            openItem.Enabled = hasOutput;
            showItem.Enabled = hasOutput;
            copyOutputItem.Enabled = !string.IsNullOrWhiteSpace(item.OutputPath);
            removeItem.Enabled = _scanCancellation is null && _batchCancellation is null;
        };

        previewItem.Click += async (_, _) => await PreviewCurrentFileAsync();
        openItem.Click += (_, _) =>
        {
            InputFile? item = GetCurrentFile();
            if (!string.IsNullOrWhiteSpace(item?.OutputPath))
                OpenPath(item.OutputPath);
        };
        showItem.Click += (_, _) =>
        {
            InputFile? item = GetCurrentFile();
            if (!string.IsNullOrWhiteSpace(item?.OutputPath))
                ShowInExplorer(item.OutputPath);
        };
        copySourceItem.Click += (_, _) =>
        {
            InputFile? item = GetCurrentFile();
            if (!string.IsNullOrWhiteSpace(item?.SourcePath))
                Clipboard.SetText(item.SourcePath);
        };
        copyOutputItem.Click += (_, _) =>
        {
            InputFile? item = GetCurrentFile();
            if (!string.IsNullOrWhiteSpace(item?.OutputPath))
                Clipboard.SetText(item.OutputPath);
        };
        removeItem.Click += (_, _) => RemoveSelectedFiles();

        dgvFiles.ContextMenuStrip = menu;
    }

    private InputFile? GetCurrentFile() =>
        dgvFiles.CurrentRow?.DataBoundItem as InputFile;

    private async Task PreviewCurrentFileAsync()
    {
        InputFile? file = GetCurrentFile();
        if (file is null ||
            string.IsNullOrWhiteSpace(file.OutputPath) ||
            !File.Exists(file.OutputPath))
        {
            txtPreview.Clear();
            return;
        }

        try
        {
            txtPreview.Text = await ReadPreviewAsync(file.OutputPath);
        }
        catch (Exception ex)
        {
            txtPreview.Text = $"Unable to preview file:\r\n\r\n{ex.Message}";
        }
    }

    private void RemoveSelectedFiles()
    {
        foreach (DataGridViewRow row in dgvFiles.SelectedRows.Cast<DataGridViewRow>().ToList())
        {
            if (row.DataBoundItem is InputFile item)
                _files.Remove(item);
        }
    }

    private void OpenOutputFolder()
    {
        string path = txtOutputFolder.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(this, "Select an output folder first.", "Output Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Directory.CreateDirectory(path);
            OpenPath(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unable to Open Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // Caller-facing actions remain non-fatal if Windows cannot open the target.
        }
    }

    private static void ShowInExplorer(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                OpenPath(directory);
        }
    }

    private void RestoreSettings()
    {
        AppSettings settings = _settingsService.Load();

        txtOutputFolder.Text = settings.OutputFolder;
        chkPreserveDirectories.Checked = settings.PreserveDirectories;
        chkForceOverwrite.Checked = settings.ForceOverwrite;
        chkSilentAssertions.Checked = settings.SilentAssertions;
        chkIgnoreDebugInfo.Checked = settings.IgnoreDebugInfo;
        chkMinimizeDiffs.Checked = settings.MinimizeDiffs;
        chkUnrestrictedAscii.Checked = settings.UnrestrictedAscii;
        txtExtensionFilter.Text = settings.ExtensionFilter;

        Width = Math.Max(MinimumSize.Width, settings.WindowWidth);
        Height = Math.Max(MinimumSize.Height, settings.WindowHeight);
        if (settings.WindowMaximized)
            WindowState = FormWindowState.Maximized;
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_scanCancellation is not null || _batchCancellation is not null)
        {
            DialogResult result = MessageBox.Show(
                this,
                "An operation is still running. Cancel it and exit?",
                "Exit LuaJIT Decompiler v2 GUI",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _scanCancellation?.Cancel();
            _batchCancellation?.Cancel();
        }

        Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;

        var settings = new AppSettings
        {
            OutputFolder = txtOutputFolder.Text.Trim(),
            PreserveDirectories = chkPreserveDirectories.Checked,
            ForceOverwrite = chkForceOverwrite.Checked,
            SilentAssertions = chkSilentAssertions.Checked,
            IgnoreDebugInfo = chkIgnoreDebugInfo.Checked,
            MinimizeDiffs = chkMinimizeDiffs.Checked,
            UnrestrictedAscii = chkUnrestrictedAscii.Checked,
            ExtensionFilter = txtExtensionFilter.Text.Trim(),
            WindowWidth = Math.Max(MinimumSize.Width, bounds.Width),
            WindowHeight = Math.Max(MinimumSize.Height, bounds.Height),
            WindowMaximized = WindowState == FormWindowState.Maximized
        };

        try
        {
            _settingsService.Save(settings);
        }
        catch
        {
            // Settings persistence must never prevent the app from closing.
        }
    }

    private void SetBusyState(bool busy)
    {
        btnAddFiles.Enabled = !busy;
        btnAddFolder.Enabled = !busy;
        btnRemove.Enabled = !busy;
        btnClear.Enabled = !busy;
        btnBrowseOutput.Enabled = !busy;
        btnOpenOutput.Enabled = !busy;
        btnDecompile.Enabled = !busy;
        btnCancel.Enabled = busy;

        txtOutputFolder.Enabled = !busy;
        chkPreserveDirectories.Enabled = !busy;
        chkForceOverwrite.Enabled = !busy;
        chkSilentAssertions.Enabled = !busy;
        chkIgnoreDebugInfo.Enabled = !busy;
        chkMinimizeDiffs.Enabled = !busy;
        chkUnrestrictedAscii.Enabled = !busy;
        txtExtensionFilter.Enabled = !busy;
    }

    private void SetProgressComplete(string operation, string detail, int value, int maximum)
    {
        progressBar.MarqueeAnimationSpeed = 0;
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Minimum = 0;
        progressBar.Maximum = Math.Max(1, maximum);
        progressBar.Value = Math.Clamp(value, 0, progressBar.Maximum);
        lblOperation.Text = operation;
        lblProgress.Text = detail;
    }

    private void UpdateFileCount()
    {
        int bytecode = _files.Count(x => x.Detection.IsSupportedBytecode);
        lblFileCount.Text = $"{_files.Count:N0} files · {bytecode:N0} LuaJIT bytecode";
    }

    private void UpdateDecompilerStatus()
    {
        if (_decompiler.IsDecompilerAvailable)
        {
            lblDecompiler.Text = "●  Decompiler ready";
            lblDecompiler.ForeColor = Success;
        }
        else
        {
            lblDecompiler.Text = "●  Decompiler missing";
            lblDecompiler.ForeColor = Danger;
            toolTip.SetToolTip(lblDecompiler, "Expected at third_party\\luajit-decompiler-v2.exe");
        }
    }

    private void UpdateVersionLabel()
    {
        string? informationalVersion = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        lblVersion.Text = string.IsNullOrWhiteSpace(informationalVersion)
            ? "Pre-release"
            : $"v{informationalVersion}";
    }
}

internal sealed class ThemedProgressBar : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private int _marqueeOffset;
    private readonly System.Windows.Forms.Timer _marqueeTimer;
    private ProgressBarStyle _style = ProgressBarStyle.Continuous;
    private int _marqueeAnimationSpeed = 30;

    public ThemedProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 18;
        _marqueeTimer = new System.Windows.Forms.Timer();
        _marqueeTimer.Tick += (_, _) =>
        {
            _marqueeOffset = (_marqueeOffset + Math.Max(3, Width / 45)) % Math.Max(1, Width + 1);
            Invalidate();
        };
    }

    public bool DarkMode { get; set; }

    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; if (_maximum < value) _maximum = value; Value = _value; Invalidate(); }
    }

    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(value, _minimum); Value = _value; Invalidate(); }
    }

    public int Value
    {
        get => _value;
        set { _value = Math.Clamp(value, _minimum, _maximum); Invalidate(); }
    }

    public ProgressBarStyle Style
    {
        get => _style;
        set
        {
            _style = value;
            UpdateMarqueeTimer();
            Invalidate();
        }
    }

    public int MarqueeAnimationSpeed
    {
        get => _marqueeAnimationSpeed;
        set
        {
            _marqueeAnimationSpeed = Math.Max(0, value);
            UpdateMarqueeTimer();
        }
    }

    private void UpdateMarqueeTimer()
    {
        if (_style == ProgressBarStyle.Marquee && _marqueeAnimationSpeed > 0)
        {
            _marqueeTimer.Interval = Math.Max(10, _marqueeAnimationSpeed);
            _marqueeTimer.Start();
        }
        else
        {
            _marqueeTimer.Stop();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Rectangle bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        Color track = DarkMode ? Color.FromArgb(17, 24, 39) : Color.FromArgb(229, 231, 235);
        Color border = DarkMode ? Color.FromArgb(75, 85, 99) : Color.FromArgb(209, 213, 219);
        Color fill = Color.FromArgb(0, 120, 212);

        using var trackBrush = new SolidBrush(track);
        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(border);

        e.Graphics.FillRectangle(trackBrush, bounds);

        Rectangle inner = Rectangle.Inflate(bounds, -1, -1);
        if (inner.Width > 0 && inner.Height > 0)
        {
            if (_style == ProgressBarStyle.Marquee)
            {
                int blockWidth = Math.Max(36, inner.Width / 5);
                int x = _marqueeOffset - blockWidth;
                e.Graphics.FillRectangle(fillBrush, new Rectangle(x, inner.Y, blockWidth, inner.Height));
                if (x + blockWidth < inner.Right)
                    e.Graphics.FillRectangle(fillBrush, new Rectangle(x + inner.Width + blockWidth, inner.Y, blockWidth, inner.Height));
            }
            else
            {
                double range = Math.Max(1, _maximum - _minimum);
                double ratio = Math.Clamp((_value - _minimum) / range, 0d, 1d);
                int width = (int)Math.Round(inner.Width * ratio);
                if (width > 0)
                    e.Graphics.FillRectangle(fillBrush, new Rectangle(inner.X, inner.Y, width, inner.Height));
            }
        }

        e.Graphics.DrawRectangle(borderPen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _marqueeTimer.Dispose();
        base.Dispose(disposing);
    }
}

