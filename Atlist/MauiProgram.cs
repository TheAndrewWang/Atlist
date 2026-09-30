using Atlist.Services;
using Atlist.ViewModels;
using Atlist.Views;

namespace Atlist;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // Put these .ttf files in Resources/Fonts (all free from Google Fonts).
                fonts.AddFont("AtkinsonHyperlegible-Regular.ttf", "Atkinson");
                fonts.AddFont("AtkinsonHyperlegible-Bold.ttf", "AtkinsonBold");
                fonts.AddFont("Fraunces-SemiBold.ttf", "FrauncesSemiBold");
                fonts.AddFont("VT323-Regular.ttf", "LcdFont"); // pixel font for the LCD mirror
            });

        // One Bluetooth service for the whole app, so every page shares the same connection.
        builder.Services.AddSingleton<IChecklistBleService, ChecklistBleService>();
        builder.Services.AddSingleton<PairedDeviceStore>();
        builder.Services.AddSingleton<QuickMessageStore>();
        builder.Services.AddSingleton<ScheduleStore>();
        builder.Services.AddSingleton<HistoryStore>();
        builder.Services.AddSingleton<DeviceLink>(); // one at a time: every page shares its lock
        builder.Services.AddSingleton<MessageService>();
        builder.Services.AddSingleton<DeviceSyncService>();

        builder.Services.AddTransient<ConnectViewModel>();
        builder.Services.AddTransient<ConnectPage>();

        builder.Services.AddTransient<DevicesViewModel>();
        builder.Services.AddTransient<DevicesPage>();

        builder.Services.AddTransient<ComposeViewModel>();
        builder.Services.AddTransient<ComposePage>();

        builder.Services.AddTransient<QuickMessagesViewModel>();
        builder.Services.AddTransient<QuickMessagesPage>();

        builder.Services.AddTransient<ScheduleViewModel>();
        builder.Services.AddTransient<SchedulePage>();

        builder.Services.AddTransient<ScheduleEditViewModel>();
        builder.Services.AddTransient<ScheduleEditPage>();

        builder.Services.AddTransient<DeviceSettingsViewModel>();
        builder.Services.AddTransient<DeviceSettingsPage>();

        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<HistoryPage>();

        return builder.Build();
    }
}
