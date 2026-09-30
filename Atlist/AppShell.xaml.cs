using Atlist.Services;
using Atlist.Views;

namespace Atlist;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Pages opened on top of a tab (they have a back link, not a tab).
        Routing.RegisterRoute("settings", typeof(DeviceSettingsPage));
        Routing.RegisterRoute("quickmessages", typeof(QuickMessagesPage));
        Routing.RegisterRoute("scheduleedit", typeof(ScheduleEditPage));

        // Returning users go straight to their devices instead of the Connect screen.
        if (new PairedDeviceStore().GetAll().Count > 0)
            CurrentItem = Items.OfType<TabBar>().First();
    }
}
