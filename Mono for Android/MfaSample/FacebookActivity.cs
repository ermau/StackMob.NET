using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Webkit;
using Android.Widget;

namespace MfaSample
{
	[Activity(Label = "StackMob Facebook Sample")]
	public class FacebookActivity : Activity
	{
		private const string AppId = "app id";
		private const string AppSecret = "app secret";
		private const string RedirectUri = "redirect uri";

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate(bundle);
			SetContentView (Resource.Layout.Social);

			this.display = FindViewById<TextView> (Resource.Id.display);

			WebView web = FindViewById<WebView> (Resource.Id.web);

			FindViewById<Button> (Resource.Id.createuser)
				.Click += OnClickFacebookCreate;

			FindViewById<Button> (Resource.Id.login)
				.Click += OnClickFacebookLogin;

			FindViewById<Button> (Resource.Id.info)
				.Click += OnClickFacebookInfo;

			this.actionLayout = FindViewById<LinearLayout> (Resource.Id.actionsLayout);

			FacebookAuthClient client = new FacebookAuthClient (web);
			web.SetWebViewClient (client);

			client.AccessToken.ContinueWith (OnAccessTokenReceived);
			client.Auth();
		}

		private void OnClickFacebookCreate (object sender, EventArgs eventArgs)
		{
			StackMobActivity.StackMob.CreateUserWithFacebook ("fbtestuser", this.accessToken,
				() => Display ("User created"), Display);
		}

		private void OnClickFacebookInfo (object sender, EventArgs eventArgs)
		{
			StackMobActivity.StackMob.GetFacebookUserInfo (Display, Display);
		}

		private void OnClickFacebookLogin (object sender, EventArgs eventArgs)
		{
			StackMobActivity.StackMob.LoginWithFacebook (this.accessToken,
				(username, dict) => Display ("Username: " + username + System.Environment.NewLine + GetStringForDictionary (dict)),
				Display);
		}

		private TextView display;
		private LinearLayout actionLayout;
		private string accessToken;

		private void OnAccessTokenReceived (Task<string> task)
		{
			RunOnUiThread (() =>
			{
				if (task.IsFaulted)
					Toast.MakeText (this, "Error: " + task.Exception.InnerExceptions.First().Message, ToastLength.Long).Show();
				else
				{
					this.accessToken = task.Result;
					this.actionLayout.Visibility = ViewStates.Visible;
				}
			});
		}

		private void Display (Exception ex)
		{
			var wex = ex as WebException;
			if (wex != null)
			{
				using (Stream stream = wex.Response.GetResponseStream())
				using (StreamReader reader = new StreamReader (stream))
					Display (reader.ReadToEnd() + System.Environment.NewLine + ex.ToString());
			}
			else
				Display (ex.ToString());
		}

		private void Display (IEnumerable<KeyValuePair<string, object>> dict)
		{
			Display (GetStringForDictionary (dict));
		}

		private string GetStringForDictionary (IEnumerable<KeyValuePair<string, object>> dict)
		{
			StringBuilder builder = new StringBuilder();
			foreach (var kvp in dict)
			{
				builder.Append ("[");
				builder.Append (kvp.Key);
				builder.Append ("] = ");
				builder.AppendLine (kvp.Value.ToString());
			}

			return builder.ToString();
		}

		private void Display (string text)
		{
			RunOnUiThread (() => this.display.Text = text);
		}

		private class FacebookAuthClient
			: WebViewClient
		{
			public FacebookAuthClient (WebView view)
			{
				this.view = view;
			}

			public Task<string> AccessToken
			{
				get { return this.tcs.Task; }
			}

			public void Auth()
			{
				this.view.Visibility = ViewStates.Visible;
				this.requestId = Guid.NewGuid().ToString();

				this.view.LoadUrl ("https://www.facebook.com/dialog/oauth?client_id=" + AppId
				                   + "&redirect_uri=" + Uri.EscapeUriString (RedirectUri)
				                   + "&state=" + Uri.EscapeUriString (this.requestId));
			}

			public override void OnPageStarted (WebView view, string url, Android.Graphics.Bitmap favicon)
			{
				if (!url.StartsWith (RedirectUri) || this.tcs.Task.IsCompleted)
					return;

				this.view.Visibility = ViewStates.Gone;

				var values = ParseQueryString (new Uri (url).Query);

				if (values ["state"] != this.requestId)
				{
					this.tcs.SetException (new Exception ("State mismatch"));
					return;
				}

				if (!values.ContainsKey ("code"))
				{
					this.tcs.SetException (new Exception ("No code"));
					return;
				}

				HttpWebRequest request =
					new HttpWebRequest (new Uri ("https://graph.facebook.com/oauth/access_token?client_id=" + AppId
						                            + "&redirect_uri=" + Uri.EscapeUriString (RedirectUri)
						                            + "&client_secret=" + Uri.EscapeUriString (AppSecret)
						                            + "&code=" + Uri.EscapeUriString (values ["code"])));

				try
				{
					HttpWebResponse httpresponse = (HttpWebResponse)request.GetResponse();
					using (Stream rstream = httpresponse.GetResponseStream())
					using (StreamReader reader = new StreamReader (rstream))
					{
						string response = Uri.UnescapeDataString (reader.ReadToEnd());
						var parts = ParseQueryString (response);

						string token = parts ["access_token"];
						this.tcs.SetResult (token);
					}
				}
				catch (WebException ex)
				{
					this.tcs.SetException (ex);
				}

				base.OnPageStarted (view, url, favicon);
			}
			
			private readonly TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
			private readonly WebView view;
			private string requestId;

			private IDictionary<string, string> ParseQueryString (string query)
			{
				if (query.StartsWith ("?"))
					query = query.Substring (1, query.Length - 1);

				Dictionary<string, string> dict = new Dictionary<string, string>();

				string[] parts = query.Split ('&');
				foreach (string part in parts)
				{
					string[] nameAndValue = part.Split ('=');
					string name = Uri.UnescapeDataString (nameAndValue [0]);
					string value = Uri.UnescapeDataString (nameAndValue [1]);

					dict [name] = value;
				}

				return dict;
			}
		}
	}
}