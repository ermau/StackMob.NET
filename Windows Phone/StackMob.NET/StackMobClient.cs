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
using System.Threading;
using OAuth;
using ServiceStack.Text;

namespace StackMob
{
	/// <summary>
	/// A class providing a client interface to the StackMob.com APIs.
	/// </summary>
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

		/// <summary>
		/// Create an object with a set of keys and values.
		/// </summary>
		/// <param name="type">The type of object (the schema) to create.</param>
		/// <param name="values">A dictionary of columns and values.</param>
		/// <param name="success">A callback on success, returning the stored object.</param>
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
		/// <param name="success">A callback on success, returning the stored object.</param>
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
		/// <param name="success">A callback on success, returning the stored object.</param>
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

			var req = GetRequest (parentType, "POST", parentId + "/" + field);
			Execute (req,
				s => JsonSerializer.SerializeToStream (items, s),
				s =>
				{
					string contents;
					using (StreamReader reader = new StreamReader (s))
						contents = reader.ReadToEnd();

					JsonObject jobj = JsonSerializer.DeserializeFromString<JsonObject> (contents);

					IEnumerable<string> ids;

					if (jobj.ContainsKey ("succeeded"))
						ids = JsonSerializer.DeserializeFromString<IEnumerable<string>> (jobj["succeeded"]);
					else
					{
						JsonObject apis = GetApis();
						if (!apis.ContainsKey (parentType))
							throw new Exception ("API not found for " + parentType);

						string refType = apis.Object (parentType).Object ("properties").Object (field)["$ref"];

						JsonObject properties = apis.Object (refType).Object ("properties");

						string primaryKey = FindIdentityColumn (properties);
						if (primaryKey == null)
							throw new Exception ("Primary key not found for " + parentType);

						ids = new[] { jobj [primaryKey] };
					}

					success (ids);
				},
				failure);
		}

		/// <summary>
		/// Updates an existing object with the given column/value dictionary.
		/// </summary>
		/// <param name="type">The parentType (schema) of object to update.</param>
		/// <param name="id">The id of the object to update.</param>
		/// <param name="values">A dictionary of columns to values.</param>
		/// <param name="success">A callback on success, returning the stored object.</param>
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
		/// <param name="success">A callback on success, returning the stored object.</param>
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

			var req = GetRequest (type, "PUT", id);

			Execute (req,
				s => JsonSerializer.SerializeToStream (value, s),
				s => success (JsonSerializer.DeserializeFromStream<T> (s)),
				failure);
		}

		/// <summary>
		/// Appends <paramref name="values"/> to an array <paramref name="field"/> of an existing object.
		/// </summary>
		/// <typeparam name="T">The parentType of object to update.</typeparam>
		/// <param name="parentType">The parent parentType (schema) to update.</param>
		/// <param name="parentId">The id of the parent object to update.</param>
		/// <param name="field">The array field.</param>
		/// <param name="values">The values to append.</param>
		/// <param name="success">A callback on success, returning the stored object.</param>
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
		/// <param name="success">A callback on success, returning the stored object.</param>
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
		/// <param name="success">A callback on success, returning the stored object.</param>
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
		/// <param name="success">A callback on success, returning the stored object.</param>
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
		/// <param name="success">A callback on success, returning the stored object.</param>
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
		/// <param name="success">A callback on success, returning the stored object.</param>
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
		/// <typeparam name="T">The parentType of object to retrieve.</typeparam>
		/// <param name="type">The parentType name (schema) to retrieve.</param>
		/// <param name="success">A callback on success, returning a list of objects.</param>
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
				s =>
				{
					var result = JsonSerializer.DeserializeFromStream<IEnumerable<T>> (s);
					success (result);
				},
				failure);
		}

		/// <summary>
		/// Gets the object with the given <paramref name="id"/>.
		/// </summary>
		/// <typeparam name="T">The parentType of object to retrieve.</typeparam>
		/// <param name="type">The parentType name (schema) of the object to retrieve.</param>
		/// <param name="id">The id of the object to retrieve.</param>
		/// <param name="success">A callback on success, returning the stored object.</param>
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

			var req = GetRequest (type, "GET", id);
			Execute (req,
				s =>
				{
					T result = JsonSerializer.DeserializeFromStream<T> (s);
					success (result);
				},
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

			var req = GetRequest (type, "DELETE", id);
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

		private readonly string accepts;

		private readonly string apiKey;
		private readonly string apiSecret;
		private readonly string userObjectName;
		private readonly string appname;

		private JsonObject GetApis ()
		{
			ManualResetEvent mre = new ManualResetEvent (false);

			JsonObject api = null;
			Exception error = null;

			var req = GetRequest ("listapi", "GET");
			Execute (req,
				s =>
				{
					api = JsonSerializer.DeserializeFromStream<JsonObject> (s);
					mre.Set();
				},
				
				ex =>
				{
					error = ex;
					mre.Set();
				});

			mre.WaitOne();

			if (error != null)
				throw error;

			return api;
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

			var req = GetRequest (parentType, "DELETE", parentId + "/" + ids);
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

			var req = GetRequest (parentType, "PUT", parentId + "/" + field);
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
