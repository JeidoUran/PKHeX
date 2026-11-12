using System;
using System.Collections.Generic;
using System.Windows.Forms;
using PKHeX.Core;

namespace PKHeX.WinForms.Controls;

/// <summary>Read-only encounter section from the existing verbose legality formatter.</summary>
public sealed class EncounterSummaryView : UserControl
{
    private readonly Label Heading = new() { Name = "L_EncounterSummary", Dock = DockStyle.Top, Height = 20 };
    private readonly Label Explanation = new() { Name = "L_EncounterInference", Dock = DockStyle.Bottom, Height = 32 };
    private readonly TextBox Summary = new()
    {
        Name = "TB_EncounterSummary", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Vertical, WordWrap = true, BorderStyle = BorderStyle.FixedSingle,
    };
    private LegalityAnalysis? DisplayedAnalysis;
    private string DisplayedLanguage = string.Empty;

    public EncounterSummaryView()
    {
        Name = "EncounterSummary";
        Padding = new Padding(4);
        Controls.Add(Summary); Controls.Add(Heading); Controls.Add(Explanation);
    }

    public void InvalidateAnalysis()
    {
        DisplayedAnalysis = null;
        Summary.Clear(); // Never leave the previous Pokémon's reconstruction visible.
    }

    public void ShowAnalysis(LegalityAnalysis? analysis, string language)
    {
        if (ReferenceEquals(DisplayedAnalysis, analysis) && DisplayedLanguage == language && Summary.TextLength != 0) return;
        bool french = language == "fr";
        Heading.Text = french ? "Analyse de rencontre / RNG" : "Encounter / RNG analysis";
        Explanation.Text = french
            ? "Origine compatible déduite ; ne prouve pas une histoire unique."
            : "Inferred compatible origin; does not prove a unique history.";
        if (analysis is not { Parsed: true } || analysis.Info.Entity.Species == 0)
            Summary.Text = french ? "Analyse indisponible pour ce Pokémon." : "Analysis unavailable for this Pokémon.";
        else
        {
            var localizer = LegalityLocalizationContext.Create(analysis, language);
            var lines = new List<string>();
            LegalityFormatting.AddEncounterInfo(localizer, lines);
            Summary.Text = string.Join(Environment.NewLine, lines);
        }
        Summary.Select(0, 0);
        DisplayedAnalysis = analysis;
        DisplayedLanguage = language;
    }
}
