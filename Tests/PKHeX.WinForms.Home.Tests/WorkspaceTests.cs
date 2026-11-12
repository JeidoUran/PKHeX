using System.Collections;
using System.Reflection;
using PKHeX.Core;
using PKHeX.WinForms;
using PKHeX.WinForms.Controls;
using MainWindow = PKHeX.WinForms.Main;

internal static class WorkspaceTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static T Get<T>(object value, string name) => (T)value.GetType().GetField(name, Flags)!.GetValue(value)!;
    private static object? Call(object value, string name, params object[] args) => (value is MainWindow ? typeof(MainWindow) : value.GetType()).GetMethod(name, Flags)!.Invoke(value, args);
    private static List<BoxEditor> Boxes(SAVEditor editor) => editor.M.Boxes.Where(b => b.FindForm() == editor.FindForm()).ToList();

    internal static void Run(MainWindow main, PKMEditor editor, SAVEditor save, SAV_Database database,
        List<SlotCache> loaded, string output, Action<bool, string> check)
    {
        var search = Get<EntitySearchControl>(database, "UC_EntitySearch");
        var format = Get<ComboBox>(search, "CB_Format");
        var comparator = Get<ComboBox>(search, "CB_FormatComparator");
        var formats = ((IEnumerable<ComboItem>)format.DataSource!).ToArray();
        var homeChoice = formats.Single(f => f.Text == "HOME (PH4/EH4)");
        search.ResetComboBoxSelections();
        var ordinary = new SlotCache(new SlotInfoFileSingle("synthetic-workspace.pk9"), new PK9 { Species = 25, Version = GameVersion.VL });
        loaded.Add(ordinary);
        format.SelectedValue = homeChoice.Value;
        var matches = ((IEnumerable<SlotCache>)Call(database, "SearchDatabase")!).ToList();
        check(matches.Count == loaded.Count - 1 && matches.All(s => s.Source is SlotInfoFileHome), "HOME format filters source containers independently of their game projection");
        check(!comparator.Enabled && search.CreateSearchSettings("").Context == EntityContext.None, "HOME format cannot acquire an artificial generation or numeric comparison");
        format.SelectedValue = (int)EntityContext.Gen9;
        matches = ((IEnumerable<SlotCache>)Call(database, "SearchDatabase")!).ToList();
        check(comparator.Enabled && matches.Contains(ordinary) && matches.All(s => s.Entity.Context == EntityContext.Gen9), "ordinary Gen9 filtering remains a game-context filter");
        format.SelectedValue = (int)EntityContext.None;
        check(((IEnumerable<SlotCache>)Call(database, "SearchDatabase")!).Count() == loaded.Count, "Any restores all source formats");
        loaded.Remove(ordinary);

        check(main.FormBorderStyle == FormBorderStyle.Sizable && main.MaximizeBox, "main window supports resizing and maximize");
        var compact = main.ClientSize;
        var minimum = main.MinimumSize;
        var tabs = Get<TabControl>(editor, "TC_Editor");
        var status = Get<StatusConditionView>(editor, "StatusView");
        check(status.Top >= tabs.Bottom && status.Bottom <= editor.Height, "status control is below HOME and inside the minimum window");
        var pcBefore = save.SAV.GetPCBinary();
        var originalNickname = Get<TextBox>(editor, "TB_Nickname").Text;
        Get<TextBox>(editor, "TB_Nickname").Text = "LayoutTest";
        main.ClientSize = new Size(1920, 1024); Application.DoEvents();
        var views = Boxes(save);
        Console.WriteLine($"Workspace: main={main.ClientSize}, editor={editor.Size}, save={save.Size}, box={save.Box.Size}, grid={save.Box.BoxPokeGrid.Size}, views={views.Count}, registered={save.M.Boxes.Count}");
        check(views.Count >= 4 && views.All(b => ReferenceEquals(b.M, save.M)), "large window embeds multiple boxes with the existing shared slot manager");
        var body = Get<TabControl>(save, "tabBoxMulti").TabPages.Cast<TabPage>().Single(t => t.Name == "Tab_Box");
        foreach (var name in new[] { "B_PopoutBox", "B_SearchBox" })
        {
            var button = Get<Button>(save, name);
            check(button.Visible && body.ClientRectangle.Contains(button.Bounds) && body.Controls.GetChildIndex(button) < body.Controls.GetChildIndex(save.Box),
                "primary toolbar button remains visible above the grid: " + name);
        }
        foreach (var view in views)
        {
            var position = body.PointToClient(view.PointToScreen(Point.Empty));
            check(new Rectangle(Point.Empty, body.ClientSize).Contains(new Rectangle(position, view.Size)), "embedded box fits without clipping");
        }
        var extra = ((IEnumerable)Get<object>(editor, "ExtraEditorPanes")).Cast<object>().ToList();
        check(extra.Count >= 2 && Get<Dictionary<TabPage, Panel>>(editor, "EditorContents").Count == 7, "large editor arranges original tab contents into multiple panes");
        check(Get<TextBox>(editor, "TB_Nickname").Text == "LayoutTest", "pending editor text survives expansion");
        // Select an already-visible secondary tab: the two panes swap, without duplicate fields.
        var firstChoice = Get<ComboBox>(extra[0], "Choice");
        firstChoice.SelectedIndex = firstChoice.Items.Count - 1;
        var visibleContents = Get<Dictionary<TabPage, Panel>>(editor, "EditorContents").Values.Where(p => p.Parent is not TabPage).ToArray();
        check(visibleContents.Distinct().Count() == extra.Count, "changing a pane selector keeps exactly one instance of each visible content panel");
        tabs.SelectedIndex = tabs.TabCount - 1;
        check(Get<TextBox>(editor, "TB_Nickname").Text == "LayoutTest", "primary tab navigation preserves pending fields while secondary panes rearrange");

        var primary = save.Box;
        primary.CurrentBox = 7;
        check(views.Skip(1).Select((v, i) => v.CurrentBox == (8 + i) % save.SAV.BoxCount).All(x => x), "secondary boxes follow primary navigation in order");
        views[1].CurrentBox = 25;
        primary.CurrentBox = 8;
        check(views[1].CurrentBox == 25 && views[2].CurrentBox == 10, "manual selection pins only that secondary box");
        var follow = views[1].Parent!.Controls.OfType<CheckBox>().Single();
        follow.Checked = true;
        check(views[1].CurrentBox == 9, "Follow explicitly restores linked navigation");
        views[1].CurrentBox = 25;
        main.ClientSize = compact; Application.DoEvents();
        check(Boxes(save).Count == 1 && save.M.Boxes.Count == 1, "shrinking removes secondary subscriptions and returns to a single box");
        check(Get<TextBox>(editor, "TB_Nickname").Text == "LayoutTest", "pending editor text survives collapse");
        main.ClientSize = new Size(1920, 1024); Application.DoEvents();
        views = Boxes(save);
        check(views[1].CurrentBox == 25, "pinned selection survives a shrink-expand cycle");
        primary.CurrentBox = save.SAV.BoxCount - 1;
        var secondFollow = views[1].Parent!.Controls.OfType<CheckBox>().Single(); secondFollow.Checked = true;
        check(views[1].CurrentBox == 0 && views[2].CurrentBox == 1, "linked navigation wraps at the last box");
        check(pcBefore.SequenceEqual(save.SAV.GetPCBinary()), "resizing and navigation leave all Pokémon box bytes unchanged");
        Get<TextBox>(editor, "TB_Nickname").Text = originalNickname;
        tabs.SelectedIndex = 0;
        SaveImage(main, Path.Combine(output, "workspace-large.png"));
        main.ClientSize = compact; Application.DoEvents();
        tabs.SelectedIndex = tabs.TabCount - 1;
        SaveImage(main, Path.Combine(output, "workspace-compact.png"));
        var synthetic = new PK9 { Species = 25, Version = GameVersion.VL, Nickname = "Synthetic", CurrentLevel = 10 };
        main.ClientSize = new Size(1920, 1024); Application.DoEvents();
        views = Boxes(save);
        var slot = new SlotInfoBox(views[1].CurrentBox, 0, save.SAV);
        save.EditEnv.Slots.Set(slot, synthetic, SlotTouchType.Set);
        check(save.SAV.GetBoxSlotAtIndex(slot.Box, 0).Species == 25 && views[1].SlotPictureBoxes[0].Image is not null, "normal slot notifications update embedded views");
        save.EditEnv.Slots.Undo();
        check(save.SAV.GetBoxSlotAtIndex(slot.Box, 0).Species == 0, "undo still operates on embedded box edits");
        save.EditEnv.Slots.Redo();
        check(save.SAV.GetBoxSlotAtIndex(slot.Box, 0).Species == 25, "redo still operates on embedded box edits");

        var oldViews = views.Skip(1).ToArray();
        foreach (var version in new[] { GameVersion.BD, GameVersion.R, GameVersion.C, GameVersion.RD, GameVersion.VL })
        {
            Call(main, "LoadBlankSaveFile", version); Application.DoEvents();
            check(Boxes(save).All(v => ReferenceEquals(v.SAV, save.SAV)) && Boxes(save).Count <= save.SAV.BoxCount,
                "changing game rebuilds views against the current save: " + version);
            var sources = Get<Dictionary<TabPage, Panel>>(editor, "EditorContents");
            check(sources.Values.Distinct().Count() == sources.Count, "editor contents stay unique across tab availability changes: " + version);
        }
        check(oldViews.All(v => v.IsDisposed), "previous-save embedded views are disposed");
        main.ClientSize = compact; Application.DoEvents();
        check(main.MinimumSize == minimum, "returning to compact size retains the protected minimum");
    }

    private static void SaveImage(MainWindow main, string path)
    {
        using var image = new Bitmap(main.Width, main.Height);
        main.DrawToBitmap(image, new Rectangle(Point.Empty, main.Size));
        image.Save(path);
    }
}
