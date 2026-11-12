using System.Windows.Forms;
using PKHeX.Core;

namespace PKHeX.WinForms.Controls;

public sealed partial class PKMEditor
{
    private HomeDataViewer HomeViewer = null!;

    private void InitializeHomeTab()
    {
        HomeViewer = new HomeDataViewer { Dock = DockStyle.Fill };
        var contents = new TabPage { Name = "Hidden_HOME", Text = "HOME", Padding = new Padding(0) };
        contents.Controls.Add(HomeViewer);
        Hidden_TC.TabPages.Add(contents);
        TC_Editor.TabPages.Add(new TabPage { Name = "Tab_HOME", Text = "HOME" });
        Layout += (_, _) => ArrangeHomeStatus();
        ArrangeHomeStatus();
    }

    private void ArrangeHomeStatus()
    {
        int Scale(int value) => (int)System.Math.Round(value * DeviceDpi / 96d);
        TC_Editor.Height = TC_Editor.TabCount * TC_Editor.ItemSize.Width + Scale(5);
        StatusView.SetBounds(Scale(2), TC_Editor.Bottom + Scale(4), TC_Editor.Width - Scale(4), Scale(40));
    }

    public void PopulateHomeFields(PKM gameEntity, HomeFile source, string path)
    {
        PopulateFields(gameEntity);
        HomeViewer.SetSource(source, path);
    }
}
