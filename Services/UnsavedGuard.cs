namespace panialera.Services;

public enum GuardChoice { Save, Discard }

// Tracks the "dirty" state of forms and drives the "unsaved changes" dialog.
// Forms register themselves (as a stack) with: whether they are dirty, how to save, how to discard.
public class UnsavedGuard
{
    private readonly List<Registration> _regs = new();

    // Set by the dialog component (UnsavedChangesDialog), which lives in the layout.
    public Func<Task<GuardChoice>>? DialogHandler { get; set; }

    public IDisposable Register(Func<bool> isDirty, Func<Task<bool>> save, Action discard)
    {
        var reg = new Registration(isDirty, save, discard);
        _regs.Add(reg);
        return new Unregister(this, reg);
    }

    // The "active" form is the last one registered (a modal sits on top of its page).
    private Registration? Current => _regs.Count > 0 ? _regs[^1] : null;

    public bool IsDirty => Current?.IsDirty() ?? false;

    // Returns true when it is fine to continue (saved successfully or discarded); false
    // otherwise (saving failed validation, which counts as cancel).
    public async Task<bool> TryLeaveAsync()
    {
        var cur = Current;
        if (cur is null || !cur.IsDirty())
            return true;
        if (DialogHandler is null)
            return true;

        var choice = await DialogHandler();
        if (choice == GuardChoice.Discard)
        {
            cur.Discard();
            return true;
        }
        // Save: if validation fails, Save returns false and navigation does not continue.
        return await cur.Save();
    }

    private sealed record Registration(Func<bool> IsDirty, Func<Task<bool>> Save, Action Discard);

    private sealed class Unregister(UnsavedGuard guard, object reg) : IDisposable
    {
        public void Dispose() => guard._regs.Remove((Registration)reg);
    }
}
