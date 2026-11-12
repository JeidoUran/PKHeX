using System.Reflection;
using PKHeX.Core;
using PKHeX.WinForms;
using PKHeX.WinForms.Controls;
using MainWindow = PKHeX.WinForms.Main;

internal static class ConsultationTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static T Get<T>(object owner, string name) => (T)(owner is MainWindow ? typeof(MainWindow) : owner.GetType()).GetField(name, Flags)!.GetValue(owner)!;
    private static object? Call(object owner, string name, params object[] args) => (owner is MainWindow ? typeof(MainWindow) : owner.GetType()).GetMethod(name, Flags)!.Invoke(owner, args);

    internal static void Run(MainWindow main, PKMEditor editor, SAVEditor save, SAV_Database database, List<SlotCache> loaded,
        string output, Action<bool, string> check)
    {
        check(main.FormBorderStyle == FormBorderStyle.FixedSingle && !main.MaximizeBox, "main window retains its original fixed-window behavior");
        check(save.M.Boxes.Count == 1 && typeof(MainWindow).GetMethod("InitializeWorkspace", Flags) is null,
            "adaptive panes and embedded multi-box behavior are absent");
        var tabs = Get<TabControl>(editor, "TC_Editor");
        var status = Get<StatusConditionView>(editor, "StatusView");
        check(status.Top >= tabs.Bottom && status.Bottom <= editor.Height, "visible status control fits below HOME without overlap");
        var search = Get<EntitySearchControl>(database, "UC_EntitySearch");
        var format = Get<ComboBox>(search, "CB_Format");
        var home = ((IEnumerable<ComboItem>)format.DataSource!).Single(c => c.Text == "HOME (PH4/EH4)");
        search.ResetComboBoxSelections();
        var ordinary = new SlotCache(new SlotInfoFileSingle("synthetic-consultation.pk9"), new PK9 { Species = 25, Version = GameVersion.VL });
        loaded.Add(ordinary);
        format.SelectedValue = home.Value;
        check(((IEnumerable<SlotCache>)Call(database, "SearchDatabase")!).Count() == loaded.Count - 1,
            "HOME source filter survives independently of adaptive layout");
        format.SelectedValue = (int)EntityContext.None;
        check(((IEnumerable<SlotCache>)Call(database, "SearchDatabase")!).Contains(ordinary), "ordinary source formats remain searchable");
        loaded.Remove(ordinary);

        var fixtureRoot = Path.Combine(Environment.CurrentDirectory, "Tests", "PKHeX.Core.Tests", "Legality", "Legal");
        var fixtures = new[]
        {
            Path.Combine(fixtureRoot, "Generation 3", "Misc", "327 - Spinda - 88283DD1BB49 Method 3.pk3"),
            Path.Combine(fixtureRoot, "Generation 4", "0113 - CHANSEY - D4E98650DFCD swarm1 pressure.pk4"),
        };
        var summary = Get<EncounterSummaryView>(editor, "EncounterSummary");
        var summaryText = Get<TextBox>(summary, "Summary");
        check(summaryText.ReadOnly && summary.Parent == Get<TabControl>(editor, "Hidden_TC").TabPages.Cast<TabPage>().Single(p => p.Name == "Hidden_Met"),
            "encounter summary is read-only and belongs to the original Met tab");
        foreach (var path in fixtures)
        {
            var data = File.ReadAllBytes(path);
            var originalFile = data.ToArray();
            var pk = EntityFormat.GetFromBytes(data)!;
            var before = pk.Data.ToArray();
            var analysis = new LegalityAnalysis(pk);
            check(analysis.Parsed, "upstream classic-era fixture has a parsed analysis: " + pk.Format);
            foreach (var language in new[] { "en", "fr" })
            {
                summary.InvalidateAnalysis(); summary.ShowAnalysis(analysis, language);
                var lines = new List<string>();
                LegalityFormatting.AddEncounterInfo(LegalityLocalizationContext.Create(analysis, language), lines);
                var expected = string.Join(Environment.NewLine, lines);
                check(summaryText.Text == expected && analysis.Report(language, true).EndsWith(expected, StringComparison.Ordinal),
                    "Met displays the exact existing verbose encounter/RNG block: " + pk.Format + "/" + language);
                if (language == "en")
                    check(summaryText.Text.Contains("Frame:", StringComparison.Ordinal), "classic-era fixture includes reconstructed frame information: " + pk.Format);
            }
            check(before.SequenceEqual(pk.Data.ToArray()) && originalFile.SequenceEqual(File.ReadAllBytes(path)), "formatting leaves fixture entity and file bytes unchanged");
            check(summaryText.Text.Contains(EncounterDisplayLocalization.Get("fr").OriginSeed, StringComparison.Ordinal), "classic-era summary includes the existing RNG origin seed");
        }
        summary.ShowAnalysis(null, "fr");
        check(!summaryText.Text.Contains("Frame", StringComparison.Ordinal), "unavailable analysis replaces previous reconstruction");

        // Exercise the actual editor's lazy refresh, on a synthetic blank game save.
        Call(main, "LoadBlankSaveFile", GameVersion.FR);
        var fixture = EntityFormat.GetFromBytes(File.ReadAllBytes(fixtures[0]))!;
        editor.PopulateFields(fixture, false);
        tabs.SelectedIndex = 0;
        editor.UpdateLegality();
        check(summaryText.TextLength == 0, "hidden Met does not eagerly calculate the encounter summary");
        tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(p => p.Name == "Tab_Met");
        check(summaryText.TextLength > 0, "selecting Met populates the current analysis");
        var analysisInEditor = Get<LegalityAnalysis>(editor, "Legality");
        var expectedEditor = new List<string>();
        LegalityFormatting.AddEncounterInfo(LegalityLocalizationContext.Create(analysisInEditor, MainWindow.CurrentLanguage), expectedEditor);
        check(summaryText.Text == string.Join(Environment.NewLine, expectedEditor), "editor uses its existing legality analysis rather than a separate reconstruction");
        // Render the same editor geometry used by the fixed main window, without activating it.
        main.PerformLayout(); editor.PerformLayout();
        using (var image = new Bitmap(editor.Width, editor.Height))
        { editor.DrawToBitmap(image, editor.ClientRectangle); image.Save(Path.Combine(output, "met-summary-gba.png")); }
        check(summary.Bounds.Bottom <= summary.Parent!.ClientSize.Height && Get<FlowLayoutPanel>(editor, "FLP_Met").Bottom <= summary.Top,
            "Met fields and summary have separate non-overlapping areas");
        editor.PopulateFields(new PK3(), false);
        check(!summaryText.Text.Contains(EncounterDisplayLocalization.Get(MainWindow.CurrentLanguage).OriginSeed, StringComparison.Ordinal),
            "loading an empty entity clears the previous seed/frame display");
        Call(main, "LoadBlankSaveFile", GameVersion.D);
        editor.PopulateFields(EntityFormat.GetFromBytes(File.ReadAllBytes(fixtures[1]))!, false);
        tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(p => p.Name == "Tab_Met");
        using (var image = new Bitmap(editor.Width, editor.Height))
        { editor.DrawToBitmap(image, editor.ClientRectangle); image.Save(Path.Combine(output, "met-summary-ds.png")); }
        Call(main, "LoadBlankSaveFile", GameVersion.VL);
    }
}
