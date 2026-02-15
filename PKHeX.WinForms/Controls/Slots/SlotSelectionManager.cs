using System;
using System.Collections.Generic;
using System.Windows.Forms;
using PKHeX.Core;

namespace PKHeX.WinForms.Controls;

/// <summary>
/// Manages the selection state of multiple slots for multi-drag operations.
/// </summary>
public sealed class SlotSelectionManager
{
    private readonly HashSet<SlotViewInfo<PictureBox>> _selectedSlots = new();
    private ISlotViewer<PictureBox>? _currentViewer;

    /// <summary>
    /// Fired when the selection state changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Adds a slot to the selection.
    /// </summary>
    /// <param name="slot">The slot to add.</param>
    /// <returns>True if the slot was added; false if it was already selected or violates constraints.</returns>
    public bool Add(SlotViewInfo<PictureBox> slot)
    {
        // Enforce single-viewer constraint: can only select slots from the same box/party
        if (_currentViewer != null && slot.View != _currentViewer)
            return false;

        if (_selectedSlots.Add(slot))
        {
            _currentViewer = slot.View;
            OnSelectionChanged();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a slot from the selection.
    /// </summary>
    /// <param name="slot">The slot to remove.</param>
    /// <returns>True if the slot was removed; false if it wasn't selected.</returns>
    public bool Remove(SlotViewInfo<PictureBox> slot)
    {
        if (_selectedSlots.Remove(slot))
        {
            if (_selectedSlots.Count == 0)
                _currentViewer = null;
            OnSelectionChanged();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all selected slots.
    /// </summary>
    public void Clear()
    {
        if (_selectedSlots.Count == 0)
            return;

        _selectedSlots.Clear();
        _currentViewer = null;
        OnSelectionChanged();
    }

    /// <summary>
    /// Checks if a slot is currently selected.
    /// </summary>
    /// <param name="slot">The slot to check.</param>
    /// <returns>True if the slot is selected.</returns>
    public bool Contains(SlotViewInfo<PictureBox> slot) => _selectedSlots.Contains(slot);

    /// <summary>
    /// Gets all currently selected slots.
    /// </summary>
    /// <returns>A read-only collection of selected slots.</returns>
    public IReadOnlyCollection<SlotViewInfo<PictureBox>> GetAll() => _selectedSlots;

    /// <summary>
    /// Gets the number of currently selected slots.
    /// </summary>
    public int Count => _selectedSlots.Count;

    private void OnSelectionChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);
}
