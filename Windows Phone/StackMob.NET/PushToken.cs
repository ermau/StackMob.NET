//
// PushToken.cs
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
using ServiceStack.Text;

namespace StackMob
{
	public enum PushTokenType
	{
		Android,
		iOS
	}

	/// <summary>
	/// Class representing a token for push. 
	/// </summary>
	public class PushToken
	{
		/// <summary>
		/// Creates and initializes a new <see cref="PushToken"/> instance.
		/// </summary>
		/// <param name="type">The type of token in <paramref name="token"/>.</param>
		/// <param name="token">The token.</param>
		/// <exception cref="ArgumentNullException"><paramref name="token"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentException">
		/// <para><paramref name="type" /> is not a valid <see cref="PushTokenType"/></para>
		/// <para>-- or --</para>
		/// <para><paramref name="token"/> is an empty string.</para>
		/// </exception>
		public PushToken (PushTokenType type, string token)
		{
			if (!Enum.IsDefined (typeof(PushTokenType), type))
				throw new ArgumentException ("Invalid type", "type");
			if (token == null)
				throw new ArgumentNullException ("token");
			if (token.Trim() == String.Empty)
				throw new ArgumentException ("token can not be empty", "token");

			Type = type;
			Token = token;
		}

		/// <summary>
		/// Gets the type of the token.
		/// </summary>
		public PushTokenType Type
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the token.
		/// </summary>
		public string Token
		{
			get;
			private set;
		}

		internal IDictionary<string,object> ToJsonObject()
		{
			var jobj = new Dictionary<string, object>();
			jobj["type"] = Type.ToString().ToLower();
			jobj["token"] = Token;
			return jobj;
		}
	}
}
