using System;
using System.Windows.Forms;
using System.Windows.Forms.Automation;

namespace PKHeX.WinForms.Controls;

/// <summary>
/// PictureBox control that can be focused and selected.
/// </summary>
/// <remarks>Draws a focus rectangle, and can be tabbed between, raising events for screen readers.</remarks>
public class SelectablePictureBox : PictureBox
{
    public SelectablePictureBox()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
        TabStop = true;
    }

    public static int FocusBorderDeflate { get; set; }

    /// <summary>
    /// Gets or sets whether this slot is currently selected for multi-drag operations.
    /// </summary>
    public bool IsSelected { get; set; }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        base.OnMouseDown(e);
    }
    protected override void OnEnter(EventArgs e)
    {
        Invalidate();
        base.OnEnter(e);
        AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.Other,
            AutomationNotificationProcessing.All, AccessibleDescription ?? AccessibleName ?? string.Empty);
    }
    protected override void OnLeave(EventArgs e)
    {
        Invalidate();
        base.OnLeave(e);
    }
    protected override void OnPaint(PaintEventArgs pe)
    {
        base.OnPaint(pe);

        var rc = ClientRectangle;
        rc.Inflate(-FocusBorderDeflate, -FocusBorderDeflate);

        // Blue border for selection (persistent)
        if (IsSelected)
        {
            using var pen = new System.Drawing.Pen(System.Drawing.Color.DodgerBlue, 3);
            pe.Graphics.DrawRectangle(pen, rc);
        }
        // Dotted border for keyboard focus (transient)
        else if (Focused)
        {
            ControlPaint.DrawFocusRectangle(pe.Graphics, rc);
        }
    }
}
