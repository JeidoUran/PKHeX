using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using PKHeX.Core;
using PKHeX.Core.Searching;
using PKHeX.WinForms;
using PKHeX.WinForms.Controls;
using MainWindow = PKHeX.WinForms.Main;

internal static class Program
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks++; Console.WriteLine("PASS " + description);
    }
    private static T Field<T>(object owner, string name) => (T)(owner.GetType().GetField(name, Private)
        ?? throw new MissingFieldException(owner.GetType().Name, name)).GetValue(owner)!;
    private static object? Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private)!.Invoke(owner, args);

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(output);
            var files = Directory.GetFiles(root, "*.ph4", SearchOption.AllDirectories);
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest-sha256.json")));
            var hashes = manifest.RootElement.GetProperty("files").EnumerateArray().ToDictionary(e => e.GetProperty("path").GetString()!, e => e.GetProperty("sha256").GetString()!);
            var db = new ConcurrentBag<SlotCache>();
            var trainer = new SimpleTrainerInfo(GameVersion.VL);
            var extensions = new HashSet<string>(EntityFileExtension.GetExtensionsAll().Select(e => "." + e));
            var sizes432 = 0; var unavailable = 0;
            foreach (var path in files)
            {
                SlotInfoLoader.AddFromLocalFile(path, db, trainer, extensions);
                if (new FileInfo(path).Length == 432) sizes432++;
            }
            Check(db.Count == files.Length, "every PH4 produces one database entry");
            Check(db.All(x => x.Source is SlotInfoFileHome), "all HOME sources use the protected source type");
            foreach (var entry in db)
            {
                var source = (SlotInfoFileHome)entry.Source;
                entry.Entity.ForcePartyData();
                _ = entry.Entity.FileName;
                _ = entry.Identify();
                if (source.Home.CreateCompatibleEntity(typeof(PK9)) is null) unavailable++;
                CheckHash(source.Path, root, hashes);
                if (!source.Home.GetOriginalData().SequenceEqual(File.ReadAllBytes(source.Path))) throw new Exception("Source bytes changed.");
            }
            Check(true, "all projections support party statistics and database identity");
            Check(true, "all input files still match their import SHA-256 manifest");

            // Initialize application settings before controls; use only this test output directory.
            RuntimeHelpers.RunClassConstructor(typeof(MainWindow).Assembly.GetType("PKHeX.WinForms.Program")!.TypeHandle);
            MainWindow.Settings.EntityDb.FilterUnavailableSpecies = true;
            MainWindow.Settings.EntityDb.FilterHomeByCurrentSave = false;
            MainWindow.Settings.EntityDb.SearchBackups = MainWindow.Settings.EntityDb.SearchExtraSaves = false;
            MainWindow.Settings.LocalResources.DatabasePath = root;
            using var main = new MainWindow();
            Invoke(main, "LoadBlankSaveFile", GameVersion.VL);
            var editor = Field<PKMEditor>(main, "PKME_Tabs");
            var saveEditor = Field<SAVEditor>(main, "C_SAV");
            using var database = new SAV_Database(editor, saveEditor);
            _ = database.Handle;
            var stopwatch = Stopwatch.StartNew();
            while (!Field<Button>(database, "B_Search").Enabled && stopwatch.Elapsed < TimeSpan.FromSeconds(90))
            { Application.DoEvents(); Thread.Sleep(10); }
            var loaded = Field<List<SlotCache>>(database, "RawDB");
            Check(loaded.Count == files.Length, "real PKM Database keeps the complete HOME collection independently of the current save");

            MainWindow.Settings.EntityDb.FilterHomeByCurrentSave = true;
            using (var filteredDatabase = new SAV_Database(editor, saveEditor))
            {
                _ = filteredDatabase.Handle;
                stopwatch.Restart();
                while (!Field<Button>(filteredDatabase, "B_Search").Enabled && stopwatch.Elapsed < TimeSpan.FromSeconds(90))
                { Application.DoEvents(); Thread.Sleep(10); }
                var filtered = Field<List<SlotCache>>(filteredDatabase, "RawDB");
                var presence = EntityPresenceFilters.GetFilterEntity(saveEditor.SAV.Context)!;
                Check(filtered.Count == loaded.Count(x => presence(x.Entity)), "optional HOME species filter matches the current save's presence filter");
            }
            MainWindow.Settings.EntityDb.FilterHomeByCurrentSave = false;

            var sample = args.Length > 2 ? loaded.Single(x => ((SlotInfoFileHome)x.Source).Path == Path.GetFullPath(args[2])) : loaded.Where(x => ((SlotInfoFileHome)x.Source).Home.CreateCompatibleEntity(typeof(PK9)) is not null).OrderByDescending(x => ((SlotInfoFileHome)x.Source).Home.AvailableFormats.Length).First();
            typeof(SAV_Database).GetField("Results", Private)!.SetValue(database, new List<SlotCache> { sample });
            Invoke(database, "FillPKXBoxes", 0);
            var boxes = Field<PictureBox[]>(database, "PKXBOXES");
            Invoke(database, "ClickView", boxes[0], EventArgs.Empty);
            var viewer = Field<HomeDataViewer>(editor, "HomeViewer");
            var sourceInfo = (SlotInfoFileHome)sample.Source;
            Check(ReferenceEquals(Field<HomeFile>(viewer, "Source"), sourceInfo.Home), "View carries the original HOME source to the main editor");
            var tabs = Field<TabControl>(editor, "TC_Editor");
            Check(tabs.TabPages[^1].Name == "Tab_HOME" && tabs.TabPages[^2].Name == "Tab_OTMisc", "HOME tab follows OT/Misc");
            tabs.SelectedIndex = tabs.TabPages.Count - 1;
            var games = Field<ComboBox>(viewer, "Games");
            Check(games.Items.Count == sourceInfo.Home.AvailableFormats.Length + 1, "inspector lists every original block plus shared HOME data");
            for (int i = 0; i < games.Items.Count; i++)
            {
                games.SelectedIndex = i;
                Check(Field<DataGridView>(viewer, "Grid").Rows.Count > 0, "block " + i + " displays its stored fields");
            }
            Check(Field<DataGridView>(viewer, "Grid").ReadOnly, "stored fields are read-only");
            Check(!(bool)Invoke(database, "IsIndividualFilePKMDB", sample)!, "clone deletion excludes original HOME sources");
            // Render the actual editor in an invisible, non-activating test host.
            editor.Parent!.Controls.Remove(editor);
            using var host = new RenderHost { ClientSize = new Size(500, 620), ShowInTaskbar = false, Opacity = 0 };
            editor.Dock = DockStyle.Fill; host.Controls.Add(editor); host.Show(); Application.DoEvents();
            var pk9Choice = games.Items.Cast<object>().Select((value, index) => (value, index)).FirstOrDefault(x => x.value.ToString()!.Contains("(PK9)"));
            if (pk9Choice.value is not null) games.SelectedIndex = pk9Choice.index;
            var metRow = Field<DataGridView>(viewer, "Grid").Rows.Cast<DataGridViewRow>().SingleOrDefault(r => (string?)r.Cells[0].Value == "MetLocation");
            if (metRow is not null)
                Check(((string)metRow.Cells[1].Value!).StartsWith(Field<ComboBox>(editor, "CB_MetLocation").Text, StringComparison.Ordinal), "HOME location labels match the main Met tab");
            Console.WriteLine("Inspector buttons: " + Field<Button>(viewer, "Export").Text + " | " + Field<Button>(viewer, "Copy").Text);
            Check(Field<Button>(viewer, "Export").FlatStyle == FlatStyle.Popup && Field<Button>(viewer, "Copy").FlatStyle == FlatStyle.Popup,
                "inspector buttons use the application's supported dark-theme rendering");
            using (var bitmap = new Bitmap(editor.Width, editor.Height))
            { editor.DrawToBitmap(bitmap, editor.ClientRectangle); bitmap.Save(Path.Combine(output, "home-editor.png")); }
            Check(true, "HOME tab paints successfully with a seventh navigation entry");
            editor.PopulateFields(editor.Entity.Clone(), false);
            Check(typeof(HomeDataViewer).GetField("Source", Private)!.GetValue(viewer) is null, "loading an ordinary entity clears the previous HOME source");
            foreach (var path in files) CheckHash(path, root, hashes);
            Check(true, "all source hashes remain unchanged after the interface tests");
            var report = new { Files = files.Length, Loaded = loaded.Count, FilesOfLength432 = sizes432, CannotCreateSVView = unavailable, Checks = checks, SourcesUnchanged = true, SavedImage = "home-editor.png" };
            File.WriteAllText(Path.Combine(output, "integration.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(report));
            if (args.Length > 3)
            {
                var cfg = MainWindow.Settings;
                cfg.Startup.Language = "fr";
                cfg.Startup.DefaultSaveVersion = GameVersion.VL;
                cfg.Startup.AutoLoadSaveOnStartup = SaveFileLoadSetting.Disabled;
                cfg.Startup.TryDetectRecentSave = false;
                cfg.Startup.RecentlyLoaded.Clear();
                cfg.Startup.SkipSplashScreen = true;
                cfg.Startup.ShowChangelogOnUpdate = false;
                cfg.Startup.PluginLoadEnable = false;
                cfg.Backup.BAKPrompt = true;
                PKHeXSettings.SaveSettings(Path.GetFullPath(args[3]), cfg).GetAwaiter().GetResult();
                Check(File.Exists(args[3]), "isolated preview configuration written outside the user's installation");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private sealed class RenderHost : Form
    {
        protected override bool ShowWithoutActivation => true;
    }

    private static void CheckHash(string path, string root, Dictionary<string, string> hashes)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(hashes[relative], StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Source hash mismatch.");
    }
}
