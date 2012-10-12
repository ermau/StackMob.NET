//
// StackMobClient.Push.cs
//
// Copyright 2012 Xamarin, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using MonoTouch.UIKit;
using System.Collections.Generic;
using ServiceStack.Text;

namespace StackMob
{
	public partial class StackMobClient
	{
		/// <summary>
		/// Registers the device for push notifications.
		/// </summary>
		/// <param name="username">The username to register the device with.</param>
		/// <param name="registrationId">The iOS push registration token.</param>
		/// <param name="success">A callback on success.</param>
		/// <param name="failure">A callback on failure, giving the exception.</param>
		/// <exception cref="ArgumentNullException"><paramref name="username"/>, <paramref name="registrationId"/>, <paramref name="success"/> or <paramref name="failure"/> is <c>null.</c></exception>
		/// <exception cref="ArgumentException"><paramref name="username"/> or <paramref name="registrationId"/> is an empty string.</exception>
		public void RegisterPush (string username, string registrationId, Action success, Action<Exception> failure)
		{
			RegisterPush (username, new PushToken (PushTokenType.iOS, registrationId), success, failure);
		}
	}
}

