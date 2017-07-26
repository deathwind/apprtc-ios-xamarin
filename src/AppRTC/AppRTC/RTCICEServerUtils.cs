//
// RTCICEServerUtils.cs
//
// Author:
//       valentingrigorean <v.grigorean@software-dep.net>
//
// Copyright (c) 2017 (c) Grigorean Valentin
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Collections.Generic;
using Foundation;
using WebRTCBinding;
using System;

namespace AppRTC
{
    public static class RTCICEServerUtils
    {
        private static readonly NSString UsernameKey = new NSString(@"username");
        private static readonly NSString PasswordKey = new NSString(@"password");
        private static readonly NSString UrisKey = new NSString(@"uris");
        private static readonly NSString UrlKey = new NSString(@"urls");
        private static readonly NSString CredentialKey = new NSString(@"credential");

        public static RTCICEServer ServerFromJSONDictionary(NSDictionary dict)
        {
            var url = dict[UrlKey] as NSString;
            var username = dict[UsernameKey] as NSString;
            var credential = dict[CredentialKey] as NSString;

            username = username ?? (NSString)"";
            credential = credential ?? (NSString)"";

            return new RTCICEServer(new NSUrl(url), username, credential);
        }

        public static IList<RTCICEServer> ServersFromCEODJSONDictionary(NSDictionary dict)
        {
            var servers = new List<RTCICEServer>();

            var username = dict[UsernameKey] as NSString;
            var password = dict[PasswordKey] as NSString;

            var uris = dict[UrisKey] as NSArray;

            for (var i = (nuint)0; i < uris.Count; i++)
            {
                var uri = uris.GetItem<NSString>(i);
                servers.Add(new RTCICEServer(new NSUrl(uri), username, password));
            }
            return servers;
        }
    }
}
