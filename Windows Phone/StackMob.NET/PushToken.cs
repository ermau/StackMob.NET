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

	public class PushToken
	{
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

		public PushTokenType Type
		{
			get;
			private set;
		}

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
