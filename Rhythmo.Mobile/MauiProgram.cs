using Microsoft.Extensions.Logging;
using Rhythmo.Mobile.Configuration;
using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Services;
using Rhythmo.Mobile.Social;
using Rhythmo.Mobile.Theme;
using SupabaseClient = Supabase.Client;

namespace Rhythmo.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("MaterialSymbolsRounded.ttf", SessionMuscleGlyph.FontAlias);
			});

		var supaSettings = SupabaseSettings.Load();
		builder.Services.AddSingleton(supaSettings);
		builder.Services.AddSingleton(_ =>
		{
			var options = new Supabase.SupabaseOptions { AutoRefreshToken = true, AutoConnectRealtime = false };
			return new SupabaseClient(supaSettings.Url, supaSettings.AnonKey, options);
		});
		builder.Services.AddSingleton<SupabaseAuthService>();
		builder.Services.AddSingleton<RhythmoMemoryCache>();
		builder.Services.AddSingleton<IRhythmoRepository, SupabaseRhythmoRepository>();
		builder.Services.AddSingleton<SupabaseBootstrap>();
		builder.Services.AddSingleton<ActiveProfileStore>();
		builder.Services.AddSingleton<GlobalExceptionBootstrap>();
		builder.Services.AddSingleton<IDevErrorPresenter, DevPopupErrorPresenter>();
		builder.Services.AddSingleton<SocialHubService>();
		builder.Services.AddSingleton<PersonalRecordService>();
		builder.Services.AddSingleton<WorkoutDraftStore>();
		builder.Services.AddTransient<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		ServiceHelper.Services = app.Services;
		app.Services.GetService<GlobalExceptionBootstrap>()?.Register();
		return app;
	}
}
