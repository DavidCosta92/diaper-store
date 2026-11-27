using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace panialera.Services;

public enum WhatsAppTarget
{
    App,
    Web,
    None
}

// Hands a message to WhatsApp with the text ready; the user picks the chat. Uses WhatsApp Desktop
// when this PC has it, otherwise WhatsApp Web in the default browser. The caller also copies the
// text, so it can be pasted if neither opens.
public class WhatsAppShare(Func<string, bool>? open = null, Func<bool>? appInstalled = null)
{
    private readonly Func<string, bool> _open = open ?? OpenWithWindows;
    private readonly Func<bool> _appInstalled = appInstalled ?? HasAppHandler;

    public static string AppLinkFor(string text) => "whatsapp://send?text=" + Uri.EscapeDataString(text);

    public static string WebLinkFor(string text) => "https://web.whatsapp.com/send?text=" + Uri.EscapeDataString(text);

    public WhatsAppTarget TryOpen(string text) =>
        Open(AppLinkFor(text), WebLinkFor(text));

    // The chat with one number (country code included, e.g. 5492644123456).
    public WhatsAppTarget TryOpenChat(string number) =>
        Open("whatsapp://send?phone=" + number, "https://web.whatsapp.com/send?phone=" + number);

    private WhatsAppTarget Open(string appLink, string webLink)
    {
        if (_appInstalled() && _open(appLink))
            return WhatsAppTarget.App;
        return _open(webLink) ? WhatsAppTarget.Web : WhatsAppTarget.None;
    }

    private static bool OpenWithWindows(string link)
    {
        try
        {
            Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Logger.Error("Could not open a WhatsApp link", ex);
            return false;
        }
    }

    // Whether Windows has an app for whatsapp:// links (WhatsApp Desktop registers it). Asked
    // before opening, because an unknown link can make Windows offer the Store instead of failing.
    private static bool HasAppHandler()
    {
        const int isProtocol = 0x1000;
        const int friendlyAppName = 4;
        try
        {
            var name = new StringBuilder(256);
            uint size = 256;
            return AssocQueryString(isProtocol, friendlyAppName, "whatsapp", null, name, ref size) == 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(int flags, int str, string assoc, string? extra,
        StringBuilder output, ref uint outputSize);
}
