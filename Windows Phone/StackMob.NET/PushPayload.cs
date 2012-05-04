//
// PushPayload.cs
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

using System.Collections.Generic;

namespace StackMob
{
	public class PushPayload
		: Dictionary<string, object>
	{
		/// <summary>
		/// Gets or sets the badge count to set.
		/// </summary>
		public int Badge
		{
			get { return (int)this["badge"]; }
			set { this["badge"] = value; }
		}

		/// <summary>
		/// Gets or sets the filename of the sound to play.
		/// </summary>
		public string Sound
		{
			get { return (string)this["sound"]; }
			set { this["sound"] = value; }
		}

		/// <summary>
		/// Gets or sets the alert text.
		/// </summary>
		public string Alert
		{
			get { return (string)this["alert"]; }
			set { this["alert"] = value; }
		}
	}
}
