using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PKHeX.Core;

namespace PKHeX.WinForms.Controls;

/// <summary>Neutral, read-only inspection of the original HOME blocks.</summary>
public sealed class HomeDataViewer : UserControl
{
    private readonly Label SourceLabel = new() { Name = "L_HomeSource", AutoSize = false, Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly ComboBox Games = new() { Name = "CB_HomeBlock", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox Filter = new() { Name = "TB_HomeFilter", Dock = DockStyle.Fill, PlaceholderText = "Filter fields…" };
    private readonly Button Export = new() { Name = "B_HomeExport", Text = "Export game file…", FlatStyle = FlatStyle.Popup, Dock = DockStyle.Fill };
    private readonly Button Copy = new() { Name = "B_HomeCopy", Text = "Copy fields", FlatStyle = FlatStyle.Popup, Dock = DockStyle.Fill };
    private readonly DataGridView Grid = new()
    {
        Name = "DGV_HomeFields", Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
        BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.None,
    };
    private HomeFile? Source;
    private string SourcePath = string.Empty;
    private bool French => GameInfo.CurrentLanguage == "fr";

    private sealed record BlockChoice(HomeGameDataFormat Format, string Name)
    {
        public override string ToString() => Name;
    }

    public HomeDataViewer()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(6) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.Controls.Add(SourceLabel, 0, 0);
        layout.Controls.Add(Games, 0, 1);
        layout.Controls.Add(Filter, 0, 2);
        layout.Controls.Add(Grid, 0, 3);
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        buttons.Controls.Add(Export, 0, 0); buttons.Controls.Add(Copy, 1, 0);
        layout.Controls.Add(buttons, 0, 4);
        Grid.Columns.Add("Field", "Field"); Grid.Columns.Add("Value", "Stored value");
        Controls.Add(layout);
        Games.SelectedIndexChanged += (_, _) => ShowBlock();
        Filter.TextChanged += (_, _) => ShowBlock();
        Export.Click += (_, _) => ExportBlock();
        Copy.Click += (_, _) => CopyFields();
        SetSource(null, string.Empty);
    }

    public void SetSource(HomeFile? source, string path)
    {
        Source = source; SourcePath = path;
        Grid.Columns[0].HeaderText = French ? "Champ" : "Field";
        Grid.Columns[1].HeaderText = French ? "Valeur stockée" : "Stored value";
        Filter.PlaceholderText = French ? "Filtrer les champs…" : "Filter fields…";
        Export.Text = French ? "Exporter vers un jeu…" : "Export game file…";
        Copy.Text = French ? "Copier" : "Copy fields";
        SourceLabel.Text = source is null
            ? (French ? "Ouvrez un fichier HOME depuis la base ou le menu Fichier." : "Open a HOME file from the database or File menu.")
            : $"{Path.GetFileName(path)}\n" + (French ? "Source HOME d’origine, en lecture seule. Les autres onglets affichent une copie de jeu." : "Original HOME source, read only. Other tabs display a game-format copy.");
        Games.Items.Clear(); Grid.Rows.Clear();
        if (source is null) { Export.Enabled = Copy.Enabled = false; return; }
        Games.Items.Add(new BlockChoice(HomeGameDataFormat.None, French ? "HOME — données communes" : "HOME — shared data"));
        foreach (var format in source.AvailableFormats)
            Games.Items.Add(new BlockChoice(format, GetGameName(format)));
        Games.SelectedIndex = 0;
        Copy.Enabled = true;
    }

    private string GetGameName(HomeGameDataFormat format)
    {
        string Version(GameVersion version) => GameInfo.GetVersionName(version);
        var name = format switch
        {
            HomeGameDataFormat.PK8 => $"{Version(GameVersion.SW)} / {Version(GameVersion.SH)}",
            HomeGameDataFormat.PB7 => $"{Version(GameVersion.GP)} / {Version(GameVersion.GE)}",
            HomeGameDataFormat.PB8 => $"{Version(GameVersion.BD)} / {Version(GameVersion.SP)}",
            HomeGameDataFormat.PA8 => Version(GameVersion.PLA),
            HomeGameDataFormat.PK9 => $"{Version(GameVersion.SL)} / {Version(GameVersion.VL)}",
            HomeGameDataFormat.PA9 => Version(GameVersion.ZA),
            _ => "Pokémon Champions",
        };
        return $"{name} ({format})";
    }

    private void ShowBlock()
    {
        Grid.Rows.Clear();
        if (Source is null || Games.SelectedItem is not BlockChoice choice) return;
        var block = Source.GetBlock(choice.Format);
        var gameEntity = Source.CreateGameEntity(choice.Format);
        Export.Enabled = choice.Format is not (HomeGameDataFormat.None or HomeGameDataFormat.PC9);
        if (block is null) return;
        foreach (var property in block.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).OrderBy(p => p.Name))
        {
            var type = property.PropertyType;
            if (!property.CanRead || property.GetIndexParameters().Length != 0 || type.IsByRefLike || property.Name == "SerializedSize") continue;
            if (!(type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid))) continue;
            var value = property.GetValue(block);
            AddField(property.Name, FormatValue(property.Name, value, gameEntity));
        }
        if (gameEntity is ITechRecord records)
        {
            var moves = records.Permit.RecordPermitIndexes;
            var count = Math.Min(records.Permit.RecordCountUsed, moves.Length);
            for (int i = 0; i < count; i++)
            {
                if (records.GetMoveRecordFlag(i))
                    AddField($"TM/TR [{i:000}]", FormatValue("Move1", moves[i]));
            }
        }
        // Keep bitfields and unmodeled bytes accessible without claiming guessed meanings.
        AddField(French ? "Octets du bloc (hex)" : "Block bytes (hex)", Convert.ToHexString(Source.GetBlockBytes(choice.Format)));
    }

    private static string FormatValue(string name, object? value, PKM? game = null)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (value is null || value.GetType().IsEnum) return text;
        if (game is not null && name is "MetLocation" or "EggLocation" && value is ushort location)
        {
            var place = GameInfo.GetLocationList(game.Version, game.Context, egg: name == "EggLocation")
                .FirstOrDefault(item => item.Value == location)?.Text;
            return string.IsNullOrWhiteSpace(place) ? text : $"{place} ({text})";
        }
        if (name == "Timestamp" && value is ulong timestamp && timestamp is > 0 and <= 253402300799)
            return $"{DateTimeOffset.FromUnixTimeSeconds((long)timestamp):yyyy-MM-dd HH:mm:ss} UTC ({text})";
        string[]? names = name is "Move1" or "Move2" or "Move3" or "Move4" or "RelearnMove1" or "RelearnMove2" or "RelearnMove3" or "RelearnMove4"
            ? GameInfo.Strings.movelist : name == "Ability" ? GameInfo.Strings.abilitylist : name == "Ball" ? GameInfo.Strings.balllist : null;
        if (names is not null && int.TryParse(text, out var index) && (uint)index < names.Length)
            return $"{names[index]} ({text})";
        return text;
    }

    private void AddField(string name, string value)
    {
        var search = Filter.Text.Trim();
        if (search.Length != 0 && !name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            && !value.Contains(search, StringComparison.CurrentCultureIgnoreCase)) return;
        Grid.Rows.Add(name, value);
    }

    private void CopyFields()
    {
        var text = string.Join(Environment.NewLine, Grid.Rows.Cast<DataGridViewRow>().Select(r => $"{r.Cells[0].Value}\t{r.Cells[1].Value}"));
        if (text.Length != 0) Clipboard.SetText(text);
    }

    private void ExportBlock()
    {
        if (Source is null || Games.SelectedItem is not BlockChoice choice) return;
        var entity = Source.CreateGameEntity(choice.Format);
        if (entity is null) return;
        using var dialog = new SaveFileDialog { Filter = $"{entity.Extension} (*.{entity.Extension})|*.{entity.Extension}", FileName = entity.FileName, OverwritePrompt = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (HomeFile.IsHomeExtension(Path.GetExtension(dialog.FileName))
            || (!string.IsNullOrEmpty(SourcePath) && string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(SourcePath), StringComparison.OrdinalIgnoreCase)))
        {
            WinFormsUtil.Alert(French ? "Choisissez un fichier de jeu distinct de la source HOME." : "Choose a game-format file separate from the HOME source.");
            return;
        }
        // Only a separate game-format file is written. The original HOME container is immutable.
        entity.RefreshChecksum();
        var data = new byte[entity.SIZE_PARTY];
        entity.WriteDecryptedDataParty(data);
        try { File.WriteAllBytes(dialog.FileName, data); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { WinFormsUtil.Alert(ex.Message); }
    }

    public static void ShowWindow(IWin32Window owner, HomeFile source, string path)
    {
        var window = new Form { Text = $"HOME — {Path.GetFileName(path)}", StartPosition = FormStartPosition.CenterParent, Size = new Size(620, 680), MinimumSize = new Size(420, 400) };
        var viewer = new HomeDataViewer { Dock = DockStyle.Fill };
        viewer.SetSource(source, path);
        viewer.SourceLabel.Text = Path.GetFileName(path) + "\n" + (viewer.French
            ? "Vue indisponible pour le jeu courant. Consultation de la source HOME en lecture seule."
            : "View unavailable for the current game. Inspecting the read-only HOME source.");
        window.Controls.Add(viewer); window.Show(owner);
    }
}
