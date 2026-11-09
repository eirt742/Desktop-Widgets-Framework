using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace DesktopWidgets.App.API
{
    public sealed class WidgetBridgeConnection : IDisposable
    {
        private readonly WidgetBridgeManager _manager;
        private readonly CoreWebView2 _webView;

        private bool _disposed;

        public WidgetWindow Widget { get; }

        internal WidgetBridgeConnection(
            WidgetBridgeManager manager,
            CoreWebView2 webView,
            WidgetWindow widget)
        {
            _manager = manager;
            _webView = webView;
            Widget = widget;

            _webView.WebMessageReceived += OnWebMessageReceived;
        }

        private void OnWebMessageReceived(
            object? sender,
            CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (_disposed)
                return;

            _manager.HandleMessage(this, e.WebMessageAsJson);
        }

        internal void SendResponse(
            string id,
            bool success,
            object? result,
            string? error)
        {
            SendMessage(new
            {
                id,
                success,
                result,
                error
            });
        }

        public void SendEvent(string eventName, object? data = null)
        {
            SendMessage(new
            {
                type = eventName,
                data
            });
        }

        private void SendMessage(object message)
        {
            if (_disposed)
                return;

            try
            {
                string json = JsonSerializer.Serialize(message);
                _webView.PostWebMessageAsJson(json);
            }
            catch (InvalidOperationException)
            {
                // WebView2 уже уничтожен.
                Dispose();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // WebView2 уже недоступен.
                Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _webView.WebMessageReceived -= OnWebMessageReceived;
            }
            catch
            {
                // WebView2 уже уничтожен.
            }

            _manager.Disconnect(this);
        }
    }
}