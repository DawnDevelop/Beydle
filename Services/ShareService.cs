using Microsoft.JSInterop;

namespace Beydle.Services;

/// <summary>The data wwwroot/js/share.js draws the result image from. Serialised with camelCase names.</summary>
public sealed record ShareCard(
    string Title,
    string Summary,
    bool XtremeMode,
    int Columns,
    int[][] Rows,
    string[] Left,
    bool[] Picture,
    string Site);

public sealed class ShareService(IJSRuntime js)
{
    public async Task<bool> CopyToClipboardAsync(string text)
    {
        try
        { await js.InvokeVoidAsync("navigator.clipboard.writeText", text); return true; }
        catch (JSException) { return false; }
    }

    /// <summary>Draws and shares the result card (wwwroot/js/share.js): "shared", "copied", "saved", "cancelled" or "failed".</summary>
    public async Task<string> ShareImageAsync(ShareCard card)
    {
        try
        { return await js.InvokeAsync<string>("beydleShareImage", card); }
        catch (JSException) { return "failed"; }
    }
}
