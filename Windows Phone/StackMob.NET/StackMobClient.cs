//
// StackMobClient.cs
//
// Copyright 2012 Xamarin, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using OAuth;
using ServiceStack.Text;

namespace StackMob
{
	public class StackMobClient
	{
		public StackMobClient (string apiKey, string apiSecret, string appname, int apiVersion, string userObjectName = "user")
		{
			if (apiKey == null)
				throw new ArgumentNullException ("apiKey");
			if (apiSecret == null)
				throw new ArgumentNullException ("apiSecret");
			if (apiVersion < 0)
				throw new ArgumentException ("API Version must be 0 or greater", "apiVersion");

			this.apiKey = apiKey;
			this.apiSecret = apiSecret;
			this.userObjectName = userObjectName;
			this.appname = appname;

			this.accepts = "application/vnd.stackmob+json; version=" + apiVersion;
		}

		public void Create (string type, IDictionary<string, object> values, Action<IDictionary<string, object>> success, Action<Exception> failure)
		{
			Create<IDictionary<string, object>> (type, values, success, failure);
		}

		public void Create<T> (string type, T value, Action<T> success, Action<Exception> failure)
		{
			CheckType (type);
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "POST");

			Execute (req, HttpStatusCode.Created,
				s => JsonSerializer.SerializeToStream (value, s),
				s =>
				{
					T result = JsonSerializer.DeserializeFromStream<T> (s);
					success (result);
				},
				failure);
		}

		public void CreateRelated<T> (string type, string id, string field, IEnumerable<T> items, Action<IEnumerable<string>> success, Action<Exception> failure)
		{
			CheckType (type);
			CheckId (id);
			CheckField (field);
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "POST", id + "/" + field);
			Execute (req, HttpStatusCode.OK,
				s => JsonSerializer.SerializeToStream (items, s),
				s =>
				{
					JsonObject jobj = JsonSerializer.DeserializeFromStream<JsonObject> (s);
					success (JsonSerializer.DeserializeFromString<IEnumerable<string>> (jobj ["succeeded"]));
				},
				failure);
		}

		public void Update (string type, string id, IDictionary<string, object> values, Action<IDictionary<string, object>> success, Action<Exception> failure)
		{
			Update<IDictionary<string, object>> (type, id, values, success, failure);
		}

		public void Update<T> (string type, string id, T value, Action<T> success, Action<Exception> failure)
		{
			CheckType (type);
			CheckId (id);
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "PUT", id);

			Execute (req, HttpStatusCode.OK,
				s => JsonSerializer.SerializeToStream (value, s),
				s => success (JsonSerializer.DeserializeFromStream<T> (s)),
				failure);
		}

		public void Append<T> (string type, string id, string field, IEnumerable<string> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (type, id, field, values, success, failure);
		}

		public void Append<T> (string type, string id, string field, IEnumerable<int> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (type, id, field, values, success, failure);
		}

		public void Append<T> (string type, string id, string field, IEnumerable<long> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (type, id, field, values, success, failure);
		}

		public void Append<T> (string type, string id, string field, IEnumerable<float> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (type, id, field, values, success, failure);
		}

		public void Append<T> (string type, string id, string field, IEnumerable<double> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (type, id, field, values, success, failure);
		}

		public void Append<T> (string type, string id, string field, IEnumerable<bool> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (type, id, field, values, success, failure);
		}

		public void Get<T> (string type, Action<IEnumerable<T>> success, Action<Exception> failure)
		{
			CheckType (type);
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "GET");
			Execute (req, HttpStatusCode.OK,
				s =>
				{
					var result = JsonSerializer.DeserializeFromStream<IEnumerable<T>> (s);
					success (result);
				},
				failure);
		}

		public void Get<T> (string type, string id, Action<T> success, Action<Exception> failure)
		{
			CheckType (type);
			CheckId (id);
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "GET", id);
			Execute (req, HttpStatusCode.OK,
				s =>
				{
					T result = JsonSerializer.DeserializeFromStream<T> (s);
					success (result);
				},
				failure);
		}

		public void Delete (string type, string id, Action success, Action<Exception> failure)
		{
			CheckType (type);
			CheckId (id);
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "DELETE", id);
			Execute (req, HttpStatusCode.OK, s => success(), failure);
		}

		private readonly string accepts;

		private readonly string apiKey;
		private readonly string apiSecret;
		private readonly string userObjectName;
		private readonly string appname;

		private void AppendCore<TValue,TResult> (string type, string id, string field, IEnumerable<TValue> values, Action<TResult> success, Action<Exception> failure)
		{
			CheckType (type);
			CheckId (id);
			CheckField (field);
			if (values == null)
				throw new ArgumentNullException ("values");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "PUT", id + "/" + field);
			Execute (req, HttpStatusCode.OK,
				s => JsonSerializer.SerializeToStream (values, s),
				s => success (JsonSerializer.DeserializeFromStream<TResult> (s)),
				failure);
		}

		private static void CheckField (string field)
		{
			if (field == null)
				throw new ArgumentNullException ("field");
			if (field.Trim() == String.Empty)
				throw new ArgumentException ("Can not have an empty field", "field");
		}

		private static void CheckId (string id)
		{
			if (id == null)
				throw new ArgumentNullException ("id");
			if (id.Trim() == String.Empty)
				throw new ArgumentException ("Can not have an empty id", "id");
		}

		private static void CheckType (string type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
			if (type.Trim() == String.Empty)
				throw new ArgumentException ("Can not have an empty type", "type");
		}

		private void Execute (HttpWebRequest request, HttpStatusCode expected, Action<Stream> success, Action<Exception> failure)
		{
			try
			{
				request.BeginGetResponse (resResult =>
				{
					HttpWebResponse response;
					try
					{
						response = (HttpWebResponse) request.EndGetResponse (resResult);
					}
					catch (Exception ex)
					{
						failure (ex);
						return;
					}

					if (response.StatusCode == expected)
					{
						try
						{
							using (Stream s = response.GetResponseStream())
								success (s);
						}
						catch (Exception ex)
						{
							failure (ex);
						}
					}
					else
						failure (new Exception (response.StatusDescription));
				}, null);
			}
			catch (WebException wex)
			{
				failure (wex);
			}
		}

		private void Execute (HttpWebRequest request, HttpStatusCode expected, Action<Stream> send, Action<Stream> success, Action<Exception> failure)
		{
			try
			{
				request.BeginGetRequestStream (reqResult =>
				{
					try
					{
						if (send != null)
						{
							using (Stream s = request.EndGetRequestStream (reqResult))
								send (s);
						}

						Execute (request, expected, success, failure);
					}
					catch (Exception ex)
					{
						failure (ex);
					}
				}, null);
			}
			catch (WebException wex)
			{
				failure (wex);
			}
		}

		private HttpWebRequest GetRequest (string resource, string method, string id = "", string query = "")
		{
			string url = "http://api.mob1.stackmob.com/" + resource;
			if (!String.IsNullOrWhiteSpace (id))
				url += "/" + id;

			if (!String.IsNullOrWhiteSpace (query))
				url += "?" + query;

			var oAuthRequest = new OAuth.OAuthRequest();
			oAuthRequest.RequestUrl = url;
			oAuthRequest.ConsumerKey = this.apiKey;
			oAuthRequest.ConsumerSecret = this.apiSecret;
			oAuthRequest.SignatureMethod = OAuthSignatureMethod.HmacSha1;
			oAuthRequest.Method = method;
			oAuthRequest.Type = OAuthRequestType.ProtectedResource;

			var request = (HttpWebRequest)WebRequest.Create (url);
			request.Method = method;
			request.Accept = this.accepts;
			request.Headers ["Authorization"] = oAuthRequest.GetAuthorizationHeader();

			return request;
		}
	}
}
