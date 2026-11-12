using System;
using System.Windows.Forms;

namespace PKHeX.WinForms.Controls;

public sealed partial class PKMEditor
{
    private EncounterSummaryView EncounterSummary = null!;

    private void InitializeEncounterSummary()
    {
        EncounterSummary = new EncounterSummaryView { Dock = DockStyle.Bottom, Height = (int)Math.Round(192 * DeviceDpi / 96d) };
        Hidden_Met.Controls.Add(EncounterSummary);
        FLP_Met.AutoScroll = true;
        Hidden_Met.VisibleChanged += (_, _) => RefreshEncounterSummary();
    }

    private void RefreshEncounterSummary()
    {
        // Formatting is lazy: reuse the current analysis only when Met is selected.
        if (EncounterSummary is not null && Hidden_TC.SelectedTab == Hidden_Met)
            EncounterSummary.ShowAnalysis(Legality, Main.CurrentLanguage);
    }
}
