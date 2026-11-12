using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PKHeX.WinForms.Controls;

public partial class SAVEditor
{
    private sealed record BoxPane(Panel Panel, BoxEditor View, CheckBox Follow);
    private readonly List<BoxPane> ExtraBoxes = [];
    private readonly Dictionary<int, int> PinnedBoxes = [];
    private bool ArrangingBoxes;

    private void InitializeAdaptiveBoxes()
    {
        Box.CurrentBoxChanged += (_, _) => FollowMainBox();
        Disposed += (_, _) => ClearAdaptiveBoxes();
    }

    private void ClearAdaptiveBoxes()
    {
        foreach (var pane in ExtraBoxes.ToArray()) RemoveBoxPane(pane);
        ExtraBoxes.Clear();
        PinnedBoxes.Clear();
    }

    private void RemoveBoxPane(BoxPane pane)
    {
        M.Boxes.Remove(pane.View);
        if (EditEnv is not null) EditEnv.Slots.Publisher.Unsubscribe(pane.View);
        Tab_Box.Controls.Remove(pane.Panel);
        pane.Panel.Dispose();
    }

    private int ScaleWorkspace(int value) => (int)Math.Round(value * DeviceDpi / 96d);

    private void LayoutAdaptiveBoxes()
    {
        if (ArrangingBoxes || !FieldsLoaded || !SAV.HasBox || SAV.BoxCount == 0 || Box.BoxSlotCount == 0) return;
        ArrangingBoxes = true;
        Tab_Box.SuspendLayout();
        try
        {
            int gap = ScaleWorkspace(8), header = ScaleWorkspace(24);
            int width = Math.Max(Box.BoxPokeGrid.Width, Box.CB_BoxSelect.Width + Box.B_BoxLeft.Width + Box.B_BoxRight.Width + 2 * B_PopoutBox.Width) + gap;
            int height = Box.BoxPokeGrid.Bottom + header + gap;
            int columns = Math.Max(1, (Tab_Box.ClientSize.Width - gap) / (width + gap));
            int rows = Math.Max(1, (Tab_Box.ClientSize.Height - gap) / (height + gap));
            int count = Math.Min(SAV.BoxCount, columns * rows);
            while (ExtraBoxes.Count >= count)
            {
                var pane = ExtraBoxes[^1];
                int index = ExtraBoxes.Count;
                if (!pane.Follow.Checked) PinnedBoxes[index] = pane.View.CurrentBox;
                else PinnedBoxes.Remove(index);
                RemoveBoxPane(pane);
                ExtraBoxes.RemoveAt(ExtraBoxes.Count - 1);
            }
            while (ExtraBoxes.Count < count - 1) ExtraBoxes.Add(CreateBoxPane(ExtraBoxes.Count + 1));
            int left = Math.Max(gap / 2, (Tab_Box.ClientSize.Width - columns * (width + gap) + gap) / 2);
            Box.AutoSize = false;
            Box.SetBounds(left, header, width, height - header);
            Box.RecenterControls();
            for (int i = 0; i < ExtraBoxes.Count; i++)
            {
                var pane = ExtraBoxes[i];
                int cell = i + 1;
                pane.Panel.SetBounds(left + (cell % columns) * (width + gap), (cell / columns) * (height + gap), width, height);
                pane.Follow.Text = Main.CurrentLanguage == "fr" ? "Suivre la première boîte" : "Follow the first box";
                pane.Follow.SetBounds(0, 0, width, header);
                pane.View.SetBounds(0, header, width, height - header);
                pane.View.RecenterControls();
            }
            BoxSearchAlignButton();
            BoxPopoutAlignButton();
            B_SearchBox.BringToFront(); B_PopoutBox.BringToFront();
            FollowMainBoxCore();
        }
        finally { Tab_Box.ResumeLayout(); ArrangingBoxes = false; }
    }

    private BoxPane CreateBoxPane(int index)
    {
        var panel = new Panel { Name = $"P_EmbeddedBox{index}", Margin = Padding.Empty };
        var view = new BoxEditor { Name = $"EmbeddedBox{index}", AutoSize = false, CanSetCurrentBox = false };
        view.Setup(M);
        view.InitializeFromSAV(SAV);
        view.ApplySearchFilter(_searchFilter, false);
        foreach (var pb in view.SlotPictureBoxes) pb.ContextMenuStrip = menu.mnuVSD;
        var follow = new CheckBox { Name = $"CHK_FollowBox{index}", Checked = !PinnedBoxes.ContainsKey(index) };
        panel.Controls.Add(view); panel.Controls.Add(follow);
        Tab_Box.Controls.Add(panel);
        if (Application.IsDarkModeEnabled)
        {
            WinFormsTranslator.ReformatDark(view.B_BoxLeft); WinFormsTranslator.ReformatDark(view.B_BoxRight);
            WinFormsTranslator.ReformatDark(view.CB_BoxSelect); WinFormsTranslator.ReformatDark(follow);
        }
        if (PinnedBoxes.TryGetValue(index, out int pinned)) view.CurrentBox = Math.Clamp(pinned, 0, SAV.BoxCount - 1);
        EditEnv.Slots.Publisher.Subscribe(view);
        view.ResetSlots();
        view.CurrentBoxChanged += (_, _) =>
        {
            if (!ArrangingBoxes) follow.Checked = false;
        };
        follow.CheckedChanged += (_, _) =>
        {
            if (!follow.Checked) PinnedBoxes[index] = view.CurrentBox;
            else { PinnedBoxes.Remove(index); FollowMainBox(); }
        };
        // Wheel navigation here applies to this view, without changing the primary box.
        view.MouseWheel += (_, e) => view.CurrentBox = e.Delta > 0 ? view.Editor.MoveLeft() : view.Editor.MoveRight();
        return new BoxPane(panel, view, follow);
    }

    private void FollowMainBox()
    {
        if (ArrangingBoxes || !SAV.HasBox) return;
        ArrangingBoxes = true;
        try { FollowMainBoxCore(); }
        finally { ArrangingBoxes = false; }
    }

    private void FollowMainBoxCore()
    {
        for (int i = 0; i < ExtraBoxes.Count; i++)
        {
            var pane = ExtraBoxes[i];
            if (!pane.Follow.Checked) continue;
            int target = (Box.CurrentBox + i + 1) % SAV.BoxCount;
            if (pane.View.CurrentBox != target) pane.View.CurrentBox = target;
        }
    }
}
