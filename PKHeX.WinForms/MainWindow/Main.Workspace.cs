using System;
using System.Drawing;
using System.Windows.Forms;

namespace PKHeX.WinForms;

public partial class Main
{
    private bool ArrangingWorkspace;
    private bool AutomaticWorkspace = true;
    private int CompactEditorWidth;
    private int CompactSaveWidth;

    private void InitializeWorkspace()
    {
        CompactEditorWidth = splitContainer1.Panel1.Width;
        CompactSaveWidth = splitContainer1.Panel2.Width;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        splitContainer1.FixedPanel = FixedPanel.Panel1;
        splitContainer2.FixedPanel = FixedPanel.Panel1;
        splitContainer1.Panel2MinSize = CompactSaveWidth;
        splitContainer1.SplitterWidth = Math.Max(4, (int)Math.Round(4 * DeviceDpi / 96d));
        // Leave a real, visible status button below all seven navigation entries.
        int needed = PKME_Tabs.MinimumWorkspaceHeight - PKME_Tabs.Height;
        ClientSize = new Size(ClientSize.Width + splitContainer1.SplitterWidth, ClientSize.Height + Math.Max(0, needed));
        MinimumSize = Size;
        var automatic = new ToolStripMenuItem
        {
            Name = "Menu_AutomaticWorkspace", CheckOnClick = true, Checked = true,
            Text = CurrentLanguage == "fr" ? "Répartition automatique des volets" : "Automatic pane layout",
        };
        automatic.CheckedChanged += (_, _) => { AutomaticWorkspace = automatic.Checked; ArrangeWorkspace(); };
        Menu_Options.DropDownItems.Add(automatic);
        splitContainer1.SplitterMoving += (_, _) => { AutomaticWorkspace = false; automatic.Checked = false; };
        Resize += (_, _) => ArrangeWorkspace();
        ArrangeWorkspace();
    }

    private void ArrangeWorkspace()
    {
        if (ArrangingWorkspace || WindowState == FormWindowState.Minimized) return;
        ArrangingWorkspace = true;
        try
        {
            if (AutomaticWorkspace)
            {
                int spare = splitContainer1.ClientSize.Width - CompactEditorWidth - CompactSaveWidth - splitContainer1.SplitterWidth;
                int extra = PKME_Tabs.AdditionalPaneWidth;
                int desired = CompactEditorWidth + (spare >= extra ? extra : 0);
                splitContainer1.SplitterDistance = Math.Clamp(desired, splitContainer1.Panel1MinSize,
                    Math.Max(splitContainer1.Panel1MinSize, splitContainer1.Width - CompactSaveWidth - splitContainer1.SplitterWidth));
            }
        }
        finally { ArrangingWorkspace = false; }
    }
}
