using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DesktopWidgets.App.Core;

/// <summary>
/// Owns the single WebView2 environment and serializes controller creation.
/// All WebView2 controls in the application must use this service.
/// </summary>
internal static class WebView2Runtime
{
    private static readonly Lazy<Task<CoreWebView2Environment>> EnvironmentTask =
        new(CreateEnvironmentAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly SemaphoreSlim ControllerGate = new(1, 1);

    private static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        string userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopWidgets",
            "WebView2"
        );

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
            CoreWebView2Environment environment = await EnvironmentTask.Value;
            await control.EnsureCoreWebView2Async(environment);

            return control.CoreWebView2
                ?? throw new InvalidOperationException(
                    "WebView2 initialization completed without a CoreWebView2 instance."
                );
        }
        finally
        {
            ControllerGate.Release();
        }
    }
}
