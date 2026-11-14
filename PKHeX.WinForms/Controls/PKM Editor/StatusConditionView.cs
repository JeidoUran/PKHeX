using System;
using System.Drawing;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.WinForms.Controls;

public partial class StatusConditionView : UserControl
{
    private PKM? pk;
    private bool Loading;
    private readonly ToolTip Hover = new()
    {
        AutoPopDelay = 5000,
        InitialDelay = 200,
        ReshowDelay = 500,
        ShowAlways = true,
    };

    public StatusConditionView()
    {
        InitializeComponent();
        PB_Status.MouseHover += (_, _) => PB_Status.Cursor = Cursors.Hand;
        PB_Status.SizeMode = PictureBoxSizeMode.CenterImage;
        PB_Status.AccessibleName = "Status condition";
        PB_Status.Paint += (_, e) =>
        {
            ControlPaint.DrawBorder(e.Graphics, PB_Status.ClientRectangle, SystemColors.ControlDark, ButtonBorderStyle.Solid);
            if (PB_Status.Image is null)
                TextRenderer.DrawText(e.Graphics, Main.CurrentLanguage == "fr" ? "Statut…" : "Status…", Font,
                    PB_Status.ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
    }

    public void LoadPKM(PKM entity)
    {
        pk = entity;
        LoadStoredValues();
    }

    public void LoadStoredValues()
    {
        if (pk is null || Loading)
            return;
        Loading = true;
        if (!pk.PartyStatsPresent)
            ClearStatus();
        else if (pk.Stat_HPCurrent == 0)
            SetFaint();
        else
            SetStatus(pk.Status_Condition, pk.Format);
        Loading = false;
    }

    private void SetFaint()
    {
        PB_Status.Image = Drawing.PokeSprite.Properties.Resources.sickfaint;
        Hover.RemoveAll();
    }

    private void ClearStatus()
    {
        PB_Status.Image = null;
        Hover.RemoveAll();
    }

    private void SetStatus(int value, int generation)
    {
        if (generation <= 4)
        {
            StatusCondition status = (StatusCondition)(value & 0xFF);
            PB_Status.Image = status.GetStatusSprite();

            var text = WinFormsTranslator.TranslateEnum(status, Main.CurrentLanguage);
            Hover.SetToolTip(PB_Status, $"Status Condition: {text}");
        }
        else
        {
            StatusType status = (StatusType)(value & 0xFF);
            PB_Status.Image = status.GetStatusSprite();

            var text = WinFormsTranslator.TranslateEnum(status, Main.CurrentLanguage);
            Hover.SetToolTip(PB_Status, $"Status Condition: {text}");
        }
    }

    private void PB_Status_Click(object sender, EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(pk);
        int generation = pk.Format;
        using var form = new StatusBrowser(generation);
        form.LoadList(pk);
        form.ShowDialog();
        if (!form.WasChosen)
            return;
        var current = pk.Status_Condition;
        current &= ~0xFF;
        current |= generation <= 4 ? (int)form.Choice : (int)form.Choice.GetStatusType();
        pk.Status_Condition = current;
        LoadStoredValues();
    }
}
