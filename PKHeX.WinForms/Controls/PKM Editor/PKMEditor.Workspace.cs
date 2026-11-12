using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PKHeX.WinForms.Controls;

public sealed partial class PKMEditor
{
    private sealed record TabChoice(TabPage Page, string Text)
    {
        public override string ToString() => Text;
    }
    private sealed class EditorPane
    {
        public readonly Panel Panel = new();
        public readonly ComboBox Choice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
        public readonly Panel Body = new() { Dock = DockStyle.Fill };
        public TabPage? Page;
    }

    private readonly Dictionary<TabPage, Panel> EditorContents = [];
    private readonly List<EditorPane> ExtraEditorPanes = [];
    private bool ArrangingEditor;
    private bool EditorWorkspaceReady;

    private int EditorScale(int value) => (int)Math.Round(value * DeviceDpi / 96d);
    public int MinimumWorkspaceHeight => Math.Max(EditorScale(410), TC_Editor.Top + TC_Editor.TabCount * TC_Editor.ItemSize.Width + EditorScale(56));
    public int AdditionalPaneWidth => EditorScale(312);

    private void InitializeEditorWorkspace()
    {
        EditorWorkspaceReady = true;
        SizeChanged += (_, _) => ArrangeEditorPanes();
        TC_Editor.ControlAdded += (_, _) => ArrangeEditorPanes();
        TC_Editor.ControlRemoved += (_, _) => ArrangeEditorPanes();
        ArrangeEditorPanes();
    }

    private void ArrangeEditorPanes()
    {
        if (!EditorWorkspaceReady || ArrangingEditor || Hidden_TC.SelectedTab is not { } primary) return;
        ArrangingEditor = true;
        SuspendLayout();
        try
        {
            int gap = EditorScale(8), top = 0;
            TC_Editor.Height = TC_Editor.TabCount * TC_Editor.ItemSize.Width + EditorScale(5);
            StatusView.SetBounds(EditorScale(2), TC_Editor.Bottom + gap / 2, TC_Editor.Width - EditorScale(4), EditorScale(40));
            int left = TC_Editor.Right, width = Math.Max(1, ClientSize.Width - left);
            int height = Math.Max(1, ClientSize.Height - top);
            int columns = Math.Max(1, (width + gap) / (EditorScale(304) + gap));
            int rows = Math.Max(1, (height + gap) / (EditorScale(410) + gap));
            int count = Math.Min(Hidden_TC.TabCount, columns * rows);
            int cellWidth = (width - gap * (columns - 1)) / columns;
            int usedRows = (count + columns - 1) / columns;
            int cellHeight = (height - gap * (usedRows - 1)) / usedRows;
            var pages = Hidden_TC.TabPages.Cast<TabPage>().ToArray();
            if (count > 1)
            {
                // Reparent each existing content panel; never create a second editable copy.
                foreach (var page in pages)
                {
                    if (EditorContents.ContainsKey(page)) continue;
                    var contents = new Panel { Name = "Workspace_" + page.Name, Size = page.ClientSize, Dock = DockStyle.Fill };
                    var children = page.Controls.Cast<Control>().ToArray();
                    foreach (var child in children) contents.Controls.Add(child);
                    page.Controls.Add(contents);
                    EditorContents.Add(page, contents);
                }
            }
            // Return hidden or newly-primary pages to their original TabPage.
            foreach (var (page, content) in EditorContents)
                if (content.Parent != page) page.Controls.Add(content);
            Hidden_TC.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Hidden_TC.SetBounds(left, top - EditorScale(3), cellWidth, cellHeight + EditorScale(3));
            var used = new HashSet<TabPage> { primary };
            while (ExtraEditorPanes.Count < count - 1) ExtraEditorPanes.Add(CreateEditorPane(ExtraEditorPanes.Count + 1));
            for (int i = 0; i < ExtraEditorPanes.Count; i++)
            {
                var pane = ExtraEditorPanes[i];
                pane.Panel.Visible = i < count - 1;
                if (i >= count - 1) continue;
                var selected = pane.Page;
                if (selected is null || !pages.Contains(selected) || used.Contains(selected)) selected = pages.First(p => !used.Contains(p));
                used.Add(selected); pane.Page = selected;
                int cell = i + 1;
                pane.Panel.SetBounds(left + (cell % columns) * (cellWidth + gap), top + (cell / columns) * (cellHeight + gap), cellWidth, cellHeight);
                pane.Choice.Items.Clear();
                foreach (var page in pages.Where(p => p != primary))
                    pane.Choice.Items.Add(new TabChoice(page, TC_Editor.TabPages[Hidden_TC.TabPages.IndexOf(page)].Text));
                pane.Choice.SelectedItem = pane.Choice.Items.Cast<TabChoice>().Single(c => c.Page == selected);
                pane.Body.Controls.Add(EditorContents[selected]);
            }
        }
        finally { ResumeLayout(); ArrangingEditor = false; }
    }

    private EditorPane CreateEditorPane(int index)
    {
        var pane = new EditorPane();
        pane.Panel.Name = $"P_EditorPane{index}";
        pane.Choice.Name = $"CB_EditorPane{index}";
        pane.Panel.Controls.Add(pane.Body); pane.Panel.Controls.Add(pane.Choice);
        Controls.Add(pane.Panel);
        if (Application.IsDarkModeEnabled) WinFormsTranslator.ReformatDark(pane.Choice);
        pane.Choice.SelectedIndexChanged += (_, _) =>
        {
            if (ArrangingEditor || pane.Choice.SelectedItem is not TabChoice chosen) return;
            var other = ExtraEditorPanes.FirstOrDefault(p => p != pane && p.Page == chosen.Page);
            if (other is not null) other.Page = pane.Page;
            pane.Page = chosen.Page;
            ArrangeEditorPanes();
        };
        return pane;
    }
}
