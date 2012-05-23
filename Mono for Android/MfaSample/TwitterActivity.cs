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
using SimpleOAuth;

namespace MfaSample
{
	[Activity(Label = "StackMob Twitter Sample")]
	public class TwitterActivity : Activity
	{
		// Twitter API values
		private const string ConsumerKey = "consumer key";
		private const string ConsumerSecret = "consumer secret";
		private const string RedirectUri = "redirect uri";

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate(bundle);
			SetContentView (Resource.Layout.Social);

			this.display = FindViewById<TextView> (Resource.Id.display);
			this.web = FindViewById<WebView> (Resource.Id.web);

			FindViewById<Button> (Resource.Id.createuser)
				.Click += OnClickTwitterCreate;

			FindViewById<Button> (Resource.Id.login)
				.Click += OnClickTwitterLogin;

			FindViewById<Button> (Resource.Id.info)
				.Click += OnClickTwitterInfo;

			this.actionLayout = FindViewById<LinearLayout> (Resource.Id.actionsLayout);

			TwitterAuthClient client = new TwitterAuthClient (web);
			web.SetWebViewClient (client);

			client.AccessToken.ContinueWith (OnAccessTokenReceived);
			client.Auth();
		}

		private void OnClickTwitterCreate (object sender, EventArgs eventArgs)
		{
			StackMobActivity.StackMob.CreateUserWithTwitter ("twtestuser", this.accessToken, this.accessSecret,
				() => Display ("User created"), Display);
		}

		private void OnClickTwitterInfo (object sender, EventArgs eventArgs)
		{
			StackMobActivity.StackMob.GetTwitterUserInfo (Display, Display);
		}

		private void OnClickTwitterLogin (object sender, EventArgs eventArgs)
		{
			StackMobActivity.StackMob.LoginWithTwitter (this.accessToken, this.accessSecret,
				(username, dict) => Display ("Username: " + username + System.Environment.NewLine + GetStringForDictionary (dict)),
				Display);
		}

		private TextView display;
		private LinearLayout actionLayout;
		private WebView web;

		private string accessToken;
		private string accessSecret;

		private void OnAccessTokenReceived (Task<Tuple<string, string>> task)
		{
			RunOnUiThread (() =>
			{
				if (task.IsFaulted)
					Display (task.Exception.InnerException);
				else
				{
					this.accessToken = task.Result.Item1;
					this.accessSecret = task.Result.Item2;
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
					Display (reader.ReadToEnd() + System.Environment.NewLine + ex);
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
			this.web.Visibility = ViewStates.Gone;
			this.actionLayout.Visibility = ViewStates.Visible;
			RunOnUiThread (() => this.display.Text = text);
		}

		private class TwitterAuthClient
			: WebViewClient
		{
			public TwitterAuthClient (WebView view)
			{
				this.view = view;
			}

			public Task<Tuple<string, string>> AccessToken
			{
				get { return this.tcs.Task; }
			}

			public void Auth()
			{
				this.view.Visibility = ViewStates.Visible;

				HttpWebRequest request = new HttpWebRequest (new Uri ("https://api.twitter.com/oauth/request_token"));
				request.SignRequest (new Tokens { ConsumerKey = ConsumerKey, ConsumerSecret = ConsumerSecret })
					.WithCallback (RedirectUri)
					.InHeader();

				try
				{
					HttpWebResponse response = (HttpWebResponse)request.GetResponse();
					using (Stream rstream = response.GetResponseStream())
					using (StreamReader reader = new StreamReader (rstream))
					{
						string rcontents = reader.ReadToEnd();
						var values = ParseQueryString (rcontents);
						if (values["oauth_callback_confirmed"] != "true")
						{
							this.tcs.SetException (new Exception ("Callback not confirmed"));
							return;
						}

						this.token = values ["oauth_token"];
						this.secret = values ["oauth_token_secret"];
					}
				}
				catch (Exception ex)
				{
					this.tcs.SetException (ex);
					return;
				}

				this.view.LoadUrl ("https://api.twitter.com/oauth/authenticate?oauth_token=" + this.token);
			}

			public override void OnPageStarted (WebView view, string url, Android.Graphics.Bitmap favicon)
			{
				if (!url.StartsWith (RedirectUri) || this.tcs.Task.IsCompleted)
					return;

				this.view.Visibility = ViewStates.Gone;

				var values = ParseQueryString (new Uri (url).Query);

				if (values["oauth_token"] != this.token)
				{
					this.tcs.SetException (new Exception ("Invalid token"));
					return;
				}

				this.verifier = values ["oauth_verifier"];

				HttpWebRequest request = new HttpWebRequest (new Uri ("https://api.twitter.com/oauth/access_token"));
				request.Method = "POST";
				request.SignRequest (new Tokens
				{
					ConsumerKey = ConsumerKey,
					ConsumerSecret = ConsumerSecret,
					AccessToken = this.token,
					AccessTokenSecret = this.secret
				}).InHeader();

				using (StreamWriter writer = new StreamWriter (request.GetRequestStream()))
				{
					writer.Write ("oauth_verifier=" + this.verifier);
					writer.Flush();
				}

				try
				{
					HttpWebResponse httpresponse = (HttpWebResponse)request.GetResponse();
					using (Stream rstream = httpresponse.GetResponseStream())
					using (StreamReader reader = new StreamReader (rstream))
					{
						string response = Uri.UnescapeDataString (reader.ReadToEnd());
						var parts = ParseQueryString (response);

						this.tcs.SetResult (new Tuple<string, string> (parts ["oauth_token"], parts ["oauth_token_secret"]));
					}
				}
				catch (WebException ex)
				{
					this.tcs.SetException (ex);
				}

				base.OnPageStarted (view, url, favicon);
			}
			
			private readonly TaskCompletionSource<Tuple<string, string>> tcs = new TaskCompletionSource<Tuple<string, string>>();
			private readonly WebView view;
			
			private string token;
			private string secret;
			private string verifier;

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