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
using System.Linq;
using System.Net;
using System.Text;
using ServiceStack.Text;
using SimpleOAuth;

namespace StackMob
{
	public delegate void ThirdPartyLoginSuccess (string username, IDictionary<string, object> thirdPartyInfo);

	public enum OAuthVersion
	{
		/// <summary>
		/// OAuth 1
		/// </summary>
		One = 1,

		/// <summary>
		/// OAuth 2
		/// </summary>
		Two = 2
	}

	/// <summary>
	/// A class providing a client interface to the StackMob.com APIs.
	/// </summary>
	public partial class StackMobClient
	{
		/// <summary>
		/// Creates and initializes a new instance of the <see cref="StackMobClient"/> class;
		/// </summary>
		/// <param name="apiVersion">The version of your API to use.</param>
		/// <param name="apiKey">Your StackMob API key.</param>
		/// <exception cref="ArgumentException">
		/// <para><paramref name="apiVersion"/> is &lt; 0.</para>
		/// <para>-- or --</para>
		/// <para><paramref name="apiKey"/> is an empty <c>string</c>.</para>
		/// </exception>
		/// <exception cref="ArgumentNullException"><paramref name="apiKey"/> is <c>null.</c></exception>
		public StackMobClient (int apiVersion, string apiKey)
		{
			if (apiVersion < 0)
				throw new ArgumentException ("API Version must be 0 or greater", "apiVersion");
			CheckArgument (apiKey, "apiKey");

			this.apiKey = apiKey;
			this.accepts = "application/vnd.stackmob+json; version=" + apiVersion;
		}

		/// <summary>
		/// Creates and initializes a new instance of the <see cref="StackMobClient"/> class;
		/// </summary>
		/// <param name="oauth">The version of OAuth to use.</param>
		/// <param name="apiVersion">The version of your API to use.</param>
		/// <param name="apiKey">Your StackMob API key.</param>
		/// <param name="apiSecret">Your StackMob API secret.</param>
		/// <exception cref="ArgumentException">
		/// <para><paramref name="apiVersion"/> is &lt; 0.</para>
		/// <para>-- or --</para>
		/// <para><paramref name="apiKey"/> or <paramref name="apiSecret"/> are empty <c>strings</c>.</para>
		/// <para>-- or --</para>
		/// <para><paramref name="oauth"/> is not a valid member of <see cref="OAuthVersion"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentNullException"><paramref name="apiKey"/> or <paramref name="apiSecret"/> is <c>null.</c></exception>
		public StackMobClient (OAuthVersion oauth, int apiVersion, string apiKey, string apiSecret)
			: this (apiVersion, apiKey)
		{
			if (!Enum.IsDefined (typeof(OAuthVersion), oauth))
				throw new ArgumentOutOfRangeException ("oauth");
			
			CheckArgument (apiSecret, "apiSecret");

			this.oauthv = oauth;
			this.apiSecret = apiSecret;
			this.apiKey = apiKey;
		}

		/// <summary>
		/// Creates and initializes a new instance of the <see cref="StackMobClient"/> class;
		/// </summary>
		/// <param name="oauth">The version of OAuth to use.</param>
		/// <param name="apiVersion">The version of your API to use.</param>
		/// <param name="apiKey">Your StackMob API key.</param>
		/// <param name="apiSecret">Your StackMob API secret.</param>
		/// <param name="apiHost">The host (stackmob.com) to use to access the API.</param>
		/// <param name="userSchema">The name of the user schema.</param>
		/// <param name="usernameField">The name of the username field.</param>
		/// <param name="passwordField">The name of the user password field.</param>
		/// <exception cref="ArgumentException">
		/// <para><paramref name="apiVersion"/> is &lt; 0.</para>
		/// <para>-- or --</para>
		/// <para><paramref name="apiKey"/>, <paramref name="apiSecret"/>, <paramref name="apiHost"/>, <paramref name="userSchema"/>
		/// <paramref name="usernameField"/> or <paramref name="passwordField"/> are empty <c>strings</c>.</para>
		/// <para>-- or --</para>
		/// <para><paramref name="oauth"/> is not a valid member of <see cref="OAuthVersion"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="apiKey"/>, <paramref name="apiSecret"/>, <paramref name="apiHost"/>, <paramref name="userSchema"/>,
		/// <paramref name="usernameField"/> or <paramref name="passwordField"/> is <c>null.</c>
		/// </exception>
		public StackMobClient (OAuthVersion oauth, int apiVersion, string apiKey, string apiSecret, string apiHost, string userSchema, string usernameField, string passwordField)
			: this (oauth, apiVersion, apiKey, apiSecret)
		{
			CheckArgument (apiHost, "apiHost");
			CheckArgument (userSchema, "userSchema");
			CheckArgument (usernameField, "usernameField");
			CheckArgument (passwordField, "passwordField");

			this.oauthv = oauth;
			this.apiKey = apiKey;
			this.apiHost = apiHost;
			this.userSchema = userSchema;
			this.usernameField = usernameField;
		}

		/// <summary>
		/// Gets whether there is a user logged in or not.
		/// </summary>
		public bool IsLoggedIn
		{
			get { return (DateTime.Now.Subtract (this.loginTime) < TimeSpan.FromMinutes (30)); }
		}

		/// <summary>
		/// Gets the username of the currently logged in user.
		/// </summary>
		public string LoggedInUser
		{
			get { return this.loginUsername; }
		}

		/// <summary>
		/// Create an object with a set of keys and values.
		/// </summary>
		/// <param name="type">The type of object (the schema) to create.</param>
		/// <param name="values">A dictionary of columns and values.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="type" />, <paramref name="values"/>, <paramref name="success"/>
		///  or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException"><paramref name="type"/> is an empty string.</exception>
		public void Create (string type, IDictionary<string, object> values, Action<IDictionary<string, object>> success, Action<Exception> failure)
		{
			Create<IDictionary<string, object>> (type, values, success, failure);
		}

		/// <summary>
		/// Creates an object of parentType <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The parentType of object to create.</typeparam>
		/// <param name="type">The name of the schema the parentType is stored in.</param>
		/// <param name="value">The object to store.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="type" />, <paramref name="success"/> or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException"><paramref name="type"/> is an empty string.</exception>
		public void Create<T> (string type, T value, Action<T> success, Action<Exception> failure)
		{
			CheckArgument (type, "type");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "POST");

			Execute (req,
				s => JsonSerializer.SerializeToStream (value, s),
				s =>
				{
					T result = JsonSerializer.DeserializeFromStream<T> (s);
					success (result);
				},
				failure);
		}

		/// <summary>
		/// Creates objects of parentType <typeparamref name="T"/> in relation to <paramref name="parentId"/>.
		/// </summary>
		/// <typeparam name="T">The parentType of objects to create.</typeparam>
		/// <param name="parentType">The parentType (schema) of the parent parentType.</param>
		/// <param name="parentId">The id of the parent object.</param>
		/// <param name="field">The relationship or array field.</param>
		/// <param name="items">The items to create.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="field"/>,
		/// <paramref name="items"/>, <paramref name="success"/> or <paramref name="failure"/> are <c>null</c>
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para>
		/// <paramref name="parentType"/>, <paramref name="parentId"/> or <paramref name="field"/>
		/// are empty strings.
		/// </para>
		/// </exception>
		public void CreateRelated<T> (string parentType, string parentId, string field, IEnumerable<T> items, Action<IEnumerable<string>> success, Action<Exception> failure)
		{
			CheckArgument (parentType, "parentType");
			CheckArgument (parentId, "parentId");
			CheckArgument (field, "field");
			if (items == null)
				throw new ArgumentNullException ("items");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (parentType, "POST", id: parentId + "/" + field);
			Execute (req,
				s => JsonSerializer.SerializeToStream (items, s),
				s =>
				{
					string contents;
					using (StreamReader reader = new StreamReader (s))
						contents = reader.ReadToEnd();

					JsonObject jobj = JsonSerializer.DeserializeFromString<JsonObject> (contents);

					if (jobj.ContainsKey ("succeeded"))
						success (JsonSerializer.DeserializeFromString<IEnumerable<string>> (jobj["succeeded"]));
					else
					{
						GetPrimaryKey (parentType, field,
							key => success (new[] { jobj [key] }),
							failure);
					}
				},
				failure);
		}

		/// <summary>
		/// Updates an existing object with the given column/value dictionary.
		/// </summary>
		/// <param name="type">The parentType (schema) of object to update.</param>
		/// <param name="id">The id of the object to update.</param>
		/// <param name="values">A dictionary of columns to values.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="type" />, <paramref name="id"/>, <paramref name="values"/>,
		/// <paramref name="success"/> or <paramref name="failure"/> are <c>null</c>
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/>, <paramref name="id"/> are empty strings.
		/// </exception>
		public void Update (string type, string id, IDictionary<string, object> values, Action<IDictionary<string, object>> success, Action<Exception> failure)
		{
			Update<IDictionary<string, object>> (type, id, values, success, failure);
		}

		/// <summary>
		/// Updates an existing object.
		/// </summary>
		/// <typeparam name="T">The parentType of object to update.</typeparam>
		/// <param name="type">The parentType (schema) of object to update.</param>
		/// <param name="id">The id of the object to update.</param>
		/// <param name="value">The latest representation of the object.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="type" />, <paramref name="id"/>, <paramref name="success"/>
		/// or <paramref name="failure"/> are <c>null</c>
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/>, <paramref name="id"/> are empty strings.
		/// </exception>
		public void Update<T> (string type, string id, T value, Action<T> success, Action<Exception> failure)
		{
			CheckArgument (type, "type");
			CheckArgument (id, "id");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "PUT", id: id);

			Execute (req,
				s => JsonSerializer.SerializeToStream (value, s),
				s => success (JsonSerializer.DeserializeFromStream<T> (s)),
				failure);
		}

		/// <summary>
		/// Increments a <paramref name="field"/> by one on an object.
		/// </summary>
		/// <param name="type">The parentType (schema) of object to update.</param>
		/// <param name="id">The id of the object to update.</param>
		/// <param name="field">The field to increment.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="type" />, <paramref name="id"/>, <paramref name="field"/>,
		/// <paramref name="success"/> or <paramref name="failure"/> are <c>null</c>
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/>, <paramref name="id"/> or <paramref name="field"/> are empty strings.
		/// </exception>
		public void Increment (string type, string id, string field, Action success, Action<Exception> failure)
		{
			AdjustCore (type, id, field, 1, success, failure);
		}

		/// <summary>
		/// Decrements a <paramref name="field"/> by one on an object.
		/// </summary>
		/// <param name="type">The parentType (schema) of object to update.</param>
		/// <param name="id">The id of the object to update.</param>
		/// <param name="field">The field to increment.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="type" />, <paramref name="id"/>, <paramref name="field"/>,
		/// <paramref name="success"/> or <paramref name="failure"/> are <c>null</c>
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/>, <paramref name="id"/> or <paramref name="field"/> are empty strings.
		/// </exception>
		public void Decrement (string type, string id, string field, Action success, Action<Exception> failure)
		{
			AdjustCore (type, id, field, -1, success, failure);
		}

		/// <summary>
		/// Appends <paramref name="values"/> to an array <paramref name="field"/> of an existing object.
		/// </summary>
		/// <typeparam name="T">The parentType of object to update.</typeparam>
		/// <param name="parentType">The parent parentType (schema) to update.</param>
		/// <param name="parentId">The id of the parent object to update.</param>
		/// <param name="field">The array field.</param>
		/// <param name="values">The values to append.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="success"/>,
		/// <paramref name="values"/> or <paramref name="failure"/> are <c>null</c>
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> are empty strings.
		/// </exception>
		public void Append<T> (string parentType, string parentId, string field, IEnumerable<string> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Appends <paramref name="values"/> to an array <paramref name="field"/> of an existing object.
		/// </summary>
		/// <typeparam name="T">The parentType of object to update.</typeparam>
		/// <param name="parentType">The parent parentType (schema) to update.</param>
		/// <param name="parentId">The id of the parent object to update.</param>
		/// <param name="field">The array field.</param>
		/// <param name="values">The values to append.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="success"/>,
		/// <paramref name="values"/> or <paramref name="failure"/> are <c>null</c>
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> are empty strings.
		/// </exception>
		public void Append<T> (string parentType, string parentId, string field, IEnumerable<int> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Appends <paramref name="values"/> to an array <paramref name="field"/> of an existing object.
		/// </summary>
		/// <typeparam name="T">The parentType of object to update.</typeparam>
		/// <param name="parentType">The parent parentType (schema) to update.</param>
		/// <param name="parentId">The id of the parent object to update.</param>
		/// <param name="field">The array field.</param>
		/// <param name="values">The values to append.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="success"/>,
		/// <paramref name="values"/> or <paramref name="failure"/> are <c>null</c>
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> are empty strings.
		/// </exception>
		public void Append<T> (string parentType, string parentId, string field, IEnumerable<long> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Appends <paramref name="values"/> to an array <paramref name="field"/> of an existing object.
		/// </summary>
		/// <typeparam name="T">The parentType of object to update.</typeparam>
		/// <param name="parentType">The parent parentType (schema) to update.</param>
		/// <param name="parentId">The id of the parent object to update.</param>
		/// <param name="field">The array field.</param>
		/// <param name="values">The values to append.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="success"/>,
		/// <paramref name="values"/> or <paramref name="failure"/> are <c>null</c>
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> are empty strings.
		/// </exception>
		public void Append<T> (string parentType, string parentId, string field, IEnumerable<float> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Appends <paramref name="values"/> to an array <paramref name="field"/> of an existing object.
		/// </summary>
		/// <typeparam name="T">The parentType of object to update.</typeparam>
		/// <param name="parentType">The parent parentType (schema) to update.</param>
		/// <param name="parentId">The id of the parent object to update.</param>
		/// <param name="field">The array field.</param>
		/// <param name="values">The values to append.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="success"/>,
		/// <paramref name="values"/> or <paramref name="failure"/> are <c>null</c>
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> are empty strings.
		/// </exception>
		public void Append<T> (string parentType, string parentId, string field, IEnumerable<double> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Appends <paramref name="values"/> to an array <paramref name="field"/> of an existing object.
		/// </summary>
		/// <typeparam name="T">The parentType of object to update.</typeparam>
		/// <param name="parentType">The parent parentType (schema) to update.</param>
		/// <param name="parentId">The id of the parent object to update.</param>
		/// <param name="field">The array field.</param>
		/// <param name="values">The values to append.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <para>
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="success"/>,
		/// <paramref name="values"/> or <paramref name="failure"/> are <c>null</c>.
		/// </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> are empty strings.
		/// </exception>
		public void Append<T> (string parentType, string parentId, string field, IEnumerable<bool> values, Action<T> success, Action<Exception> failure)
		{
			AppendCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Gets all objects of the given <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The parentType name (schema) to retrieve.</param>
		/// <param name="success">A callback on success, providing an enumerable of stored objects.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="type" />, <paramref name="success"/>, or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/> is an empty string.
		/// </exception>
		public void Get (string type, Action<IEnumerable<IDictionary<string, object>>> success, Action<Exception> failure)
		{
			Get<IDictionary<string, object>> (type, success, failure);
		}

		/// <summary>
		/// Gets all objects of the given <paramref name="type"/>.
		/// </summary>
		/// <typeparam name="T">The parentType of object to retrieve.</typeparam>
		/// <param name="type">The parentType name (schema) to retrieve.</param>
		/// <param name="success">A callback on success, providing an enumerable of stored objects.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="type" />, <paramref name="success"/>, or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/> is an empty string.
		/// </exception>
		public void Get<T> (string type, Action<IEnumerable<T>> success, Action<Exception> failure)
		{
			CheckArgument (type, "type");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "GET");
			Execute (req,
				s => success (JsonSerializer.DeserializeFromStream<IEnumerable<T>> (s)),
				failure);
		}

		/// <summary>
		/// Gets the object with the given <paramref name="id"/>.
		/// </summary>
		/// <typeparam name="T">The parentType of object to retrieve.</typeparam>
		/// <param name="type">The parentType name (schema) of the object to retrieve.</param>
		/// <param name="id">The id of the object to retrieve.</param>
		/// <param name="success">A callback on success, providing the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="type" />, <paramref name="id"/>, <paramref name="success"/>,
		/// or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/> or <paramref name="id"/> are empty strings.
		/// </exception>
		public void Get<T> (string type, string id, Action<T> success, Action<Exception> failure)
		{
			CheckArgument (type, "type");
			CheckArgument (id, "id");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "GET", id: id);
			Execute (req,
				s => success (JsonSerializer.DeserializeFromStream<T> (s)),
				failure);
		}

		/// <summary>
		/// Gets objects with the given <paramref name="filters"/>.
		/// </summary>
		/// <typeparam name="T">The parentType of object to retrieve.</typeparam>
		/// <param name="type">The parentType name (schema) of the object to retrieve.</param>
		/// <param name="filters">Filters to apply to the query.</param>
		/// <param name="success">A callback on success, providing an enumerable of objects.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="type" />, <paramref name="filters"/>, <paramref name="success"/>,
		/// or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/> is an empty string.
		/// </exception>
		public void Get<T> (string type, IEnumerable<string> filters, Action<IEnumerable<T>> success, Action<Exception> failure)
		{
			CheckArgument (type, "type");
			if (filters == null)
				throw new ArgumentNullException ("filters");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "GET", query: GetQueryForFilters (filters));
			Execute (req,
				s => success (JsonSerializer.DeserializeFromStream<IEnumerable<T>> (s)),
				failure);
		}

		/// <summary>
		/// Gets objects with the given <paramref name="filters"/>.
		/// </summary>
		/// <param name="type">The parentType name (schema) of the object to retrieve.</param>
		/// <param name="filters">Filters to apply to the query.</param>
		/// <param name="success">A callback on success, providing an enumerable of stored objects.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="type" />, <paramref name="filters"/>, <paramref name="success"/>,
		/// or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/> is an empty string.
		/// </exception>
		public void Get (string type, IEnumerable<string> filters, Action<IEnumerable<IDictionary<string, object>>> success, Action<Exception> failure)
		{
			Get<IDictionary<string, object>> (type, filters, success, failure);
		}

		/// <summary>
		/// Gets objects with the given <paramref name="filters"/>.
		/// </summary>
		/// <typeparam name="T">The parentType of object to retrieve.</typeparam>
		/// <param name="type">The parentType name (schema) of the object to retrieve.</param>
		/// <param name="filters">Filters to apply to the query.</param>
		/// <param name="fields">The fields to select.</param>
		/// <param name="success">A callback on success, providing an enumerable of objects.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="type" />, <paramref name="filters"/>, <paramref name="success"/>,
		/// or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/> is an empty string.
		/// </exception>
		public void Get (string type, IEnumerable<string> filters, IEnumerable<string> fields, Action<IEnumerable<IDictionary<string, object>>> success, Action<Exception> failure)
		{
			CheckArgument (type, "type");
			if (filters == null)
				throw new ArgumentNullException ("filters");
			if (fields == null)
				throw new ArgumentNullException ("fields");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "GET", query: GetQueryForFilters (filters), select: GetSelectForFields (fields));
			Execute (req,
				s => success (JsonSerializer.DeserializeFromStream<IEnumerable<IDictionary<string, object>>> (s)),
				failure);
		}

		/// <summary>
		/// Deletes an object with the given id.
		/// </summary>
		/// <param name="type">The parentType name (schema) of the object to delete.</param>
		/// <param name="id">The id of the object to delete.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="type" />, <paramref name="id"/>, <paramref name="success"/>,
		/// or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="type"/> or <paramref name="id"/> are empty strings.
		/// </exception>
		public void Delete (string type, string id, Action success, Action<Exception> failure)
		{
			CheckArgument (type, "type");
			CheckArgument (id, "id");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "DELETE", id: id);
			Execute (req, s => success(), failure);
		}

		/// <summary>
		/// Deletes an element from an array field.
		/// </summary>
		/// <typeparam name="T">The parentType of the parent object.</typeparam>
		/// <param name="parentType">The parent parentType (schema) of the object to delete from.</param>
		/// <param name="parentId">The id of the parent object to delete from.</param>
		/// <param name="field">The array field name.</param>
		/// <param name="values">The values to delete from the <paramref name="field"/>.</param>
		/// <param name="success">A callback on success, returning the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <param name="cascade">Whether to cascade deletes or not (when <paramref name="values"/> contains IDs).</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="field"/>, <paramref name="values"/>
		/// <paramref name="success"/>, or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> or <paramref name="field"/> are empty strings.
		/// </exception>
		public void DeleteFrom<T> (string parentType, string parentId, string field, IEnumerable<string> values, Action<T> success, Action<Exception> failure, bool cascade = false)
		{
			DeleteFromCore (parentType, parentId, field, values, success, failure, cascade);
		}

		/// <summary>
		/// Deletes an element from an array field.
		/// </summary>
		/// <typeparam name="T">The parentType of the parent object.</typeparam>
		/// <param name="parentType">The parent parentType (schema) of the object to delete from.</param>
		/// <param name="parentId">The id of the parent object to delete from.</param>
		/// <param name="field">The array field name.</param>
		/// <param name="values">The values to delete from the <paramref name="field"/>.</param>
		/// <param name="success">A callback on success, returning the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="field"/>, <paramref name="values"/>
		/// <paramref name="success"/>, or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> or <paramref name="field"/> are empty strings.
		/// </exception>
		public void DeleteFrom<T> (string parentType, string parentId, string field, IEnumerable<int> values, Action<T> success, Action<Exception> failure)
		{
			DeleteFromCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Deletes an element from an array field.
		/// </summary>
		/// <typeparam name="T">The parentType of the parent object.</typeparam>
		/// <param name="parentType">The parent parentType (schema) of the object to delete from.</param>
		/// <param name="parentId">The id of the parent object to delete from.</param>
		/// <param name="field">The array field name.</param>
		/// <param name="values">The values to delete from the <paramref name="field"/>.</param>
		/// <param name="success">A callback on success, returning the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="field"/>, <paramref name="values"/>
		/// <paramref name="success"/>, or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> or <paramref name="field"/> are empty strings.
		/// </exception>
		public void DeleteFrom<T> (string parentType, string parentId, string field, IEnumerable<long> values, Action<T> success, Action<Exception> failure)
		{
			DeleteFromCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Deletes an element from an array field.
		/// </summary>
		/// <typeparam name="T">The parentType of the parent object.</typeparam>
		/// <param name="parentType">The parent parentType (schema) of the object to delete from.</param>
		/// <param name="parentId">The id of the parent object to delete from.</param>
		/// <param name="field">The array field name.</param>
		/// <param name="values">The values to delete from the <paramref name="field"/>.</param>
		/// <param name="success">A callback on success, returning the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="field"/>, <paramref name="values"/>
		/// <paramref name="success"/>, or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> or <paramref name="field"/> are empty strings.
		/// </exception>
		public void DeleteFrom<T> (string parentType, string parentId, string field, IEnumerable<float> values, Action<T> success, Action<Exception> failure)
		{
			DeleteFromCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Deletes an element from an array field.
		/// </summary>
		/// <typeparam name="T">The parentType of the parent object.</typeparam>
		/// <param name="parentType">The parent parentType (schema) of the object to delete from.</param>
		/// <param name="parentId">The id of the parent object to delete from.</param>
		/// <param name="field">The array field name.</param>
		/// <param name="values">The values to delete from the <paramref name="field"/>.</param>
		/// <param name="success">A callback on success, returning the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="field"/>, <paramref name="values"/>
		/// <paramref name="success"/>, or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> or <paramref name="field"/> are empty strings.
		/// </exception>
		public void DeleteFrom<T> (string parentType, string parentId, string field, IEnumerable<double> values, Action<T> success, Action<Exception> failure)
		{
			DeleteFromCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Deletes an element from an array field.
		/// </summary>
		/// <typeparam name="T">The parentType of the parent object.</typeparam>
		/// <param name="parentType">The parent parentType (schema) of the object to delete from.</param>
		/// <param name="parentId">The id of the parent object to delete from.</param>
		/// <param name="field">The array field name.</param>
		/// <param name="values">The values to delete from the <paramref name="field"/>.</param>
		/// <param name="success">A callback on success, returning the stored object.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="parentType" />, <paramref name="parentId"/>, <paramref name="field"/>, <paramref name="values"/>
		/// <paramref name="success"/>, or <paramref name="failure"/> are <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="parentType"/>, <paramref name="parentId"/> or <paramref name="field"/> are empty strings.
		/// </exception>
		public void DeleteFrom<T> (string parentType, string parentId, string field, IEnumerable<bool> values, Action<T> success, Action<Exception> failure)
		{
			DeleteFromCore (parentType, parentId, field, values, success, failure);
		}

		/// <summary>
		/// Creates a new user linked with a Facebook account.
		/// </summary>
		/// <param name="username">The username for the new user.</param>
		/// <param name="facebookAccessToken">The Facebook access token.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="username"/>, <paramref name="facebookAccessToken"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		/// <exception cref="ArgumentException"><paramref name="username"/> or <paramref name="facebookAccessToken"/> are empty strings.</exception>
		public void CreateUserWithFacebook (string username, string facebookAccessToken, Action success, Action<Exception> failure)
		{
			CheckArgument (username, "username");
			CheckArgument (facebookAccessToken, "facebookAccessToken");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var args = new Dictionary<string, string>();
			args["username"] = username;
			args["fb_at"] = facebookAccessToken;

			var req = GetRequest (this.userSchema + "/createUserWithFacebook", "GET", query: GetQueryForArguments (args));
			Execute (req,
				s => success(),
				failure);
		}

		/// <summary>
		/// Logs in with a Facebook account.
		/// </summary>
		/// <param name="facebookAccessToken">The Facebook access token.</param>
		/// <param name="success">A callback on success, providing the user information.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="facebookAccessToken"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		/// <exception cref="ArgumentException"><paramref name="facebookAccessToken"/> is an empty string.</exception>
		public void LoginWithFacebook (string facebookAccessToken, ThirdPartyLoginSuccess success, Action<Exception> failure)
		{
			CheckArgument (facebookAccessToken, "facebookAccessToken");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var args = new Dictionary<string, string>();
			args["fb_at"] = facebookAccessToken;

			var req = GetRequest (this.userSchema + "/facebookLogin", "GET", query: GetQueryForArguments (args));
			Execute (req,
				s =>
				{
					JsonObject jobj = JsonSerializer.DeserializeFromStream<JsonObject> (s);
					var fb = JsonSerializer.DeserializeFromString<IDictionary<string, object>> (jobj ["fb"]);
					success (jobj ["username"], fb);
				},
				failure);
		}

		/// <summary>
		/// Links the currently logged in user to a Facebook account.
		/// </summary>
		/// <param name="facebookAccessToken">The Facebook access token.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="facebookAccessToken"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		/// <exception cref="ArgumentException"><paramref name="facebookAccessToken"/> is an empty string.</exception>
		public void LinkAccountToFacebook (string facebookAccessToken, Action success, Action<Exception> failure)
		{
			CheckArgument (facebookAccessToken, "facebookAccessToken");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var args = new Dictionary<string, string>();
			args ["fb_at"] = facebookAccessToken;

			var req = GetRequest (this.userSchema + "/linkUserWithFacebook", "GET", query: GetQueryForArguments (args));
			Execute (req,
				s => success(),
				failure);
		}

		/// <summary>
		/// Gets the user's Facebook information.
		/// </summary>
		/// <param name="success">A callback on success, providing the user information.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		public void GetFacebookUserInfo (Action<IDictionary<string, object>> success, Action<Exception> failure)
		{
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (this.userSchema + "/getFacebookUserInfo", "GET");
			Execute (req,
				s => success (JsonSerializer.DeserializeFromStream<IDictionary<string, object>> (s)),
				failure);
		}

		/// <summary>
		/// Posts a message to Facebook.
		/// </summary>
		/// <param name="message">The message to post.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		/// <exception cref="ArgumentException"><paramref name="message"/> is an empty string.</exception>
		public void PostToFacebook (string message, Action success, Action<Exception> failure)
		{
			CheckArgument (message, "message");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var args = new Dictionary<string, string>();
			args["message"] = message;

			var req = GetRequest (this.userSchema + "/postFacebookMessage", "GET", query: GetQueryForArguments (args));
			Execute (req,
				s => success(),
				failure);
		}

		/// <summary>
		/// Creates a new user associated with a Twitter account.
		/// </summary>
		/// <param name="username">The username of the user to create.</param>
		/// <param name="twitterToken">The Twitter access token.</param>
		/// <param name="twitterSecret">The Twitter access secret.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="username"/>, <paramref name="twitterToken"/>, <paramref name="twitterSecret"/>,
		/// <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c> </exception>
		/// <exception cref="ArgumentException"><paramref name="username"/>, <paramref name="twitterToken"/> or <paramref name="twitterSecret"/> are empty strings.</exception>
		public void CreateUserWithTwitter (string username, string twitterToken, string twitterSecret, Action success, Action<Exception> failure)
		{
			CheckArgument (username, "username");
			CheckArgument (twitterToken, "twitterToken");
			CheckArgument (twitterSecret, "twitterSecret");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var args = new Dictionary<string, string>();
			args["tw_tk"] = twitterToken;
			args["tw_ts"] = twitterSecret;
			args["username"] = username;

			var req = GetRequest (this.userSchema + "/createuserWithTwitter", "GET", query: GetQueryForArguments (args));
			Execute (req,
				s => success(),
				failure);
		}

		/// <summary>
		/// Logs in with a Twitter account.
		/// </summary>
		/// <param name="twitterToken">The Twitter access token.</param>
		/// <param name="twitterSecret">The Twitter access secret.</param>
		/// <param name="success">A callback on success, providing the user's information.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="twitterToken"/>, <paramref name="twitterSecret"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c> </exception>
		/// <exception cref="ArgumentException"><paramref name="twitterToken"/> or <paramref name="twitterSecret"/> are empty strings.</exception>
		public void LoginWithTwitter (string twitterToken, string twitterSecret, ThirdPartyLoginSuccess success, Action<Exception> failure)
		{
			CheckArgument (twitterToken, "twitterToken");
			CheckArgument (twitterSecret, "twitterSecret");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var args = new Dictionary<string, string>();
			args["tw_tk"] = twitterToken;
			args["tw_ts"] = twitterSecret;

			var req = GetRequest (this.userSchema + "/twitterLogin", "GET", query: GetQueryForArguments (args));
			Execute (req,
				s =>
				{
					JsonObject jobj = JsonSerializer.DeserializeFromStream<JsonObject> (s);
					var tw = JsonSerializer.DeserializeFromString<IDictionary<string, object>> (jobj ["tw"]);
					success (jobj ["username"], tw);
				},
				failure);
		}

		/// <summary>
		/// Links an existing account to a Twitter account.
		/// </summary>
		/// <param name="twitterToken">The Twitter access token.</param>
		/// <param name="twitterSecret">The Twitter access secret.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="twitterToken"/>, <paramref name="twitterSecret"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c> </exception>
		/// <exception cref="ArgumentException"><paramref name="twitterToken"/> or <paramref name="twitterSecret"/> are empty strings.</exception>
		public void LinkAccountToTwitter (string twitterToken, string twitterSecret, Action success, Action<Exception> failure)
		{
			CheckArgument (twitterToken, "twitterToken");
			CheckArgument (twitterSecret, "twitterSecret");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var args = new Dictionary<string, string>();
			args["tw_tk"] = twitterToken;
			args["tw_ts"] = twitterSecret;

			var req = GetRequest (this.userSchema + "/linkUserWithTwitter", "GET", query: GetQueryForArguments (args));
			Execute (req,
				s => success(),
				failure);
		}

		/// <summary>
		/// Gets the logged in user's Twitter information.
		/// </summary>
		/// <param name="success">A callback on success, providing the user's information.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="success"/> or <paramref name="failure"/> is <c>null.</c> </exception>
		public void GetTwitterUserInfo (Action<IDictionary<string, object>> success, Action<Exception> failure)
		{
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (this.userSchema + "/getTwitterUserInfo", "GET");
			Execute (req,
				s => success (JsonSerializer.DeserializeFromStream<IDictionary<string, object>> (s)),
				failure);
		}

		/// <summary>
		/// Posts a message to Twitter.
		/// </summary>
		/// <param name="contents">The message to post to twitter.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="contents"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c> </exception>
		/// <exception cref="ArgumentException"><paramref name="contents"/> is an empty string.</exception>
		public void PostToTwitter (string contents, Action success, Action<Exception> failure)
		{
			CheckArgument (contents, "contents");

			var args = new Dictionary<string, string>();
			args ["tw_st"] = contents;

			var req = GetRequest (this.userSchema + "/twitterStatusUpdate", "GET", query: GetQueryForArguments (args));
			Execute (req,
				s => success(),
				failure);
		}

		/// <summary>
		/// Logs in the user with the given <paramref name="arguments"/>.
		/// </summary>
		/// <param name="arguments">The arguments to login with.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="arguments"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c> </exception>
		public void Login (IDictionary<string, string> arguments, Action success, Action<Exception> failure)
		{
			if (arguments == null)
				throw new ArgumentNullException ("arguments");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			this.loginUsername = null;

			GetPrimaryKey (this.userSchema,
				key =>
				{
					this.usernameField = key;
					
					var req = GetRequest (this.userSchema + "/login", "GET", query: GetQueryForArguments (arguments));
					Execute (req,
						s =>
						{
							this.loginUsername = arguments [key];
							this.loginTime = DateTime.Now;
							success();
						},
						failure);
				},

				failure);
		}

		private static Action<Exception> PassThroughFailure (Action<Exception> failure)
		{
			return ex =>
			{
				if (!(ex is WebException))
				{
					failure (ex);
					return;
				}

				try
				{
					var wex = ((WebException)ex);
					Stream stream = wex.Response.GetResponseStream();
					JsonObject obj = JsonSerializer.DeserializeFromStream<JsonObject> (stream);

					failure (new WebException (ex.Message, new Exception (GetStringForJson (obj)), wex.Status, wex.Response));
				}
				catch (Exception)
				{
					failure (ex);
				}
			};
		}

		private static string GetStringForJson (JsonObject obj)
		{
			StringBuilder messageBuilder = new StringBuilder();
			foreach (var kvp in obj)
			{
				messageBuilder.Append (kvp.Key);
				messageBuilder.Append (": ");
				messageBuilder.AppendLine (kvp.Value);
			}

			return messageBuilder.ToString();
		}

		/// <summary>
		/// Sends a forgotten password email to the user.
		/// </summary>
		/// <param name="username">The username to send the email for.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="username"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		/// <exception cref="ArgumentException"><paramref name="username"/> is an empty string.</exception>
		public void ForgotPassword (string username, Action success, Action<Exception> failure)
		{
			CheckArgument (username, "username");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var args = new Dictionary<string, string>();
			args ["username"] = username;

			var req = GetRequest (this.userSchema + "/forgotPassword", "GET", query: GetQueryForArguments (args));
			Execute (req,
				s => success(),
				failure);
		}

		/// <summary>
		/// Logs out the current user.
		/// </summary>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		/// <remarks>If you've not previously logged in, <paramref name="success"/> will be called.</remarks>
		public void Logout (Action success, Action<Exception> failure)
		{
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");
			if (this.loginUsername == null)
			{
				success();
				return;
			}

			var args = new Dictionary<string, string>();
			args[this.usernameField] = this.loginUsername;
			this.loginUsername = null;
			this.loginTime = default(DateTime);

			var req = GetRequest (this.userSchema + "/logout", "GET", query: GetQueryForArguments (args));
			Execute (req,
				s => success(),
				failure);

			this.cookieJar = new CookieContainer();
		}

		/// <summary>
		/// Sends a push notification with the supplied <paramref name="payload"/>.
		/// </summary>
		/// <param name="payload">The payload to send with the notification.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="payload"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		public void Push (PushPayload payload, Action success, Action<Exception> failure)
		{
			Push (payload, new Dictionary<string, object>(), success, failure);
		}

		/// <summary>
		/// Sends a push notification with the supplied <paramref name="payload"/> to specific user <paramref name="ids"/>.
		/// </summary>
		/// <param name="payload">The payload to send with the notification.</param>
		/// <param name="ids">The user IDs to send the notification to.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="payload"/>, <paramref name="ids"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		public void Push (PushPayload payload, IEnumerable<string> ids, Action success, Action<Exception> failure)
		{
			if (ids == null)
				throw new ArgumentNullException ("ids");

			var values = new Dictionary<string, object>();
			values ["userIds"] = ids;

			Push (payload, values, success, failure);
		}

		/// <summary>
		/// Sends a push notification with the supplied <paramref name="payload"/> to specific <paramref name="tokens"/>.
		/// </summary>
		/// <param name="payload">The payload to send with the notification.</param>
		/// <param name="tokens">The push notification tokens to send to.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="payload"/>, <paramref name="tokens"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		public void Push (PushPayload payload, IEnumerable<PushToken> tokens, Action success, Action<Exception> failure)
		{
			if (tokens == null)
				throw new ArgumentNullException ("tokens");

			var values = new Dictionary<string, object>();
			values ["tokens"] = tokens.Select (t => t.ToJsonObject()).ToJson();

			Push (payload, values, success, failure);
		}

		private DateTime loginTime;
		private string loginUsername;

		private CookieContainer cookieJar = new CookieContainer();
		private readonly string accepts;

		private readonly string apiKey;
		private readonly string apiSecret;
		private readonly string apiHost = "stackmob.com";
		private readonly string userSchema = "user";
		private string usernameField;

		private JsonObject apis;
		private readonly OAuthVersion oauthv = OAuthVersion.Two;

		private void GetApis (Action<JsonObject> success, Action<Exception> failure)
		{
			if (this.apis == null)
			{
				var req = GetRequest ("listapi", "GET");
				Execute (req,
						s =>
						{
							this.apis = JsonSerializer.DeserializeFromStream<JsonObject> (s);
							success (this.apis);
						},
						failure);
			}
			else
			{
				success (this.apis);
			}
		}

		private void AdjustCore (string type, string id, string field, int value, Action success, Action<Exception> failure)
		{
			CheckArgument (type, "type");
			CheckArgument (id, "id");
			CheckArgument (field, "field");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (type, "PUT", id: id);

			var dict = new Dictionary<string, object> { { field + "[inc]", value } };

			Execute (req,
				s => JsonSerializer.SerializeToStream (dict, s),
				s => success(),
				failure);
		}

		private void Push (PushPayload payload, IDictionary<string, object> target, Action success, Action<Exception> failure)
		{
			if (payload == null)
				throw new ArgumentNullException ("payload");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			target ["kvPairs"] = payload;

			var req = GetPushRequest ("push_users_universal", "POST");
			Execute (req,
				s => JsonSerializer.SerializeToStream (target, s),
				s => success(),
				failure);
		}

		private void RegisterPush (string username, PushToken token, Action success, Action<Exception> failure)
		{
			CheckArgument (username, "username");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var jobj = new Dictionary<string, object>();
			jobj["userId"] = username;
			jobj["token"] = token.ToJsonObject();

			var req = GetPushRequest ("register_device_token_universal", "POST");
			Execute (req,
				s => JsonSerializer.SerializeToStream (jobj, s),
				s => success(),
				failure);
		}

		private void DeleteFromCore<TValue, TResult> (string parentType, string parentId, string field, IEnumerable<TValue> values, Action<TResult> success, Action<Exception> failure, bool cascade = false)
		{
			CheckArgument (parentType, "parentType");
			CheckArgument (parentId, "parentId");
			CheckArgument (field, "field");
			if (values == null)
				throw new ArgumentNullException ("values");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			StringBuilder ids = new StringBuilder();
			foreach (TValue value in values)
			{
				if (value != null)
					ids.Append (value.ToString());
			}

			var req = GetRequest (parentType, "DELETE", id: parentId + "/" + ids);
			if (cascade)
				req.Headers["X-StackMob-CascadeDelete"] = "true";

			Execute (req,
				s => success (JsonSerializer.DeserializeFromStream<TResult> (s)),
				failure);
		}

		private void AppendCore<TValue,TResult> (string parentType, string parentId, string field, IEnumerable<TValue> values, Action<TResult> success, Action<Exception> failure)
		{
			CheckArgument (parentType, "parentType");
			CheckArgument (parentId, "parentId");
			CheckArgument (field, "field");
			if (values == null)
				throw new ArgumentNullException ("values");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			var req = GetRequest (parentType, "PUT", id: parentId + "/" + field);
			Execute (req,
				s => JsonSerializer.SerializeToStream (values, s),
				s => success (JsonSerializer.DeserializeFromStream<TResult> (s)),
				failure);
		}

		private static void CheckArgument (string value, string name)
		{
			if (value == null)
				throw new ArgumentNullException (name);
			if (value.Trim() == String.Empty)
				throw new ArgumentException (name + " can not be empty", name);
		}

		private static string FindIdentityColumn (JsonObject properties)
		{
			string primaryKey = null;
			foreach (string key in properties.Keys)
			{
				JsonObject column = properties.Object (key);

				string value;
				if (column.TryGetValue ("identity", out value) && value.ToLower() == "true")
				{
					primaryKey = key;
					break;
				}
			}

			return primaryKey;
		}

		private void GetPrimaryKey (string type, Action<string> success, Action<Exception> failure)
		{
			GetPrimaryKey (type, null, success, failure);
		}

		private void GetPrimaryKey (string type, string field, Action<string> success, Action<Exception> failure)
		{
			if (this.usernameField != null)
				success (this.usernameField);

			GetApis (apis =>
			{
				if (!apis.ContainsKey (type))
					throw new Exception ("API not found for " + type);

				JsonObject properties = apis.Object (type).Object ("properties");
				if (field != null)
				{
					string refType = properties.Object (field) ["$ref"];
					properties = apis.Object (refType).Object ("properties");
				}

				string primaryKey = FindIdentityColumn (properties);
				if (primaryKey == null)
					failure (new Exception ("Primary key not found for " + type));

				success (primaryKey);
			}, failure);
		}

		private string GetQueryForFilters (IEnumerable<string> filters)
		{
			StringBuilder builder = new StringBuilder();
			foreach (string filter in filters)
			{
				if (builder.Length > 0)
					builder.Append ("&");

				builder.Append (Uri.EscapeDataString (filter));
			}

			return builder.ToString();
		}

		private string GetQueryForArguments (IEnumerable<KeyValuePair<string, string>> arguments)
		{
			StringBuilder builder = new StringBuilder();
			foreach (var arg in arguments)
			{
				if (builder.Length > 0)
					builder.Append ("&");

				builder.Append (Uri.EscapeUriString (arg.Key));
				builder.Append ("=");
				builder.Append (Uri.EscapeUriString (arg.Value));
			}

			return builder.ToString();
		}

		private string GetSelectForFields (IEnumerable<string> fields)
		{
			StringBuilder builder = new StringBuilder();
			foreach (string field in fields)
			{
				if (builder.Length > 0)
					builder.Append (",");

				builder.Append (field);
			}

			return builder.ToString();
		}

		private void Execute (HttpWebRequest request, Action<Stream> success, Action<Exception> failure)
		{
			try
			{
				request.BeginGetResponse (resResult =>
				{
					HttpWebResponse response;
					try
					{
						response = (HttpWebResponse) request.EndGetResponse (resResult);

						using (Stream s = response.GetResponseStream())
							success (s);
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

		private void Execute (HttpWebRequest request, Action<Stream> send, Action<Stream> success, Action<Exception> failure)
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

						Execute (request, success, failure);
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

		private HttpWebRequest GetRequest (string resource, string method, string id = "", string query = "", string select = "")
		{
			return GetRequest ("api", resource, method, id, query, select);
		}

		private HttpWebRequest GetPushRequest (string resource, string method, string id = "", string query = "")
		{
			return GetRequest ("push", resource, method, id, query);
		}

		private HttpWebRequest GetRequest (string subdomain, string resource, string method, string id = "", string query = "", string select = "")
		{
			string url = "https://" + subdomain + "." + this.apiHost + "/" + resource;
			if (!String.IsNullOrWhiteSpace (id))
				url += "/" + id;

			if (!String.IsNullOrWhiteSpace (query))
				url += "?" + query;

			var request = (HttpWebRequest)WebRequest.Create (url);
			request.CookieContainer = this.cookieJar;
			request.Method = method;
			request.Accept = this.accepts;

			if (!String.IsNullOrWhiteSpace (select))
				request.Headers["X-StackMob-Select"] = select;

			request.SignRequest (new Tokens
			{
				ConsumerKey = this.apiKey,
				ConsumerSecret = this.apiSecret
			}).InHeader();

			return request;
		}
	}
}
