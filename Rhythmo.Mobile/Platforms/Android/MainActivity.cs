using Android.App;

using Android.Content;

using Android.Content.PM;

using Android.OS;



namespace Rhythmo.Mobile;



[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]

[IntentFilter(

	[Intent.ActionView],

	Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],

	DataSchemes = ["rhythmo"],

	DataHosts = ["login-callback"],

	DataPathPrefix = "/")]

public class MainActivity : MauiAppCompatActivity

{

	protected override void OnCreate(Bundle? savedInstanceState)

	{

		base.OnCreate(savedInstanceState);

		ForwardAppLink(Intent);

	}



	protected override void OnNewIntent(Intent? intent)

	{

		base.OnNewIntent(intent);

		Intent = intent;

		ForwardAppLink(intent);

	}



	static void ForwardAppLink(Intent? intent)

	{

		var data = intent?.Data;

		if (data is null)

			return;



		var uri = AndroidUriToSystemUri(data);

		if (uri is null)

			return;



		if (Microsoft.Maui.Controls.Application.Current is App app)

			app.DispatchAppLink(uri);

	}



	static Uri? AndroidUriToSystemUri(Android.Net.Uri data)

	{

		var s = data.ToString();

		return string.IsNullOrWhiteSpace(s) ? null : new Uri(s);

	}

}


