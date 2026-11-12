using System;
using System.Drawing;

namespace PKHeX.WinForms;

public partial class Main
{
    private void InitializeConsultationLayout()
    {
        // Keep the original fixed-width/fixed-window interface. Reserve one static
        // row of space for the read-only Met summary, not an adaptive workspace.
        ClientSize = new Size(ClientSize.Width, ClientSize.Height + (int)Math.Round(192 * DeviceDpi / 96d));
    }
}
