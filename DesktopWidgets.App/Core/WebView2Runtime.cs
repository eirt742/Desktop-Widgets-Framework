using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DesktopWidgets.App.Core;

/// <summary>
/// Сервис управления средой WebView2. 
/// Обеспечивает изоляцию профилей для параллельно открывающихся виджетов.
/// </summary>
internal static class WebView2Runtime
{
    private static readonly SemaphoreSlim ControllerGate = new(1, 1);

    private static async Task<CoreWebView2Environment> CreateEnvironmentForWidgetAsync()
    {
        string baseDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopWidgets",
            "WebView2"
        );

        string uniqueFolderName = $"Widget_{Guid.NewGuid():N}";
        string userDataFolder = Path.Combine(baseDataFolder, uniqueFolderName);

        Directory.CreateDirectory(userDataFolder);

        return await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder,
            options: null
        ).ConfigureAwait(false);
    }

    public static async Task<CoreWebView2> InitializeAsync(WebView2 control)
    {
        ArgumentNullException.ThrowIfNull(control);

        await ControllerGate.WaitAsync();
        try
        {
            CoreWebView2Environment environment = await CreateEnvironmentForWidgetAsync();

            await control.Dispatcher.InvokeAsync(async () =>
            {
                await control.EnsureCoreWebView2Async(environment);
            }).Task.Unwrap();

            return control.CoreWebView2 ?? throw new InvalidOperationException(
                "WebView2 initialization completed without a CoreWebView2 instance."
            );
        }
        finally
        {
            ControllerGate.Release();
        }
    }
}