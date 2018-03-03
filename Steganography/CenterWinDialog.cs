using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal class CenterWinDialog : IDisposable
{
    private readonly Form mOwner;
    private int mTries;

    public CenterWinDialog(Form owner)
    {
        mOwner = owner;
        owner.BeginInvoke(new MethodInvoker(findDialog));
    }

    public void Dispose()
    {
        mTries = -1;
    }

    private void findDialog()
    {
        if (mTries < 0) return;
        var callback = new EnumThreadWndProc(checkWindow);
        if (EnumThreadWindows(GetCurrentThreadId(), callback, IntPtr.Zero))
            if (++mTries < 10)
                mOwner.BeginInvoke(new MethodInvoker(findDialog));
    }

    private bool checkWindow(IntPtr hWnd, IntPtr lp)
    {
        var sb = new StringBuilder(260);
        GetClassName(hWnd, sb, sb.Capacity);
        if (sb.ToString() != "#32770") return true;
        var frmRect = new Rectangle(mOwner.Location, mOwner.Size);
        RECT dlgRect;
        GetWindowRect(hWnd, out dlgRect);
        MoveWindow(hWnd,
            frmRect.Left + (frmRect.Width - dlgRect.Right + dlgRect.Left) / 2,
            frmRect.Top + (frmRect.Height - dlgRect.Bottom + dlgRect.Top) / 2,
            dlgRect.Right - dlgRect.Left,
            dlgRect.Bottom - dlgRect.Top, true);
        return false;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(int tid, EnumThreadWndProc callback, IntPtr lp);

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int buflen);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rc);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);

    private delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lp);

    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}