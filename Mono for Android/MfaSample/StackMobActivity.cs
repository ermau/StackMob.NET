using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using StackMob;

namespace MfaSample
{
	[Activity(Label = "StackMob.NET Sample", MainLauncher = true, Icon = "@drawable/icon")]
	public class StackMobActivity
		: Activity
	{
		public static readonly StackMobClient StackMob =
			new StackMobClient ("api key", "api secret", "app name", 0);

		private TextView display;
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate(bundle);
			SetContentView (Resource.Layout.Main);

			this.display = FindViewById<TextView> (Resource.Id.display);

			FindViewById<Button> (Resource.Id.facebook)
				.Click += (sender, args) => StartActivity (new Intent (this, typeof (FacebookActivity)));

			FindViewById<Button> (Resource.Id.twitter)
				.Click += (sender, args) => StartActivity (new Intent (this, typeof (TwitterActivity)));

			FindViewById<Button> (Resource.Id.login)
				.Click += OnLoginClicked;

			FindViewById<Button> (Resource.Id.get)
				.Click += OnGetClicked;

			FindViewById<Button> (Resource.Id.create)
				.Click += OnCreateClicked;

			FindViewById<Button> (Resource.Id.update)
				.Click += OnUpdateClicked;
		}

		private void OnLoginClicked (object sender, EventArgs eventArgs)
		{
			StackMob.Login (new Dictionary<string, string> { { "username", "test" }, { "password", "test" } },
			                   () => Display ("Logged in"),
							   ex => Display (ex.ToString()));
		}

		private string id;
		private void OnCreateClicked (object sender, EventArgs eventArgs)
		{
			StackMob.Create ("messages", new Dictionary<string, object>
			{
				{ "message", "Hello, world!" }
			},
			dict =>
			{
				this.id = (string)dict ["messages_id"];
				Display (dict);
			},
			ex => Display (ex.ToString()));
		}

		private void OnGetClicked (object sender, EventArgs eventArgs)
		{
			StackMob.Get ("messages",
				objs =>
				{
					StringBuilder builder = new StringBuilder();
					foreach (var obj in objs)
						builder.AppendLine (GetStringForDictionary (obj));

					Display (builder.ToString());
				},
				ex => Display (ex.ToString()));
		}

		private void OnUpdateClicked (object sender, EventArgs eventArgs)
		{
			if (this.id == null)
			{
				Toast.MakeText (this, "Tab Create First", ToastLength.Long).Show();
				return;
			}

			StackMob.Update ("messages", this.id, new Dictionary<string, object>
			{
				{ "message", "Second hello to the world!" }
			},
			dict => Display (dict),
			ex => Display (ex.ToString()));
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
	}
}