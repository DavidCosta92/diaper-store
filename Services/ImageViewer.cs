namespace panialera.Services;

// Shared image viewer: any ProductImage opens it and a single ImageViewerHost
// (in MainLayout) shows the large image.
public class ImageViewer
{
    public string? Src { get; private set; }
    public string? Title { get; private set; }

    public event Action? Changed;

    public void Show(string src, string? title)
    {
        Src = src;
        Title = title;
        Changed?.Invoke();
    }

    public void Close()
    {
        Src = null;
        Title = null;
        Changed?.Invoke();
    }
}
