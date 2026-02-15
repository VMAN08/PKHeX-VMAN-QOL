using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.WinForms.Controls;

/// <summary>
/// Orchestrates the movement of slots within the GUI.
/// </summary>
public sealed class SlotChangeManager(SAVEditor se) : IDisposable
{
    public readonly SAVEditor SE = se;
    public readonly SlotTrackerImage LastSlot = new();
    public readonly DragManager Drag = new();
    public readonly SlotSelectionManager Selection = new();
    public SaveDataEditor<PictureBox> Env { get; set; } = null!;

    public readonly List<BoxEditor> Boxes = [];
    public readonly SlotHoverHandler Hover = new();

    public void Reset()
    {
        Drag.Initialize();
        LastSlot.Reset();
        Selection.Clear();
    }

    public void MouseEnter(object? sender, EventArgs e)
    {
        if (sender is not PictureBox pb)
            return;
        bool dataPresent = pb.Image is not null;
        if (dataPresent)
            Hover.Start(pb, LastSlot);
        pb.Cursor = dataPresent ? Cursors.Hand : Cursors.Default;
    }

    public void MouseLeave(object? sender, EventArgs e)
    {
        Hover.Stop();
    }

    public void MouseClick(object? sender, MouseEventArgs e)
    {
        if (sender is null)
            return;
        if (Drag.Info.IsDragDropInProgress)
            return;

        // Ctrl+Click toggles selection (don't load Pokémon)
        if ((Control.ModifierKeys & Keys.Control) == Keys.Control && sender is PictureBox pb)
        {
            if (pb.Image is null) return;
            var slotInfo = GetSlotInfo(pb);
            if (Selection.Contains(slotInfo))
                Selection.Remove(slotInfo);
            else
                Selection.Add(slotInfo);
            return;
        }

        // Normal click: clear selection and load Pokémon
        Selection.Clear();
        // Call OmniClick with Keys.Control to force viewing (since OmniClick does nothing with no modifiers)
        SE.menu.OmniClick(sender, e, Keys.Control);
    }

    public void MouseUp(object? sender, MouseEventArgs e)
    {
        if (sender is null)
            return;
        if (e.Button == MouseButtons.Left)
            Drag.Info.IsLeftMouseDown = false;
        Drag.Info.Source = null;
    }

    public void MouseDown(object? sender, MouseEventArgs e)
    {
        if (sender is null)
            return;
        if (e.Button == MouseButtons.Left)
        {
            Drag.Info.IsLeftMouseDown = true;
            Drag.MouseDownPosition = Cursor.Position;

            // Clear selection if starting drag without Ctrl on non-selected slot
            if (Control.ModifierKeys != Keys.Control && sender is PictureBox pb)
            {
                var slotInfo = GetSlotInfo(pb);
                if (!Selection.Contains(slotInfo))
                    Selection.Clear();
            }
        }
    }

    public void QueryContinueDrag(object? sender, QueryContinueDragEventArgs e)
    {
        if (sender is null)
            return;
        if (e.Action != DragAction.Cancel && e.Action != DragAction.Drop)
            return;
        Drag.Info.IsLeftMouseDown = false;
        Drag.Info.IsDragDropInProgress = false;
    }

    public void DragEnter(object? sender, DragEventArgs e)
    {
        if (sender is null)
            return;
        if ((e.AllowedEffect & DragDropEffects.Copy) != 0) // external file
            e.Effect = DragDropEffects.Copy;
        else if (e.Data is not null) // within
            e.Effect = DragDropEffects.Move;

        if (Drag.Info.IsDragDropInProgress)
            Drag.SetCursor(((Control)sender).FindForm(), Drag.Info.Cursor);
    }

    private static SlotViewInfo<T> GetSlotInfo<T>(T pb) where T : Control
    {
        if (!WinFormsUtil.TryFindFirstControlOfType<ISlotViewer<T>>(pb, out var view))
            ArgumentNullException.ThrowIfNull(view);
        var src = view.GetSlotData(pb);
        return new SlotViewInfo<T>(src, view);
    }

    public void MouseMove(object? sender, MouseEventArgs e)
    {
        if (!Drag.CanStartDrag)
        {
            Hover.UpdateMousePosition(e.Location);
            return;
        }
        if (sender is not PictureBox pb)
            return;

        // Abort if there is no Pokémon in the given slot.
        if (pb.Image is null)
            return;
        bool encrypt = Control.ModifierKeys == Keys.Control;
        HandleMovePKM(pb, encrypt);
    }

    public void DragDrop(object? sender, DragEventArgs e)
    {
        if (sender is not PictureBox pb)
            return;
        var info = GetSlotInfo(pb);
        if (!info.CanWriteTo() || Drag.Info.Source?.CanWriteTo() == false)
        {
            SystemSounds.Asterisk.Play();
            e.Effect = DragDropEffects.Copy;
            Drag.Reset();
            return;
        }

        var mod = SlotUtil.GetDropModifier();
        Drag.Info.Destination = info;
        HandleDropPKM(pb, e, mod);
    }

    private void HandleMovePKM(PictureBox pb, bool encrypt)
    {
        // Create temporary PKM file(s) to perform a drag drop operation.

        // Set flag to prevent re-entering.
        Drag.Info.IsDragDropInProgress = true;

        // Prepare Data
        Drag.Info.Source = GetSlotInfo(pb);
        Drag.Info.Destination = null;

        // Determine which slots to drag
        var slotsToDrag = new List<SlotViewInfo<PictureBox>>();
        if (Selection.Contains(Drag.Info.Source) && Selection.Count > 1)
        {
            // Multi-drag: drag all selected slots
            slotsToDrag.AddRange(Selection.GetAll());
        }
        else
        {
            // Single drag: clear any selection and drag just this slot
            Selection.Clear();
            slotsToDrag.Add(Drag.Info.Source);
        }

        // Create multiple temp files
        var newfiles = CreateDragDropPKMs(slotsToDrag, pb, encrypt, out bool external);

        // drop finished, clean up
        Drag.Info.Source = null;
        Drag.Reset();
        Drag.ResetCursor(pb.FindForm());

        // Browser apps need time to load data since the file isn't moved to a location on the user's local storage.
        // Tested 10ms -> too quick, 100ms was fine. 500ms should be safe?
        // Keep it to 20 seconds; Discord upload only stores the file path until you click Upload.
        int delay = external ? 20_000 : 0;
        foreach (var file in newfiles)
            DeleteAsync(file, delay);
        if (Drag.Info.IsDragParty)
            SE.SetParty();
    }

    private async void DeleteAsync(string path, int delay)
    {
        try
        {
            await Task.Delay(delay).ConfigureAwait(true);
            if (!File.Exists(path) || Drag.Info.CurrentPath == path)
                return;

            try { File.Delete(path); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }
        catch
        {
            // Ignore.
        }
    }

    private string CreateDragDropPKM(PictureBox pb, bool encrypt, out bool external)
    {
        // Make File
        var pk = Drag.Info.Source!.ReadCurrent();
        string newfile = FileUtil.GetPKMTempFileName(pk, encrypt);
        try
        {
            var data = encrypt ? pk.EncryptedPartyData : pk.DecryptedPartyData;
            external = TryMakeDragDropPKM(pb, data, newfile);
        }
        // Tons of things can happen with drag & drop; don't try to handle things, just indicate failure.
        catch (Exception x)
        {
            WinFormsUtil.Error("Drag && Drop Error", x);
            external = false;
        }

        return newfile;
    }

    private bool TryMakeDragDropPKM(PictureBox pb, ReadOnlySpan<byte> data, string newfile)
    {
        var img = pb.Image as Bitmap;
        ArgumentNullException.ThrowIfNull(img);
        File.WriteAllBytes(newfile, data);

        Drag.SetCursor(pb.FindForm(), new Cursor(img.GetHicon()));
        Hover.Stop();
        pb.Image = null;
        pb.BackgroundImage = SpriteUtil.Spriter.Drag;

        // Thread Blocks on DoDragDrop
        Drag.Info.CurrentPath = newfile;
        var result = pb.DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { newfile }), DragDropEffects.Copy);
        var external = Drag.Info.Destination is null || result != DragDropEffects.Link;
        if (external || Drag.Info.IsDragSameLocation) // not dropped to another box slot, restore img
        {
            pb.Image = img;
            pb.BackgroundImage = LastSlot.OriginalBackground;
            Drag.ResetCursor(pb.FindForm());
            return external;
        }

        if (result == DragDropEffects.Copy) // viewed in tabs or cloned
        {
            if (Drag.Info.Destination is null) // apply 'view' highlight
                Env.Slots.Get(Drag.Info.Source!.Slot);
            return false;
        }
        return true;
    }

    private List<string> CreateDragDropPKMs(List<SlotViewInfo<PictureBox>> slots, PictureBox pb, bool encrypt, out bool external)
    {
        var files = new List<string>();
        external = false;

        try
        {
            // Store original images for each slot
            var originalImages = new Dictionary<PictureBox, Image?>();

            // Create temp file for each slot
            foreach (var slot in slots)
            {
                var pk = slot.ReadCurrent();
                string newfile = FileUtil.GetPKMTempFileName(pk, encrypt);
                var data = encrypt ? pk.EncryptedPartyData : pk.DecryptedPartyData;
                File.WriteAllBytes(newfile, data);
                files.Add(newfile);

                // Store original image
                var slotPb = slot.View.SlotPictureBoxes[slot.Slot.Slot];
                originalImages[slotPb] = slotPb.Image;
            }

            // Use first slot's image for cursor
            if (slots.Count > 0 && pb.Image is Bitmap img)
            {
                Drag.SetCursor(pb.FindForm(), new Cursor(img.GetHicon()));
                Hover.Stop();

                // Clear images for all selected slots
                foreach (var slot in slots)
                {
                    var slotPb = slot.View.SlotPictureBoxes[slot.Slot.Slot];
                    slotPb.Image = null;
                    slotPb.BackgroundImage = SpriteUtil.Spriter.Drag;
                }

                // Perform drag operation with MULTIPLE files
                Drag.Info.CurrentPath = files[0];
                var result = pb.DoDragDrop(new DataObject(DataFormats.FileDrop, files.ToArray()), DragDropEffects.Copy);
                external = Drag.Info.Destination is null || result != DragDropEffects.Link;

                if (external || Drag.Info.IsDragSameLocation)
                {
                    // Restore original images
                    foreach (var kvp in originalImages)
                    {
                        kvp.Key.Image = kvp.Value as Image;
                        kvp.Key.BackgroundImage = LastSlot.OriginalBackground;
                    }
                    Drag.ResetCursor(pb.FindForm());
                }
            }
        }
        catch (Exception x)
        {
            WinFormsUtil.Error("Drag && Drop Error", x);
            external = false;
        }

        return files;
    }

    private void HandleDropPKM(PictureBox pb, DragEventArgs? e, DropModifier mod)
    {
        if (e?.Data?.GetData(DataFormats.FileDrop) is not string[] {Length: not 0} files)
        {
            Drag.Reset();
            Selection.Clear();
            return;
        }

        if (Directory.Exists(files[0])) // folder
        {
            SE.LoadBoxes(out _, files[0]);
            Drag.Reset();
            Selection.Clear();
            return;
        }

        e.Effect = mod == DropModifier.Clone || mod == DropModifier.CloneAndOverwrite ? DragDropEffects.Copy : DragDropEffects.Link;

        // file
        if (Drag.Info.IsDragSameLocation)
        {
            e.Effect = DragDropEffects.Link;
            Selection.Clear();
            return;
        }

        var dest = Drag.Info.Destination;

        if (Drag.Info.Source is null) // external source
        {
            bool badDest = !dest!.CanWriteTo();
            if (!TryLoadFiles(files, e, badDest))
                WinFormsUtil.Alert(MessageStrings.MsgSaveSlotBadData);
        }
        else
        {
            // Handle multi-file drop
            if (files.Length > 1)
            {
                if (!TrySetMultiplePKMDestination(pb, files, mod))
                    WinFormsUtil.Alert(MessageStrings.MsgSaveSlotEmpty);
            }
            else if (!TrySetPKMDestination(pb, mod))
            {
                WinFormsUtil.Alert(MessageStrings.MsgSaveSlotEmpty);
            }
        }
        Drag.Reset();
        Selection.Clear(); // Always clear selection after drop
    }

    /// <summary>
    /// Tries to load the input <see cref="files"/>
    /// </summary>
    /// <param name="files">Files to load</param>
    /// <param name="e">Args</param>
    /// <param name="badDest">Destination slot disallows eggs/blanks</param>
    /// <returns>True if loaded</returns>
    private bool TryLoadFiles(ReadOnlySpan<string> files, DragEventArgs e, bool badDest)
    {
        if (files.Length == 0)
            return false;

        var sav = Drag.Info.Destination!.View.SAV;
        var path = files[0];
        var temp = FileUtil.GetSingleFromPath(path, sav);
        if (temp is null)
        {
            Drag.RequestDD(this, e); // pass through
            return true; // treat as handled
        }

        var pk = EntityConverter.ConvertToType(temp, sav.PKMType, out var result);
        if (pk is null)
        {
            var c = result.GetDisplayString(temp, sav.PKMType);
            WinFormsUtil.Error(c);
            Debug.WriteLine(c);
            return false;
        }

        if (badDest && (pk.Species == 0 || pk.IsEgg))
            return false;

        if (sav is ILangDeviantSave il && !EntityConverter.IsCompatibleGB(temp, il.Japanese, pk.Japanese))
        {
            var str = EntityConverterResult.IncompatibleLanguageGB.GetIncompatibleGBMessage(pk, il.Japanese);
            WinFormsUtil.Error(str);
            Debug.WriteLine(str);
            return false;
        }

        var errata = sav.EvaluateCompatibility(pk);
        if (errata.Count > 0)
        {
            string concat = string.Join(Environment.NewLine, errata);
            if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, concat, MessageStrings.MsgContinue))
            {
                Debug.WriteLine(result.GetDisplayString(temp, sav.PKMType));
                Debug.WriteLine(concat);
                return false;
            }
        }

        Env.Slots.Set(Drag.Info.Destination!.Slot, pk);
        Debug.WriteLine(result.GetDisplayString(temp, sav.PKMType));
        return true;
    }

    private bool TrySetPKMDestination(PictureBox pb, DropModifier mod)
    {
        var info = Drag.Info;
        var pk = info.Source!.ReadCurrent();
        var msg = Drag.Info.Destination!.CanWriteTo(pk);
        if (msg != WriteBlockedMessage.None)
            return false;

        if (Drag.Info.Source is not null)
            TrySetPKMSource(mod);

        // Copy from temp to destination slot.
        var type = info.IsDragSwap ? SlotTouchType.Swap : SlotTouchType.Set;
        Env.Slots.Set(info.Destination!.Slot, pk, type);
        Drag.ResetCursor(pb.FindForm());
        return true;
    }

    private bool TrySetPKMSource(DropModifier mod)
    {
        var info = Drag.Info;
        var dest = info.Destination;
        if (dest is null || mod == DropModifier.Clone || mod == DropModifier.CloneAndOverwrite)
            return false;

        if (dest.IsEmpty() || mod == DropModifier.Overwrite)
        {
            Env.Slots.Delete(info.Source!.Slot);
            return true;
        }

        var type = info.IsDragSwap ? SlotTouchType.Swap : SlotTouchType.Set;
        var pk = dest.ReadCurrent();
        Env.Slots.Set(Drag.Info.Source!.Slot, pk, type);
        return true;
    }

    private bool TrySetMultiplePKMDestination(PictureBox pb, string[] files, DropModifier mod)
    {
        var info = Drag.Info;
        var destSlot = info.Destination!.Slot;
        var destView = info.Destination.View;

        // Calculate starting position
        int startSlot = destSlot.Slot;
        int boxSize = destView.SAV.BoxSlotCount;

        // Space validation
        if (destSlot is SlotInfoBox destBox)
        {
            int slotsInBox = boxSize - startSlot;
            if (files.Length > slotsInBox && destView.SAV.BoxCount == destBox.Box + 1)
            {
                WinFormsUtil.Alert($"Not enough space. Need {files.Length} slots, only {slotsInBox} available.");
                return false;
            }
        }

        // Load all Pokémon first
        var pkms = new List<PKM>();
        foreach (var file in files)
        {
            var pk = FileUtil.GetSingleFromPath(file, destView.SAV);
            if (pk is null) return false;
            pkms.Add(pk);
        }

        // Place each Pokémon (overwrite if Alt pressed, otherwise skip occupied slots)
        int currentSlot = startSlot;
        int currentBox = destSlot is SlotInfoBox db ? db.Box : 0;
        bool overwriteMode = mod == DropModifier.Overwrite || mod == DropModifier.CloneAndOverwrite;

        foreach (var pk in pkms)
        {
            bool foundSlot = false;
            while (!foundSlot)
            {
                // Wrap to next box if needed
                if (currentSlot >= boxSize && destSlot is SlotInfoBox)
                {
                    currentBox++;
                    currentSlot = 0;
                    if (currentBox >= destView.SAV.BoxCount)
                        break; // No more boxes available
                }

                var targetSlot = destSlot is SlotInfoBox
                    ? new SlotInfoBox(currentBox, currentSlot, destView.SAV)
                    : destSlot;

                var targetSlotView = new SlotViewInfo<PictureBox>(targetSlot, destView);

                // In overwrite mode, place regardless of slot state
                // Otherwise, only place in empty slots
                if (overwriteMode || targetSlotView.IsEmpty())
                {
                    var msg = targetSlotView.CanWriteTo(pk);
                    if (msg == WriteBlockedMessage.None)
                    {
                        Env.Slots.Set(targetSlot, pk, SlotTouchType.Set);
                        foundSlot = true;
                    }
                }

                currentSlot++;

                // Safety check: if we've checked all possible slots, break
                if (currentSlot >= boxSize && currentBox >= destView.SAV.BoxCount - 1)
                    break;
            }

            if (!foundSlot)
                break; // No more slots available
        }

        // Handle source deletion: silently clear sources unless we're cloning (no delete animation)
        if (mod != DropModifier.Clone && mod != DropModifier.CloneAndOverwrite && Selection.Count > 0)
        {
            var blank = destView.SAV.BlankPKM;
            foreach (var srcSlot in Selection.GetAll())
                Env.Slots.Set(srcSlot.Slot, blank, SlotTouchType.None);
        }

        Drag.ResetCursor(pb.FindForm());
        return true;
    }

    // Utility
    public void SwapBoxes(int index, int other, SaveFile SAV)
    {
        if (index == other)
            return;
        SAV.SwapBox(index, other);
        UpdateBoxViewAtBoxIndexes(index, other);
    }

    public void Dispose()
    {
        Hover.Dispose();
        SE.Dispose();
        LastSlot.OriginalBackground?.Dispose();
        LastSlot.CurrentBackground?.Dispose();
    }

    private void UpdateBoxViewAtBoxIndexes(params ReadOnlySpan<int> boxIndexes)
    {
        foreach (var box in Boxes)
        {
            var current = box.CurrentBox;
            if (!boxIndexes.Contains(current))
                continue;
            box.ResetSlots();
            box.ResetBoxNames(current);
        }
    }
}
