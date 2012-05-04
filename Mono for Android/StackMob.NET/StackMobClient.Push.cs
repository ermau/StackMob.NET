//
// StackMobClient.Push.cs
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
using ServiceStack.Text;

namespace StackMob
{
	public partial class StackMobClient
	{
		public void RegisterPush (string username, string registrationId, Action success, Action<Exception> failure)
		{
			CheckArgument (username, "username");
			CheckArgument (registrationId, "registrationId");
			if (success == null)
				throw new ArgumentNullException ("success");
			if (failure == null)
				throw new ArgumentNullException ("failure");

			JsonObject jobj = new JsonObject();
			jobj["userId"] = username;
			
			JsonObject token = new JsonObject();
			token["type"] = "android";
			token["token"] = registrationId;

			jobj["token"] = token.ToJson();

			var req = GetRequest ("register_device_token_universal", "POST");
			Execute (req,
				s => JsonSerializer.SerializeToStream (jobj, s),
				s => success(),
				failure);
		}
	}
}