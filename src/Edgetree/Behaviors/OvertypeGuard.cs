using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

namespace SidebarExplorer.App.Behaviors;

// Stops WPF's overtype mode from ever starting in a text box.
//
// WPF binds a bare Insert to EditingCommands.ToggleInsert, and implements
// overtype through TSF's "transitory extension" - a temporary document with a
// system-drawn popup. With a Korean IME that temporary document holds the
// composition and won't let go: one keystroke lands two characters, in a white
// system box that ignores the app's font and doesn't go away when the caret
// moves. The box stays broken until Insert is pressed again or the window
// changes, which is why it reads as "composition suddenly happens outside the
// field" rather than as an Insert problem.
//
// This is WPF-wide, not an app bug, and it was investigated to the source and
// verified in a bare one-TextBox app - see wpf-korean-ime.private.md in the
// TabStick repo before spending any time on it again. Notably: turning off
// TextEditor.AllowOvertype does NOT help (OnToggleInsert never consults it),
// and IMM32 can't reach the composition (ImmNotifyIME returns TRUE and changes
// nothing) because it lives in a different TSF document.
//
// The fix is aimed at the COMMAND, not the key. An instance CommandBinding is
// consulted before WPF's class binding, so ToggleInsert simply never runs -
// while Ctrl+Insert (copy) and Shift+Insert (paste) are separate commands and
// keep working. What's lost is overtype mode alone, which never worked properly
// with a Korean IME anyway and which browsers don't offer either.
//
// Every text box in the app needs this: one that misses it is the only place
// the symptom appears, which makes it look intermittent.
public static class OvertypeGuard
{
    public static void Disable(System.Windows.Controls.Primitives.TextBoxBase box)
    {
        box.CommandBindings.Add(new CommandBinding(
            EditingCommands.ToggleInsert,
            (_, e) => e.Handled = true,
            (_, e) =>
            {
                // CanExecute answered here as well, so the class-level handler
                // is never asked - leaving it to answer would let the command
                // route on to it.
                e.CanExecute = true;
                e.Handled = true;
            }));
    }
}
