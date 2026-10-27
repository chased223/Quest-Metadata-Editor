using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace QuestMetadataEditor;

internal sealed class MainForm : Form
{
    private static readonly Color Win7Blue = Color.FromArgb(45, 105, 164);
    private static readonly Color WindowBlue = Color.FromArgb(217, 231, 246);
    private static readonly Color Canvas = Color.FromArgb(248, 250, 253);
    private static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";

    private readonly ToolStripStatusLabel statusLabel = new("Ready");
    private readonly Label pathLabel = new()
    {
        AutoEllipsis = true,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(70, 82, 98)
    };
    private readonly Panel contentHost = new() { Dock = DockStyle.Fill, BackColor = Canvas, Padding = new Padding(12) };
    private readonly Panel overviewPanel = new() { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20) };
    private readonly Panel editorPanel = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly Panel archivePanel = new() { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };
    private readonly Panel welcomePanel = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly TextBox editorTextBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        AcceptsTab = true,
        WordWrap = false,
        ScrollBars = ScrollBars.Both,
        Font = new Font("Consolas", 10F),
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly ListView archiveList = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        HideSelection = false
    };
    private readonly TextBox packageBox = NewField();
    private readonly TextBox appLabelBox = NewField();
    private readonly TextBox versionNameBox = NewField();
    private readonly TextBox versionCodeBox = NewField();
    private readonly TextBox minSdkBox = NewField();
    private readonly TextBox targetSdkBox = NewField();
    private readonly Label formatValue = NewValueLabel();
    private readonly Label fileSizeValue = NewValueLabel();
    private readonly Label entriesValue = NewValueLabel();
    private readonly ListBox permissionsList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label overviewHint = new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Padding = new Padding(10),
        BackColor = Color.FromArgb(239, 246, 253),
        ForeColor = Color.FromArgb(47, 81, 116),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private string? currentPath;
    private string currentText = string.Empty;
    private bool isArchive;
    private bool isManifestText;
    private MetadataInfo metadata = new();
    private List<string> archiveEntryNames = new();

    public MainForm()
    {
        Text = "Quest Metadata Editor";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 590);
        Size = new Size(1120, 740);
        Font = new Font("Segoe UI", 9F);
        BackColor = Canvas;
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildInterface();
        ShowPage("Welcome");
        UpdatePathLabel();
    }

    private void BuildInterface()
    {
        var menu = new MenuStrip
        {
            BackColor = WindowBlue,
            ForeColor = Color.FromArgb(30, 47, 66),
            Renderer = new ToolStripProfessionalRenderer(new Win7ColorTable())
        };
        var fileMenu = new ToolStripMenuItem("File");
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Open...", null, (_, _) => OpenFile()) { ShortcutKeys = Keys.Control | Keys.O });
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save", null, (_, _) => SaveFile()) { ShortcutKeys = Keys.Control | Keys.S });
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save As...", null, (_, _) => SaveFileAs()) { ShortcutKeys = Keys.Control | Keys.Shift | Keys.S });
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Export metadata report...", null, (_, _) => ExportReport()));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Exit", null, (_, _) => Close()));
        menu.Items.Add(fileMenu);
        var viewMenu = new ToolStripMenuItem("View");
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("Overview", null, (_, _) => ShowPage("Overview")));
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("Manifest / text editor", null, (_, _) => ShowPage("Editor")));
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("APK contents", null, (_, _) => ShowPage("Archive")));
        menu.Items.Add(viewMenu);
        MainMenuStrip = menu;

        var toolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = Color.FromArgb(240, 246, 252),
            Renderer = new ToolStripProfessionalRenderer(new Win7ColorTable()),
            Padding = new Padding(6, 4, 6, 4)
        };
        toolbar.Items.Add(new ToolStripButton("Open", null, (_, _) => OpenFile()) { ToolTipText = "Open an APK, XML, JSON or text file" });
        toolbar.Items.Add(new ToolStripButton("Save", null, (_, _) => SaveFile()) { ToolTipText = "Save the current text file" });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("Overview", null, (_, _) => ShowPage("Overview")));
        toolbar.Items.Add(new ToolStripButton("Editor", null, (_, _) => ShowPage("Editor")));
        toolbar.Items.Add(new ToolStripButton("APK contents", null, (_, _) => ShowPage("Archive")));
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("Export report", null, (_, _) => ExportReport()));

        var pathBar = new Panel { Dock = DockStyle.Top, Height = 31, BackColor = Color.FromArgb(245, 248, 252), Padding = new Padding(10, 0, 10, 0) };
        pathBar.Controls.Add(pathLabel);

        BuildOverviewPage();
        BuildEditorPage();
        BuildArchivePage();
        BuildWelcomePage();
        contentHost.Controls.Add(overviewPanel);
        contentHost.Controls.Add(editorPanel);
        contentHost.Controls.Add(archivePanel);
        contentHost.Controls.Add(welcomePanel);

        var leftNav = new Panel { Dock = DockStyle.Left, Width = 174, BackColor = Color.FromArgb(237, 244, 251), Padding = new Padding(10) };
        var navHeader = new Label
        {
            Text = "WORKSPACE",
            Dock = DockStyle.Top,
            Height = 32,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(102, 119, 139),
            TextAlign = ContentAlignment.MiddleLeft
        };
        leftNav.Controls.Add(navHeader);
        leftNav.Controls.Add(CreateNavButton("App overview", "Overview", 42));
        leftNav.Controls.Add(CreateNavButton("Manifest editor", "Editor", 82));
        leftNav.Controls.Add(CreateNavButton("APK contents", "Archive", 122));
        var navFooter = new Label
        {
            Text = "Quest Metadata Editor\nVersion 1.0.0",
            Dock = DockStyle.Bottom,
            Height = 48,
            ForeColor = Color.FromArgb(112, 127, 145),
            TextAlign = ContentAlignment.BottomLeft
        };
        leftNav.Controls.Add(navFooter);

        var status = new StatusStrip
        {
            BackColor = Color.FromArgb(235, 243, 251),
            SizingGrip = true,
            Renderer = new ToolStripProfessionalRenderer(new Win7ColorTable())
        };
        status.Items.Add(statusLabel);
        status.Items.Add(new ToolStripStatusLabel { Spring = true });
        status.Items.Add(new ToolStripStatusLabel("Windows desktop · Local files only") { ForeColor = Color.FromArgb(94, 109, 126) });

        Controls.Add(contentHost);
        Controls.Add(leftNav);
        Controls.Add(pathBar);
        Controls.Add(toolbar);
        Controls.Add(menu);
        Controls.Add(status);
        menu.Dock = DockStyle.Top;
        toolbar.Dock = DockStyle.Top;
        pathBar.Dock = DockStyle.Top;
        status.Dock = DockStyle.Bottom;

        archiveList.DoubleClick += (_, _) => ExtractSelectedArchiveEntry();
        archiveList.SelectedIndexChanged += (_, _) => UpdateArchiveStatus();
        editorTextBox.TextChanged += (_, _) =>
        {
            if (!editorTextBox.ReadOnly)
                currentText = editorTextBox.Text;
        };
    }

    private static Button CreateNavButton(string text, string page, int top)
    {
        var button = new Button
        {
            Text = "  " + text,
            Tag = page,
            Left = 0,
            Top = top,
            Width = 150,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(237, 244, 251),
            ForeColor = Color.FromArgb(41, 63, 88),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(198, 216, 234);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(222, 236, 250);
        button.Click += (_, _) =>
        {
            if (button.Parent?.Parent is MainForm form)
                form.ShowPage(page);
        };
        return button;
    }

    private void BuildOverviewPage()
    {
        var heading = new Label
        {
            Text = "App overview",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Segoe UI", 17F, FontStyle.Regular),
            ForeColor = Win7Blue
        };
        var subtitle = new Label
        {
            Text = "Inspect Android / Meta Quest package metadata. Open a decoded AndroidManifest.xml to edit manifest fields.",
            Dock = DockStyle.Top,
            Height = 38,
            ForeColor = Color.FromArgb(89, 103, 122)
        };
        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 278,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(0, 10, 0, 10),
            BackColor = Color.White
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 9; i++) details.RowStyles.Add(new RowStyle(SizeType.Absolute, i < 6 ? 30 : 25));
        AddFieldRow(details, 0, "Package name", packageBox);
        AddFieldRow(details, 1, "App label", appLabelBox);
        AddFieldRow(details, 2, "Version name", versionNameBox);
        AddFieldRow(details, 3, "Version code", versionCodeBox);
        AddFieldRow(details, 4, "Minimum SDK", minSdkBox);
        AddFieldRow(details, 5, "Target SDK", targetSdkBox);
        AddFieldRow(details, 6, "Manifest format", formatValue);
        AddFieldRow(details, 7, "File size", fileSizeValue);
        AddFieldRow(details, 8, "Archive entries", entriesValue);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 47, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 4) };
        var applyButton = NewActionButton("Apply fields to XML", 148);
        applyButton.Click += (_, _) => ApplyFieldsToManifest();
        buttons.Controls.Add(applyButton);
        var openButton = NewActionButton("Open file...", 112);
        openButton.Click += (_, _) => OpenFile();
        buttons.Controls.Add(openButton);
        var exportButton = NewActionButton("Export report...", 128);
        exportButton.Click += (_, _) => ExportReport();
        buttons.Controls.Add(exportButton);

        overviewHint.Text = "Tip: A normal APK usually stores AndroidManifest.xml as binary XML. The app can inspect its common metadata, but it only edits manifest fields when the file is decoded to regular XML.";
        overviewHint.Height = 56;

        var permissionHeading = new Label
        {
            Text = "Declared permissions",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(58, 77, 100)
        };
        permissionsList.Height = 100;
        permissionsList.BorderStyle = BorderStyle.FixedSingle;

        overviewPanel.Controls.Add(permissionsList);
        overviewPanel.Controls.Add(permissionHeading);
        overviewPanel.Controls.Add(overviewHint);
        overviewPanel.Controls.Add(buttons);
        overviewPanel.Controls.Add(details);
        overviewPanel.Controls.Add(subtitle);
        overviewPanel.Controls.Add(heading);
    }

    private void BuildEditorPage()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.White, Padding = new Padding(14, 10, 14, 4) };
        var title = new Label { Text = "Manifest / text editor", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 16F), ForeColor = Win7Blue };
        var description = new Label { Text = "Edit decoded AndroidManifest.xml, JSON metadata, or other plain-text metadata files.", Dock = DockStyle.Top, Height = 25, ForeColor = Color.FromArgb(91, 104, 122) };
        header.Controls.Add(description);
        header.Controls.Add(title);
        editorPanel.Controls.Add(editorTextBox);
        editorPanel.Controls.Add(header);
        editorTextBox.ReadOnly = true;
        editorTextBox.BackColor = Color.FromArgb(247, 249, 252);
    }

    private void BuildArchivePage()
    {
        var heading = new Label { Text = "APK contents", Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI", 16F), ForeColor = Win7Blue };
        var hint = new Label { Text = "Select an entry and double-click it to extract a copy. APK files are ZIP-based archives.", Dock = DockStyle.Top, Height = 30, ForeColor = Color.FromArgb(91, 104, 122) };
        archiveList.Columns.Add("Entry", 500);
        archiveList.Columns.Add("Uncompressed", 110, HorizontalAlignment.Right);
        archiveList.Columns.Add("Compressed", 110, HorizontalAlignment.Right);
        archiveList.Columns.Add("Type", 100);
        archivePanel.Controls.Add(archiveList);
        archivePanel.Controls.Add(hint);
        archivePanel.Controls.Add(heading);
    }

    private void BuildWelcomePage()
    {
        var center = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var welcomeTitle = new Label
        {
            Text = "Quest Metadata Editor",
            Font = new Font("Segoe UI", 22F, FontStyle.Regular),
            ForeColor = Color.FromArgb(38, 74, 155),
            AutoSize = true
        };
        var description = new Label
        {
            Text = "A simple Windows 7-inspired utility for inspecting Quest APKs and editing decoded metadata files.",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(78, 91, 108),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Width = 500,
            Height = 48
        };
        var openButton = NewActionButton("Open a file...", 135);
        openButton.Height = 34;
        openButton.Click += (_, _) => OpenFile();
        var note = new Label
        {
            Text = "Open an APK to inspect it, or open an XML / JSON / text file to edit it.\nOriginal files are not modified until you explicitly save.",
            AutoSize = false,
            Width = 500,
            Height = 46,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(111, 125, 143)
        };
        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            Padding = new Padding(10)
        };
        stack.Controls.Add(welcomeTitle);
        stack.Controls.Add(description);
        stack.Controls.Add(openButton);
        stack.Controls.Add(note);
        center.Controls.Add(stack);
        center.Resize += (_, _) => stack.Location = new Point(Math.Max(12, (center.ClientSize.Width - stack.Width) / 2), Math.Max(12, (center.ClientSize.Height - stack.Height) / 2 - 18));
        welcomePanel.Controls.Add(center);
    }

    private static void AddFieldRow(TableLayoutPanel table, int row, string labelText, Control valueControl)
    {
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(68, 83, 103)
        };
        valueControl.Dock = DockStyle.Fill;
        if (valueControl is TextBox box)
        {
            box.Margin = new Padding(3, 3, 3, 4);
        }
        else
        {
            valueControl.Margin = new Padding(3, 5, 3, 3);
        }
        table.Controls.Add(label, 0, row);
        table.Controls.Add(valueControl, 1, row);
    }

    private static TextBox NewField() => new()
    {
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 9F),
        BackColor = Color.White
    };

    private static Label NewValueLabel() => new()
    {
        AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(76, 91, 110)
    };

    private static Button NewActionButton(string text, int width) => new()
    {
        Text = text,
        Width = width,
        Height = 28,
        FlatStyle = FlatStyle.System,
        Margin = new Padding(0, 0, 8, 0),
        Cursor = Cursors.Hand
    };

    private void ShowPage(string page)
    {
        welcomePanel.Visible = page == "Welcome";
        overviewPanel.Visible = page == "Overview";
        editorPanel.Visible = page == "Editor";
        archivePanel.Visible = page == "Archive";
        if (page == "Overview") RefreshOverviewControls();
        if (page == "Archive") archiveList.Focus();
    }

    private void OpenFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open Quest metadata or APK",
            Filter = "APK / archive / metadata files|*.apk;*.obb;*.zip;*.xml;*.json;*.txt;*.manifest;*.config|Android packages (*.apk)|*.apk|Metadata files (*.xml;*.json;*.txt;*.manifest)|*.xml;*.json;*.txt;*.manifest|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            LoadPath(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or XmlException or JsonException or ArgumentException)
        {
            MessageBox.Show(this, "The selected file could not be opened.\n\n" + ex.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Could not open file");
        }
    }

    private void LoadPath(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The selected file no longer exists.", path);
        currentPath = path;
        isArchive = LooksLikeArchive(path);
        isManifestText = false;
        currentText = string.Empty;
        metadata = new MetadataInfo
        {
            SourcePath = path,
            FileSize = info.Length,
            ManifestFormat = "Not detected"
        };
        archiveEntryNames.Clear();
        archiveList.Items.Clear();
        editorTextBox.ReadOnly = false;
        editorTextBox.BackColor = Color.White;

        if (isArchive)
        {
            LoadArchive(path);
            ShowPage("Overview");
            SetStatus($"Opened archive · {metadata.ArchiveEntryCount:N0} entries");
        }
        else
        {
            if (info.Length > 64L * 1024 * 1024)
                throw new InvalidDataException("For safety, text files larger than 64 MB are not opened in the editor.");
            byte[] bytes = File.ReadAllBytes(path);
            currentText = DecodeText(bytes);
            editorTextBox.Text = currentText;
            if (IsTextXml(currentText) && TryLoadPlainManifest(currentText, out MetadataInfo parsed))
            {
                isManifestText = true;
                metadata = MergeFileInfo(parsed, path, info.Length);
                overviewHint.Text = "Editable manifest: change the fields above, choose Apply fields to XML, then save the XML file. If you plan to rebuild an APK, use the matching decoded project workflow and sign the rebuilt package with your own key.";
            }
            else
            {
                metadata = MergeFileInfo(ParseGenericText(currentText, Path.GetExtension(path)), path, info.Length);
                overviewHint.Text = "This is a plain-text file. Edit the text in Manifest / text editor and save it. App fields are editable only when a valid AndroidManifest.xml is detected.";
            }
            editorTextBox.ReadOnly = false;
            editorTextBox.BackColor = Color.White;
            ShowPage(isManifestText ? "Overview" : "Editor");
            SetStatus($"Opened text file · {info.Length:N0} bytes");
        }

        UpdatePathLabel();
        RefreshOverviewControls();
    }

    private void LoadArchive(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        metadata.ArchiveEntryCount = archive.Entries.Count;
        metadata.ManifestFormat = "APK / ZIP archive";
        foreach (ZipArchiveEntry entry in archive.Entries.OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
        {
            archiveEntryNames.Add(entry.FullName);
            string extension = Path.GetExtension(entry.FullName);
            var item = new ListViewItem(entry.FullName);
            item.SubItems.Add(FormatSize(entry.Length));
            item.SubItems.Add(FormatSize(entry.CompressedLength));
            item.SubItems.Add(string.IsNullOrEmpty(extension) ? "Folder" : extension.TrimStart('.').ToUpperInvariant());
            item.Tag = entry.FullName;
            archiveList.Items.Add(item);
        }

        ZipArchiveEntry? manifest = archive.Entries.FirstOrDefault(e =>
            e.FullName.Equals("AndroidManifest.xml", StringComparison.OrdinalIgnoreCase));
        if (manifest == null)
        {
            metadata.ManifestFormat = "No AndroidManifest.xml found";
            currentText = string.Empty;
            editorTextBox.Text = "No AndroidManifest.xml was found at the archive root.\r\n\r\nThis may be an OBB or a non-APK ZIP archive.";
            editorTextBox.ReadOnly = true;
            editorTextBox.BackColor = Color.FromArgb(247, 249, 252);
            overviewHint.Text = "This archive has no AndroidManifest.xml at its root. Browse the file list and extract metadata files as needed.";
            return;
        }

        if (manifest.Length > 32L * 1024 * 1024)
            throw new InvalidDataException("AndroidManifest.xml is unexpectedly large; it was not read.");
        using var manifestStream = manifest.Open();
        using var memory = new MemoryStream((int)Math.Min(manifest.Length, int.MaxValue));
        manifestStream.CopyTo(memory);
        byte[] bytes = memory.ToArray();
        if (LooksLikeXml(bytes))
        {
            currentText = DecodeText(bytes);
            editorTextBox.Text = currentText;
            if (TryLoadPlainManifest(currentText, out MetadataInfo parsed))
            {
                metadata = MergeArchiveInfo(parsed, path, new FileInfo(path).Length, archive.Entries.Count);
                isManifestText = false;
                editorTextBox.ReadOnly = true;
                editorTextBox.BackColor = Color.FromArgb(247, 249, 252);
                overviewHint.Text = "The APK contains a plain-text XML manifest. Inspect it here; extract AndroidManifest.xml from APK contents to edit a separate copy.";
            }
            else
            {
                metadata.ManifestFormat = "XML (not an Android manifest)";
                editorTextBox.ReadOnly = true;
            }
        }
        else
        {
            MetadataInfo parsed = AndroidBinaryManifestReader.Read(bytes);
            metadata = MergeArchiveInfo(parsed, path, new FileInfo(path).Length, archive.Entries.Count);
            currentText = string.Empty;
            editorTextBox.Text = "This APK stores AndroidManifest.xml in Android's compiled binary XML format.\r\n\r\nThe app has read the common metadata fields for inspection. To edit them, open a decoded AndroidManifest.xml file (regular text XML) from an APK decode project.\r\n\r\nNo APK bytes have been changed.";
            editorTextBox.ReadOnly = true;
            editorTextBox.BackColor = Color.FromArgb(247, 249, 252);
            isManifestText = false;
            overviewHint.Text = "Binary manifest detected. Metadata can be inspected, but direct APK rewriting is intentionally not attempted: resource references, APK signatures and compatibility need a proper decode/rebuild/sign step.";
        }
    }

    private static bool LooksLikeArchive(string path)
    {
        string extension = Path.GetExtension(path);
        if (extension.Equals(".apk", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".obb", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return true;
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[4];
            return stream.Read(header) == 4 && header[0] == (byte)'P' && header[1] == (byte)'K';
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeXml(byte[] bytes)
    {
        int index = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) index = 3;
        while (index < bytes.Length && char.IsWhiteSpace((char)bytes[index])) index++;
        return index < bytes.Length && bytes[index] == (byte)'<';
    }

    private static bool IsTextXml(string text) => text.TrimStart('\uFEFF', ' ', '\r', '\n', '\t').StartsWith("<", StringComparison.Ordinal);

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        return new UTF8Encoding(false, false).GetString(bytes);
    }

    private static bool TryLoadPlainManifest(string xml, out MetadataInfo info)
    {
        info = new MetadataInfo { ManifestFormat = "Plain XML" };
        try
        {
            XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            XElement? manifest = document.Root;
            if (manifest == null || manifest.Name.LocalName != "manifest") return false;
            info.PackageName = (string?)manifest.Attribute("package") ?? "Not detected";
            info.VersionCode = (string?)manifest.Attribute(AndroidNs + "versionCode") ?? "Not detected";
            info.VersionName = (string?)manifest.Attribute(AndroidNs + "versionName") ?? "Not detected";
            XElement? app = manifest.Elements().FirstOrDefault(e => e.Name.LocalName == "application");
            info.AppLabel = (string?)app?.Attribute(AndroidNs + "label") ?? "Not detected";
            XElement? sdk = manifest.Elements().FirstOrDefault(e => e.Name.LocalName == "uses-sdk");
            info.MinSdk = (string?)sdk?.Attribute(AndroidNs + "minSdkVersion") ?? "Not detected";
            info.TargetSdk = (string?)sdk?.Attribute(AndroidNs + "targetSdkVersion") ?? "Not detected";
            foreach (XElement element in manifest.Elements().Where(e => e.Name.LocalName.StartsWith("uses-permission", StringComparison.Ordinal)))
            {
                string? permission = (string?)element.Attribute(AndroidNs + "name");
                if (!string.IsNullOrWhiteSpace(permission)) info.Permissions.Add(permission);
            }
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static MetadataInfo ParseGenericText(string text, string extension)
    {
        var info = new MetadataInfo { ManifestFormat = extension.TrimStart('.').ToUpperInvariant() + " text" };
        if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase)) return info;
        try
        {
            using JsonDocument json = JsonDocument.Parse(text);
            JsonElement root = json.RootElement;
            info.PackageName = FindJsonValue(root, "package", "packageName", "package_name", "applicationId") ?? info.PackageName;
            info.AppLabel = FindJsonValue(root, "appLabel", "appName", "name", "label") ?? info.AppLabel;
            info.VersionName = FindJsonValue(root, "versionName", "version", "version_name") ?? info.VersionName;
            info.VersionCode = FindJsonValue(root, "versionCode", "version_code") ?? info.VersionCode;
            info.MinSdk = FindJsonValue(root, "minSdk", "minSdkVersion", "min_sdk") ?? info.MinSdk;
            info.TargetSdk = FindJsonValue(root, "targetSdk", "targetSdkVersion", "target_sdk") ?? info.TargetSdk;
        }
        catch (JsonException)
        {
            info.ManifestFormat = "JSON text (invalid JSON)";
        }
        return info;
    }

    private static string? FindJsonValue(JsonElement element, params string[] keys)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (keys.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind == JsonValueKind.String) return property.Value.GetString();
                    if (property.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                        return property.Value.ToString();
                }
            }
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string? nested = FindJsonValue(property.Value, keys);
                if (nested != null) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                string? nested = FindJsonValue(child, keys);
                if (nested != null) return nested;
            }
        }
        return null;
    }

    private static MetadataInfo MergeFileInfo(MetadataInfo parsed, string path, long size)
    {
        parsed.SourcePath = path;
        parsed.FileSize = size;
        return parsed;
    }

    private static MetadataInfo MergeArchiveInfo(MetadataInfo parsed, string path, long size, int count)
    {
        parsed.SourcePath = path;
        parsed.FileSize = size;
        parsed.ArchiveEntryCount = count;
        return parsed;
    }

    private void RefreshOverviewControls()
    {
        SetText(packageBox, metadata.PackageName);
        SetText(appLabelBox, metadata.AppLabel);
        SetText(versionNameBox, metadata.VersionName);
        SetText(versionCodeBox, metadata.VersionCode);
        SetText(minSdkBox, metadata.MinSdk);
        SetText(targetSdkBox, metadata.TargetSdk);
        formatValue.Text = metadata.ManifestFormat;
        fileSizeValue.Text = FormatSize(metadata.FileSize);
        entriesValue.Text = metadata.ArchiveEntryCount == 0 ? "—" : metadata.ArchiveEntryCount.ToString("N0");
        permissionsList.Items.Clear();
        if (metadata.Permissions.Count == 0)
            permissionsList.Items.Add("No permissions detected");
        else
            foreach (string permission in metadata.Permissions.OrderBy(p => p, StringComparer.Ordinal)) permissionsList.Items.Add(permission);

        bool canEdit = isManifestText && !isArchive && currentPath != null;
        foreach (TextBox field in new[] { packageBox, appLabelBox, versionNameBox, versionCodeBox, minSdkBox, targetSdkBox })
            field.ReadOnly = !canEdit;
        foreach (Control control in new Control[] { packageBox, appLabelBox, versionNameBox, versionCodeBox, minSdkBox, targetSdkBox })
            control.BackColor = canEdit ? Color.White : Color.FromArgb(246, 248, 251);
    }

    private static void SetText(TextBox box, string value)
    {
        string display = value == "Not detected" ? string.Empty : value;
        if (box.Text != display) box.Text = display;
    }

    private void ApplyFieldsToManifest()
    {
        if (!isManifestText || isArchive)
        {
            MessageBox.Show(this,
                "These fields can only be applied to a decoded, plain-text AndroidManifest.xml file.\n\nOpen AndroidManifest.xml from a decoded project folder, edit the values, then save the XML. Direct binary APK rewriting is not supported.",
                "Manifest is not editable here", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(versionCodeBox.Text) && !uint.TryParse(versionCodeBox.Text.Trim(), out _))
            {
                MessageBox.Show(this, "Version code must be a non-negative whole number.", "Check version code", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                versionCodeBox.Focus();
                return;
            }
            XDocument document = XDocument.Parse(editorTextBox.Text, LoadOptions.PreserveWhitespace);
            XElement manifest = document.Root ?? throw new XmlException("Manifest root element is missing.");
            if (manifest.Name.LocalName != "manifest") throw new XmlException("The root element is not <manifest>.");

            SetOrRemove(manifest, XName.Get("package"), packageBox.Text);
            SetOrRemove(manifest, AndroidNs + "versionName", versionNameBox.Text);
            SetOrRemove(manifest, AndroidNs + "versionCode", versionCodeBox.Text);

            XElement? application = manifest.Elements().FirstOrDefault(e => e.Name.LocalName == "application");
            if (application == null)
            {
                application = new XElement(manifest.GetDefaultNamespace() + "application");
                XElement? firstAfterUsesSdk = manifest.Elements().FirstOrDefault(e => e.Name.LocalName == "uses-sdk");
                if (firstAfterUsesSdk != null) firstAfterUsesSdk.AddAfterSelf(application);
                else manifest.Add(application);
            }
            SetOrRemove(application, AndroidNs + "label", appLabelBox.Text);

            XElement? sdk = manifest.Elements().FirstOrDefault(e => e.Name.LocalName == "uses-sdk");
            if (sdk == null && (!string.IsNullOrWhiteSpace(minSdkBox.Text) || !string.IsNullOrWhiteSpace(targetSdkBox.Text)))
            {
                sdk = new XElement(manifest.GetDefaultNamespace() + "uses-sdk");
                XElement? firstUsesPermission = manifest.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("uses-permission", StringComparison.Ordinal));
                if (firstUsesPermission != null) firstUsesPermission.AddBeforeSelf(sdk);
                else application.AddBeforeSelf(sdk);
            }
            if (sdk != null)
            {
                SetOrRemove(sdk, AndroidNs + "minSdkVersion", minSdkBox.Text);
                SetOrRemove(sdk, AndroidNs + "targetSdkVersion", targetSdkBox.Text);
            }

            using var writer = new Utf8StringWriter();
            document.Save(writer, SaveOptions.DisableFormatting);
            currentText = writer.ToString();
            editorTextBox.Text = currentText;
            if (TryLoadPlainManifest(currentText, out MetadataInfo updated))
                metadata = MergeFileInfo(updated, currentPath!, metadata.FileSize);
            RefreshOverviewControls();
            SetStatus("Manifest fields applied in memory · Save to write changes");
            MessageBox.Show(this, "Fields were applied to the XML in memory. Click Save (or press Ctrl+S) to write the file.", "Fields applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException or ArgumentException)
        {
            MessageBox.Show(this, "The XML could not be updated. Check that the manifest is valid XML.\n\n" + ex.Message, "Could not apply fields", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void SetOrRemove(XElement element, XName name, string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0) element.Attribute(name)?.Remove();
        else element.SetAttributeValue(name, trimmed);
    }

    private void SaveFile()
    {
        if (currentPath == null)
        {
            SaveFileAs();
            return;
        }
        if (isArchive)
        {
            MessageBox.Show(this, "The APK / ZIP itself is read-only in this version. Save a decoded XML or text file instead; this prevents damaging an APK or silently invalidating its signature.", "Archive is read-only", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (editorTextBox.ReadOnly)
        {
            MessageBox.Show(this, "This view is read-only.", "Cannot save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            SaveTextAtomically(currentPath, editorTextBox.Text);
            currentText = editorTextBox.Text;
            if (isManifestText && TryLoadPlainManifest(currentText, out MetadataInfo parsed))
                metadata = MergeFileInfo(parsed, currentPath, new FileInfo(currentPath).Length);
            else
                metadata = MergeFileInfo(ParseGenericText(currentText, Path.GetExtension(currentPath)), currentPath, new FileInfo(currentPath).Length);
            RefreshOverviewControls();
            SetStatus("Saved · " + Path.GetFileName(currentPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, "The file could not be saved.\n\n" + ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveFileAs()
    {
        if (editorTextBox.ReadOnly || isArchive)
        {
            MessageBox.Show(this, "Open a decoded XML, JSON or text file first. APK archives are read-only.", "Save As unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "Save metadata file as",
            Filter = "XML files (*.xml)|*.xml|JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = currentPath == null ? "AndroidManifest.xml" : Path.GetFileName(currentPath),
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            SaveTextAtomically(dialog.FileName, editorTextBox.Text);
            currentPath = dialog.FileName;
            currentText = editorTextBox.Text;
            isArchive = false;
            if (TryLoadPlainManifest(currentText, out MetadataInfo parsed))
            {
                isManifestText = true;
                metadata = MergeFileInfo(parsed, currentPath, new FileInfo(currentPath).Length);
            }
            else
            {
                isManifestText = false;
                metadata = MergeFileInfo(ParseGenericText(currentText, Path.GetExtension(currentPath)), currentPath, new FileInfo(currentPath).Length);
            }
            UpdatePathLabel();
            RefreshOverviewControls();
            SetStatus("Saved copy · " + Path.GetFileName(currentPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, "The file could not be saved.\n\n" + ex.Message, "Save As failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void SaveTextAtomically(string path, string text)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath) ?? throw new IOException("The target folder could not be determined.");
        string tempPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".qme-tmp");
        string backupPath = fullPath + ".bak";
        if (File.Exists(fullPath))
        {
            if (!File.Exists(backupPath)) File.Copy(fullPath, backupPath);
            else File.Copy(fullPath, backupPath, true);
        }
        try
        {
            File.WriteAllText(tempPath, text, new UTF8Encoding(false));
            if (File.Exists(fullPath)) File.Move(tempPath, fullPath, true);
            else File.Move(tempPath, fullPath);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private void ExportReport()
    {
        if (currentPath == null)
        {
            MessageBox.Show(this, "Open a file before exporting a metadata report.", "No file selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "Export metadata report",
            Filter = "JSON report (*.json)|*.json",
            FileName = Path.GetFileNameWithoutExtension(currentPath) + "-metadata.json",
            DefaultExt = "json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var report = new
        {
            sourceFile = currentPath,
            fileSizeBytes = metadata.FileSize,
            manifestFormat = metadata.ManifestFormat,
            packageName = metadata.PackageName,
            appLabel = metadata.AppLabel,
            versionName = metadata.VersionName,
            versionCode = metadata.VersionCode,
            minSdk = metadata.MinSdk,
            targetSdk = metadata.TargetSdk,
            archiveEntryCount = metadata.ArchiveEntryCount,
            permissions = metadata.Permissions.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
            generatedAtUtc = DateTimeOffset.UtcNow
        };
        try
        {
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            SaveTextAtomically(dialog.FileName, json);
            SetStatus("Exported metadata report");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, "The report could not be exported.\n\n" + ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExtractSelectedArchiveEntry()
    {
        if (!isArchive || currentPath == null || archiveList.SelectedItems.Count == 0) return;
        string entryName = archiveList.SelectedItems[0].Tag as string ?? archiveList.SelectedItems[0].Text;
        using var dialog = new SaveFileDialog
        {
            Title = "Extract archive entry",
            FileName = Path.GetFileName(entryName),
            Filter = "All files (*.*)|*.*",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var archive = ZipFile.OpenRead(currentPath);
            ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, entryName, StringComparison.Ordinal));
            if (entry == null) throw new FileNotFoundException("The selected entry is no longer in the archive.");
            string destination = Path.GetFullPath(dialog.FileName);
            using Stream input = entry.Open();
            using FileStream output = new(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
            SetStatus("Extracted " + entryName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            MessageBox.Show(this, "The entry could not be extracted.\n\n" + ex.Message, "Extraction failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateArchiveStatus()
    {
        if (archiveList.SelectedItems.Count == 0) return;
        var item = archiveList.SelectedItems[0];
        SetStatus($"Selected: {item.Text} · {item.SubItems[1].Text}");
    }

    private void UpdatePathLabel()
    {
        pathLabel.Text = currentPath == null ? "No file open" : currentPath;
    }

    private void SetStatus(string text) => statusLabel.Text = text;

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return unit == 0 ? $"{bytes:N0} B" : $"{size:0.##} {units[unit]}";
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }

    private sealed class Win7ColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Color.FromArgb(156, 180, 205);
        public override Color MenuItemBorder => Color.FromArgb(120, 164, 208);
        public override Color MenuItemSelected => Color.FromArgb(216, 234, 251);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(236, 246, 255);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(205, 228, 249);
        public override Color ToolStripGradientBegin => Color.FromArgb(242, 247, 252);
        public override Color ToolStripGradientMiddle => Color.FromArgb(232, 241, 250);
        public override Color ToolStripGradientEnd => Color.FromArgb(222, 234, 247);
        public override Color ButtonSelectedGradientBegin => Color.FromArgb(238, 247, 255);
        public override Color ButtonSelectedGradientMiddle => Color.FromArgb(214, 235, 253);
        public override Color ButtonSelectedGradientEnd => Color.FromArgb(193, 221, 247);
        public override Color ButtonPressedGradientBegin => Color.FromArgb(189, 218, 245);
        public override Color ButtonPressedGradientMiddle => Color.FromArgb(173, 207, 240);
        public override Color ButtonPressedGradientEnd => Color.FromArgb(201, 226, 249);
    }
}
