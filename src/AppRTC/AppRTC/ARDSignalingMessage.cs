using System;
using System.Collections.Generic;
using WebRTCBinding;
using Newtonsoft.Json;
using AppRTC.Extensions;
using Foundation;

namespace AppRTC
{
    public enum ARDSignalingMessageType
    {
        Candidate,
        Offer,
        Answer,
        Bye,
    }

    public class ARDSignalingMessage
    {
        public static ARDSignalingMessage MessageFromJSONString(string json)
        {
            var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            ARDSignalingMessage message = new ARDSignalingMessage();
            var type = values["type"];
            switch (type)
            {
                case "candidate":
                    nint.TryParse(values["label"], out nint label);
                    RTCICECandidate candidate = new RTCICECandidate(values["id"], label, values["candidate"]);
                    message = new ARDICECandidateMessage(candidate);
                    break;
                case "offer":
                case "answer":
                    RTCSessionDescription description = new RTCSessionDescription(type, values["sdp"]);
                    message = new ARDSessionDescriptionMessage(description);
                    break;
                case "bye":
                    message = new ARDByeMessage();
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine($"Unexpected type: {type}");
                    break;
            }

            return message;
        }

        public ARDSignalingMessageType Type
        {
            get;
            set;
        }

        public virtual NSData JsonData
        {
            get;
        }

        public override string ToString()
        {
            return JsonData.ToNSString();
        }
    }

    public class ARDICECandidateMessage : ARDSignalingMessage
    {
        public ARDICECandidateMessage(RTCICECandidate candidate)
        {
            Type = ARDSignalingMessageType.Candidate;
            Candidate = candidate;
        }

        public RTCICECandidate Candidate
        {
            get;
            set;
        }

        public override NSData JsonData => NSData.FromString(Candidate.AsJSON());
    }

    public class ARDSessionDescriptionMessage : ARDSignalingMessage
    {
        public ARDSessionDescriptionMessage(RTCSessionDescription description)
        {
            Description = description;
            if (Description.Type.Equals("offer", StringComparison.Ordinal))
            {
                Type = ARDSignalingMessageType.Offer;
            }
            else if (Description.Type.Equals("answer", StringComparison.Ordinal))
            {
                Type = ARDSignalingMessageType.Answer;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected type: {Type}");
            }
        }

        public RTCSessionDescription Description
        {
            get;
            set;
        }

        public override NSData JsonData => NSData.FromString(Description.AsJSON());
    }

    public class ARDByeMessage : ARDSignalingMessage
    {
        public ARDByeMessage()
        {
            Type = ARDSignalingMessageType.Bye;
        }

        public override NSData JsonData => NSData.FromString(JsonConvert.SerializeObject(new { type = "bye" }));
    }
}