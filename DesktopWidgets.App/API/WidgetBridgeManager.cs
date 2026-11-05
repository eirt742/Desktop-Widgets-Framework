using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DesktopWidgets.App.API
{
    public sealed class WidgetBridgeManager
    {
        private static readonly Lazy<WidgetBridgeManager> _instance =
            new(() => new WidgetBridgeManager());

        public static WidgetBridgeManager Instance => _instance.Value;

        private readonly Dictionary<string,
            Func<WidgetBridgeContext, JsonElement, object?>> _methods = new();

        private readonly List<WidgetBridgeConnection> _connections = new();

        private WidgetBridgeManager()
        {
            RegisterDefaultMethods();
        }

        private void RegisterDefaultMethods()
        {
            Register("getWidgetInfo", (context, args) =>
            {
                return new
                {
                    name = context.Widget.WidgetName,
                    version = "1.0"
                };
            });

            Register("setPosition", (context, args) =>
            {
                if (args.ValueKind != JsonValueKind.Array ||
                    args.GetArrayLength() < 2)
                {
                    throw new ArgumentException(
                        "setPosition requires two arguments: x and y."
                    );
                }

                int x = args[0].GetInt32();
                int y = args[1].GetInt32();

                context.Widget.Left = x;
                context.Widget.Top = y;

                return new
                {
                    x,
                    y
                };
            });

            Register("minimize", (context, args) =>
            {
                context.Widget.WindowState =
                    System.Windows.WindowState.Minimized;

                return new
                {
                    success = true
                };
            });
        }

        public void Register(
            string name,
            Func<WidgetBridgeContext, JsonElement, object?> handler)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Method name cannot be empty.");

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (!_methods.TryAdd(name, handler))
            {
                throw new InvalidOperationException(
                    $"Method '{name}' is already registered."
                );
            }
        }

        public bool Unregister(string name)
        {
            return _methods.Remove(name);
        }

        public WidgetBridgeConnection Connect(
            CoreWebView2 webView,
            WidgetWindow widget)
        {
            var connection = new WidgetBridgeConnection(
                this,
                webView,
                widget
            );

            _connections.Add(connection);

            return connection;
        }

        internal void Disconnect(WidgetBridgeConnection connection)
        {
            _connections.Remove(connection);
        }

        internal void HandleMessage(
            WidgetBridgeConnection connection,
            string messageJson)
        {
            string? id = null;

            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(messageJson);

                JsonElement root = document.RootElement;

                if (!root.TryGetProperty("id", out JsonElement idElement) ||
                    idElement.ValueKind != JsonValueKind.String)
                {
                    return;
                }

                id = idElement.GetString();

                if (string.IsNullOrEmpty(id))
                    return;

                if (!root.TryGetProperty("method", out JsonElement methodElement) ||
                    methodElement.ValueKind != JsonValueKind.String)
                {
                    throw new ArgumentException("Method name is missing.");
                }

                string methodName = methodElement.GetString()!;

                JsonElement args = root.TryGetProperty(
                    "args",
                    out JsonElement argsElement)
                    ? argsElement
                    : default;

                if (!_methods.TryGetValue(methodName, out var handler))
                {
                    throw new InvalidOperationException(
                        $"Unknown method: {methodName}"
                    );
                }

                var context = new WidgetBridgeContext(connection.Widget);

                object? result = handler(context, args);

                connection.SendResponse(id, true, result, null);
            }
            catch (Exception ex)
            {
                if (id != null)
                {
                    connection.SendResponse(
                        id,
                        false,
                        null,
                        ex.Message
                    );
                }
            }
        }

        public void BroadcastEvent(string eventName, object? data = null)
        {
            foreach (var connection in _connections.ToArray())
            {
                connection.SendEvent(eventName, data);
            }
        }
    }
}