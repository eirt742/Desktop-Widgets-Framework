namespace DesktopWidgets.App.API
{
    public sealed class WidgetBridgeContext
    {
        public WidgetWindow Widget { get; }

        internal WidgetBridgeContext(WidgetWindow widget)
        {
            Widget = widget;
        }
    }
}