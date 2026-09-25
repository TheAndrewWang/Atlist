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
                // Put these .ttf files in Resources/Fonts (free from Google Fonts).
                fonts.AddFont("AtkinsonHyperlegible-Regular.ttf", "Atkinson");
                fonts.AddFont("AtkinsonHyperlegible-Bold.ttf", "AtkinsonBold");
                fonts.AddFont("Fraunces-SemiBold.ttf", "FrauncesSemiBold");
            });

        // One Bluetooth service for the whole app, so every page shares the same connection.
        builder.Services.AddSingleton<IChecklistBleService, ChecklistBleService>();
        builder.Services.AddSingleton<PairedDeviceStore>();

        builder.Services.AddTransient<ConnectViewModel>();
        builder.Services.AddTransient<ConnectPage>();

        return builder.Build();
    }
}
