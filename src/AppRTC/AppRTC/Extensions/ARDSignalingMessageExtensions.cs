using WebRTCBinding;
using Newtonsoft.Json;

namespace AppRTC.Extensions
{
    public static class ARDSignalingMessageExtensions
    {
        public static string AsJSON(this RTCSessionDescription rtcSessionDescription)
        {
            return JsonConvert.SerializeObject(new { type = rtcSessionDescription.Type, sdp = rtcSessionDescription.Description });
        }

        public static string AsJSON(this RTCICECandidate rtcICEcandidate)
        {
            return JsonConvert.SerializeObject(
                new
                {
                    type = "candidate",
                    id = (int)rtcICEcandidate.SdpMLineIndex,
                    label = rtcICEcandidate.SdpMid,
                    candidate = rtcICEcandidate.Sdp
                });
        }
    }
}