using System.ComponentModel;
using System.Windows;

namespace EyeKeeper;

// Closing a WPF window can synchronously raise Deactivated and other callbacks.
// Requests from those callbacks must not re-enter Window.Close().
public class SafeWindow : Window
{
    bool closing, closed;
    public void RequestClose()
    {
        if (!closing && !closed) Close();
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        closing = true;
        base.OnClosing(e);
        if (e.Cancel) closing = false;
    }
    protected override void OnClosed(System.EventArgs e)
    {
        closed = true;
        base.OnClosed(e);
    }
}
