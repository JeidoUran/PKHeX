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
        // The existing six-tab rail is fixed-height; make room for the seventh entry.
        TC_Editor.Height += TC_Editor.ItemSize.Width;
    }

    public void PopulateHomeFields(PKM gameEntity, HomeFile source, string path)
    {
        PopulateFields(gameEntity);
        HomeViewer.SetSource(source, path);
    }
}
