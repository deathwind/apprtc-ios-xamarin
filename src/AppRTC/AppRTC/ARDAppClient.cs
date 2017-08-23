//
// ARDAppClient.cs
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

using System;
using Foundation;
using WebRTCBinding;
using System.Collections.Generic;
using UIKit;
using AVFoundation;
using System.Linq;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;
using CoreFoundation;
using AppRTC.Extensions;

namespace AppRTC
{
    public enum ARDAppError
    {
        Unknown = -1,
        RoomFull = -2,
        CreateSDP = -3,
        SetSDP = -4,
        Network = -5,
        InvalidClient = -6,
        InvalidRoom = -7,
    }

    public class ARDAppClientConfig
    {
        public string RoomServerHostUrl { get; set; } = "http://91.231.0.173:3020/";
        public string RoomServerByeFormat { get; set; } = "{0}/leave/{1}/{2}";
        public string RoomServerRegisterFormat { get; set; } = "{0}/join/{1}";
        public string RoomServerMessageFormat { get; set; } = "{0}/message/{1}/{2}";
        public string DefaultSTUNServerUrl { get; set; } = "stun:stun.l.google.com:19302";
        public string TurnRequestUrl { get; set; } = "https://computeengineondemand.appspot.com/turn?username=iapprtc&key=4080218913";

        public static ARDAppClientConfig Default { get; } = new ARDAppClientConfig();
    }

    public class ARDAppClient : NSObject, IARDAppClient, IARDWebSocketChannelDelegate, IRTCPeerConnectionDelegate, IRTCSessionDescriptionDelegate
    {
        private ARDWebSocketChannel _channel;
        private RTCPeerConnection _peerConnection;
        private readonly RTCPeerConnectionFactory _factory = new RTCPeerConnectionFactory();
        private readonly List<ARDSignalingMessage> _messageQueue = new List<ARDSignalingMessage>();

        private bool _isTurnComplete;
        private bool _hasReceivedSdp;

        private string _roomId;
        private string _clientId;
        private bool _isInitiator;
        private bool _isSpeakerEnabled;
        private readonly List<RTCICEServer> _iceServers = new List<RTCICEServer>();
        private string _webSocketUrl;
        private string _webSocketRestUrl;
        private RTCAudioTrack _defaultAudioTrack;
        private RTCVideoTrack _defaultVideoTrack;

        private bool _isAudioEnable = true;
        private bool _isVideoEnable = true;

        private ARDAppClientState _state;

        private readonly NSObject _orientationChangeHandler;

        #region Defaults
        private RTCMediaConstraints DefaultMediaStreamConstraint =>
                new RTCMediaConstraints(null, null);

        private RTCMediaConstraints DefaultPeerConnectionConstraints =>
                new RTCMediaConstraints(null, NSArray.FromNSObjects(
                    new RTCPair("DtlsSrtpKeyAgreement", "true")));

        private RTCMediaConstraints DefaultOfferConstraints
        {
            get
            {
                NSArray mandatoryConstraints = NSArray.FromNSObjects(
                    new RTCPair("OfferToReceiveAudio", "true"),
                    new RTCPair("OfferToReceiveVideo", "true"));
                return new RTCMediaConstraints(mandatoryConstraints, null);
            }
        }

        private RTCMediaConstraints DefaultAnswerConstraints => DefaultOfferConstraints;

        private RTCICEServer DefaultSTUNServer =>
                new RTCICEServer(new NSUrl(Config.DefaultSTUNServerUrl), "", "");

        #endregion


        private bool IsRegisteredWithRoomServer => !string.IsNullOrEmpty(_clientId);

        public ARDAppClient(IARDAppClientDelegate del) : this(del, ARDAppClientConfig.Default)
        {
        }

        public ARDAppClient(IARDAppClientDelegate del, ARDAppClientConfig config)
        {
            Delegate = del;
            Config = config;

            _isSpeakerEnabled = true;
            _orientationChangeHandler = UIDevice.Notifications.ObserveOrientationDidChange(OnOrientationChanged);

            _iceServers.Add(DefaultSTUNServer);
        }

        /// <summary>
        /// Gets or sets the camera position.
        /// First camera build
        /// </summary>
        /// <value>The camera position.</value>
        public AVCaptureDevicePosition PreferCameraPosition { get; set; } = AVCaptureDevicePosition.Back;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _orientationChangeHandler.Dispose();
                Disconnect();
            }
            base.Dispose(disposing);
        }

        public ARDAppClientState State
        {
            get => _state;
            set
            {
                if (_state == value)
                    return;
                _state = value;
                Delegate?.DidChangeState(this, value);
            }
        }

        public bool IsAudioEnable
        {
            get => _isAudioEnable;
            set
            {
                if (_isAudioEnable == value)
                    return;
                _isAudioEnable = true;
                if (value)
                {
                    UnmuteAudioIn();
                }
                else
                {
                    MuteAudioIn();
                }

            }
        }

        public bool IsVideoEnable
        {
            get => _isVideoEnable;
            set
            {
                if (_isVideoEnable == value)
                    return;
                if (value)
                    UnmuteVideoIn();
                else
                    MuteVideoIn();
            }
        }

        public bool IsBackCamera
        {
            get; private set;
        }


        public void SwitchCamera()
        {
            if (IsBackCamera)
                SwapCameraToFront();
            else
                SwapCameraToBack();
        }

        public ARDAppClientConfig Config { get; private set; }

        #region ARDAppClient

        public IARDAppClientDelegate Delegate { get; private set; }

        public void ConnectToRoomWithId(string roomName)
        {
            Contract.Requires(!string.IsNullOrEmpty(roomName));
            Contract.Requires(State != ARDAppClientState.Disconnected);

            State = ARDAppClientState.Connecting;

            var turnRequestUrl = new NSUrl(Config.TurnRequestUrl);
            // Request TURN.
            RequestTURNServersWithURL(turnRequestUrl, (iceservers) =>
            {
                _iceServers.AddRange(iceservers);
                _isTurnComplete = true;
                StartSignalingIfReady();
            });

            // Register with room server.
            RegisterWithRoomServerForRoomId(roomName, response =>
            {
                if (response == null ||
                    response.result == ARDRegisterResultType.FULL ||
                    response.result == ARDRegisterResultType.UNKNOWN)
                {

                    Debug.WriteLine("Failed to register with room server. Result:{0}", response?.result);
                    Disconnect();

                    var msg = "Unknown error occurred.";
                    var errorCode = (int)ARDAppError.Unknown;

                    if (response != null &&
                        response.result == ARDRegisterResultType.FULL)
                    {
                        msg = "Room is full.";
                        errorCode = (int)ARDAppError.RoomFull;
                    }

                    var error = CreateError(errorCode, msg);
                    Delegate?.DidError(this, error);
                    return;
                }
                Debug.WriteLine("Registered with room server.");
                _roomId = response.@params.room_id;
                _clientId = response.@params.client_id;
                _isInitiator = response.@params.is_initiator;

                var messages = response.GetMessages();
                foreach (var message in messages)
                {
                    switch (message.Type)
                    {
                        case ARDSignalingMessageType.Answer:
                        case ARDSignalingMessageType.Offer:
                            _hasReceivedSdp = true;
                            _messageQueue.Insert(0, message);
                            break;
                        default:
                            _messageQueue.Add(message);
                            break;
                    }
                }

                _webSocketUrl = response.@params.wss_url;
                _webSocketRestUrl = response.@params.wss_post_url;

                RegisterWithColliderIfReady();
                StartSignalingIfReady();
            });

        }

        public void SetServerHostUrl(string serverHostUrl)
        {
            Config.RoomServerHostUrl = serverHostUrl;
        }

        public void Disconnect()
        {
            if (State == ARDAppClientState.Disconnected)
                return;
            if (IsRegisteredWithRoomServer)
                UnregisterWithRoomServer();

            if (_channel != null)
            {
                if (_channel.State == ARDWebSocketChannelState.Registered)
                {
                    var byeMessage = new ARDByeMessage();
                    _channel.SendData(byeMessage.JsonData);
                }
                _channel = null;
            }
            _clientId = null;
            _roomId = null;
            _isInitiator = false;
            _hasReceivedSdp = false;
            _messageQueue.Clear();
            _peerConnection = null;
            State = ARDAppClientState.Disconnected;
        }

        public void SwapCameraToBack()
        {
            UpdateLocalStream(localStream =>
            {
                if (localStream == null)
                    return false;
                if (localStream.VideoTracks != null && localStream.VideoTracks.Length > 0)
                    localStream.RemoveVideoTrack(localStream.VideoTracks[0]);
                var localVideoTrack = CreateLocalVideoTrack(AVCaptureDevicePosition.Back);
                if (localVideoTrack != null)
                {
                    localStream.AddVideoTrack(localVideoTrack);
                    Delegate?.DidReceiveLocalVideoTrack(this, localVideoTrack);
                }
                return true;
            });
        }

        public void SwapCameraToFront()
        {
            UpdateLocalStream(localStream =>
            {
                if (localStream == null)
                    return false;
                if (localStream.VideoTracks != null && localStream.VideoTracks.Length > 0)
                    localStream.RemoveVideoTrack(localStream.VideoTracks[0]);
                var localVideoTrack = CreateLocalVideoTrack(AVCaptureDevicePosition.Front);
                if (localVideoTrack != null)
                {
                    localStream.AddVideoTrack(localVideoTrack);
                    Delegate?.DidReceiveLocalVideoTrack(this, localVideoTrack);
                }
                return true;
            });
        }


        public void MuteAudioIn()
        {
            Debug.WriteLine("audio muted");
            UpdateLocalStream(localStream =>
            {
                if (localStream == null || localStream.AudioTracks.Length == 0)
                    return false;
                _defaultAudioTrack = localStream.AudioTracks[0];
                localStream.RemoveAudioTrack(_defaultAudioTrack);
                return true;
            });
        }

        public void UnmuteAudioIn()
        {
            Debug.WriteLine("audio unmuted");
            UpdateLocalStream(localStream =>
            {
                if (localStream == null || localStream.AudioTracks.Length == 0)
                    return false;
                localStream.AddAudioTrack(_defaultAudioTrack);
                if (_isSpeakerEnabled)
                    EnableSpeaker();
                return true;
            });
        }

        public void MuteVideoIn()
        {
            Debug.WriteLine("video muted");
            UpdateLocalStream(localStream =>
            {
                if (localStream == null || localStream.VideoTracks.Length == 0)
                    return false;
                _defaultVideoTrack = localStream.VideoTracks[0];
                localStream.RemoveVideoTrack(_defaultVideoTrack);
                return true;
            });
        }

        public void UnmuteVideoIn()
        {
            Debug.WriteLine("video unmuted");

            UpdateLocalStream(localStream =>
            {
                if (localStream == null)
                    return false;
                localStream.AddVideoTrack(_defaultVideoTrack);
                return true;
            });
        }


        #endregion

        #region ARDWebSocketChannelDelegate

        public void DidChangeState(ARDWebSocketChannelState state)
        {
            switch (state)
            {
                case ARDWebSocketChannelState.Error:
                    Disconnect();
                    break;
            }
        }

        public void DidReceiveMessage(ARDSignalingMessage message)
        {
            switch (message.Type)
            {
                case ARDSignalingMessageType.Offer:
                case ARDSignalingMessageType.Answer:
                    _hasReceivedSdp = true;
                    _messageQueue.Insert(0, message);
                    break;
                case ARDSignalingMessageType.Candidate:
                    _messageQueue.Add(message);
                    break;
                case ARDSignalingMessageType.Bye:
                    ProcessSignalingMessage(message);
                    break;
            }
            DrainMessageQueueIfReady();
        }
        #endregion

        #region WebRTC
        public void PeerConnection(RTCPeerConnection peerConnection, RTCSignalingState stateChanged)
        {
            Debug.WriteLine("Signaling state changed: {0}", stateChanged);
        }

        public void PeerConnectionAdded(RTCPeerConnection peerConnection, RTCMediaStream stream)
        {
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                Debug.WriteLine("Received {0} video tracks and {1} audio tracks",
                                stream.VideoTracks.Length, stream.AudioTracks.Length);

                if (stream.VideoTracks.Length > 0)
                {
                    var videoTrack = stream.VideoTracks[0];
                    Delegate?.DidReceiveRemoteVideoTrack(this, videoTrack);
                    if (_isSpeakerEnabled)
                        EnableSpeaker();
                }
            });
        }

        public void PeerConnectionRemoved(RTCPeerConnection peerConnection, RTCMediaStream stream)
        {
            Debug.WriteLine("Stream was removed.");
        }

        public void PeerConnectionOnRenegotiationNeeded(RTCPeerConnection peerConnection)
        {
            Debug.WriteLine("WARNING: Renegotiation needed but unimplemented.");
        }

        public void PeerConnection(RTCPeerConnection peerConnection, RTCICEConnectionState newState)
        {
            Debug.WriteLine("ICE state changed: {0}", newState);
        }

        public void PeerConnection(RTCPeerConnection peerConnection, RTCICEGatheringState newState)
        {
            Debug.WriteLine("ICE gathering state changed: {0}", newState);
        }

        public void PeerConnection(RTCPeerConnection peerConnection, RTCICECandidate candidate)
        {
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                var message = new ARDICECandidateMessage(candidate);
                SendSignalingMessage(message);
            });
        }

        public void PeerConnection(RTCPeerConnection peerConnection, RTCDataChannel dataChannel)
        {

        }
        #endregion

        #region RTCSessionDescriptionDelegate
        public void DidCreateSessionDescription(RTCPeerConnection peerConnection, RTCSessionDescription sdp, NSError error)
        {
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                if (error != null)
                {
                    Debug.WriteLine("Failed to create session description. Error: {0}", error);
                    Disconnect();
                    var sdpError = CreateError((int)ARDAppError.CreateSDP, "Failed to create session description.");
                    Delegate?.DidError(this, sdpError);
                    return;
                }
                _peerConnection.SetLocalDescriptionWithDelegate(this, sdp);
                var message = new ARDSessionDescriptionMessage(sdp);
                SendSignalingMessage(message);
            });

        }

        public void DidSetSessionDescriptionWithError(RTCPeerConnection peerConnection, NSError error)
        {
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                if (error != null)
                {
                    Debug.WriteLine("Failed to set session description. Error: {0}", error);
                    Disconnect();
                    var sdpError = CreateError((int)ARDAppError.SetSDP, "Failed to set session description.");
                    Delegate?.DidError(this, sdpError);
                    return;
                }
                // If we're answering and we've just set the remote offer we need to create
                // an answer and set the local description.
                if (!_isInitiator && _peerConnection.LocalDescription == null)
                {
                    _peerConnection.CreateAnswerWithDelegate(this, DefaultAnswerConstraints);
                }
            });
        }
        #endregion


        private void OnOrientationChanged(object sender, NSNotificationEventArgs notification)
        {
            var orientation = UIDevice.CurrentDevice.Orientation;
            if (!orientation.IsLandscape() ||
                !orientation.IsPortrait() ||
                _peerConnection == null)
            {
                return;
            }

            UpdateLocalStream(localStream =>
            {
                localStream.RemoveVideoTrack(localStream.VideoTracks[0]);

                var localVideoTrack = CreateLocalVideoTrack(IsBackCamera ? AVCaptureDevicePosition.Back : AVCaptureDevicePosition.Front);
                if (localVideoTrack != null)
                {
                    localStream.AddVideoTrack(localVideoTrack);
                    Delegate?.DidReceiveLocalVideoTrack(this, localVideoTrack);
                }
                return true;
            });
        }

        private RTCMediaStream CreateLocalMediaStream()
        {
            var localStream = _factory.MediaStreamWithLabel("ARDAMS");
            var localVideoTrack = CreateLocalVideoTrack(PreferCameraPosition);
            if (localVideoTrack != null)
            {
                localStream.AddVideoTrack(localVideoTrack);
                Delegate?.DidReceiveLocalVideoTrack(this, localVideoTrack);
            }
            localStream.AddAudioTrack(_factory.AudioTrackWithID("ARDAMSa0"));
            if (_isSpeakerEnabled)
                EnableSpeaker();
            return localStream;
        }

        private RTCVideoTrack CreateLocalVideoTrack(AVCaptureDevicePosition position = AVCaptureDevicePosition.Front)
        {
            // The iOS simulator doesn't provide any sort of camera capture
            // support or emulation (http://goo.gl/rHAnC1) so don't bother
            // trying to open a local stream.
            // TODO(tkchin): local video capture for OSX. See
            // https://code.google.com/p/webrtc/issues/detail?id=3417.

            bool isSimulator = ObjCRuntime.Runtime.Arch == ObjCRuntime.Arch.SIMULATOR;
            if (isSimulator)
                return null;
            var cameraId = "";
            var avCaptureDDS = AVCaptureDeviceDiscoverySession.Create(
                new[]
            {
                AVCaptureDeviceType.BuiltInWideAngleCamera,
                AVCaptureDeviceType.BuiltInDuoCamera
            }, AVMediaType.Video, AVCaptureDevicePosition.Unspecified);
            var devices = avCaptureDDS.Devices;

            if (devices.Length == 0)
                return null;

            foreach (var device in devices)
            {
                if (device.Position == position)
                {
                    cameraId = device.LocalizedName;
                    break;
                }
            }

            IsBackCamera = devices != null && position == AVCaptureDevicePosition.Back;

            if (string.IsNullOrEmpty(cameraId))
                cameraId = devices.FirstOrDefault()?.LocalizedName;

            var capturer = RTCVideoCapturer.CapturerWithDeviceName(cameraId);
            var videoSource = _factory.VideoSourceWithCapturer(capturer, DefaultMediaStreamConstraint);
            return _factory.VideoTrackWithID("ARDAMSv0", videoSource);
        }

        private void RequestTURNServersWithURL(
            NSUrl requestUrl,
            Action<IList<RTCICEServer>> completionHandler)
        {
            Contract.Requires(requestUrl != null);
            Contract.Requires(!string.IsNullOrEmpty(requestUrl.AbsoluteString));
            var list = new List<RTCICEServer>();

            var request = new NSMutableUrlRequest(requestUrl);
            var options = new NSMutableDictionary
            {
                { (NSString)"user-agent",(NSString)"Mozilla/5.0" },
                { (NSString)"origin",(NSString)Config.RoomServerHostUrl }
            };
            request.Headers = options;

            request.SendAsyncRequest((response, data, error) =>
            {
                if (error != null)
                {
                    Debug.WriteLine("Unable to get TURN server.");
                    completionHandler?.Invoke(list);
                    return;
                }
                var dict = data.DictionaryWithJSONData();
                completionHandler?.Invoke(
                    dict == null ?
                    list : RTCICEServerUtils.ServersFromCEODJSONDictionary(dict));
            });
        }

        /// <summary>
        /// Updates the local stream.
        /// if callback return true will refresh the stream
        /// </summary>
        /// <param name="callback">Callback.</param>
        private void UpdateLocalStream(Func<RTCMediaStream, bool> callback)
        {
            if (_peerConnection == null || _peerConnection.LocalStreams.Length == 0)
                return;
            var localMedia = _peerConnection.LocalStreams[0];
            var shouldRefresh = callback?.Invoke(localMedia);
            if (shouldRefresh.HasValue && shouldRefresh.Value)
            {
                _peerConnection.RemoveStream(localMedia);
                _peerConnection.AddStream(localMedia);
            }
        }

        private void EnableSpeaker()
        {
            AVAudioSession.SharedInstance()
                          .OverrideOutputAudioPort(
                              AVAudioSessionPortOverride.Speaker, out NSError err);
            _isSpeakerEnabled = true;
        }

        private void DisableSpeaker()
        {
            AVAudioSession.SharedInstance()
                          .OverrideOutputAudioPort(
                              AVAudioSessionPortOverride.None, out NSError err);
            _isSpeakerEnabled = false;
        }

        private void WaitForAnswer()
        {
            DrainMessageQueueIfReady();
        }

        private void SendOffer()
        {
            _peerConnection.CreateOfferWithDelegate(this, DefaultOfferConstraints);
        }

        private void ProcessSignalingMessage(ARDSignalingMessage msg)
        {
            Contract.Requires(_peerConnection != null);
            Contract.Requires(msg.Type != ARDSignalingMessageType.Bye);

            switch (msg.Type)
            {
                case ARDSignalingMessageType.Offer:
                case ARDSignalingMessageType.Answer:
                    var sdpMessage = (ARDSessionDescriptionMessage)msg;
                    var description = sdpMessage.Description;
                    _peerConnection.SetRemoteDescriptionWithDelegate(this, description);
                    break;
                case ARDSignalingMessageType.Candidate:
                    var candidateMessage = (ARDICECandidateMessage)msg;
                    _peerConnection.AddICECandidate(candidateMessage.Candidate);
                    break;
                case ARDSignalingMessageType.Bye:
                    Disconnect();
                    break;
            }
        }

        private void DrainMessageQueueIfReady()
        {
            if (_peerConnection == null || !_hasReceivedSdp)
                return;
            foreach (var msg in _messageQueue)
                ProcessSignalingMessage(msg);

            _messageQueue.Clear();
        }

        #region Room server methods
        private void UnregisterWithRoomServer()
        {
            var urlString = string.Format(
                Config.RoomServerByeFormat, Config.RoomServerHostUrl,
                _roomId, _clientId);

            var url = new NSUrl(urlString);

            Debug.WriteLine("C->RS: BYE");
            url.SendAsyncPostToURL(null, (succeeded, data) =>
            {
                if (succeeded)
                    Debug.WriteLine("Unregistered from room server.");
                else
                    Debug.WriteLine("Failed to unregister from room server.");
            });
        }

        private void RegisterWithRoomServerForRoomId(
            string roomId,
            Action<ARDRegisterResponse> completionHandler)
        {
            var urlString = string.Format(Config.RoomServerRegisterFormat, Config.RoomServerHostUrl, roomId);
            var roomURL = new NSUrl(urlString);
            Debug.WriteLine(@"Registering with room server.");

            roomURL.SendAsyncPostToURL(null, (succeeded, data) =>
            {
                var response = default(ARDRegisterResponse);

                if (!succeeded)
                {
                    Delegate?.DidError(this, RoomServerNetworkError());
                    completionHandler?.Invoke(response);
                    return;
                }
                try
                {
                    response = JsonConvert.DeserializeObject<ARDRegisterResponse>(
                        data.ToString());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to parse json: {ex.Message} data:{data}");
                }
                completionHandler?.Invoke(response);
            });
        }

        private void SendSignalingMessageToRoomServer(ARDSignalingMessage message, Action<ARDMessageResultType> completionHandler)
        {
            var urlString = string.Format(Config.RoomServerMessageFormat,
                                          Config.RoomServerHostUrl,
                                          _roomId, _clientId);

            var url = new NSUrl(urlString);
            Debug.WriteLine("C->RS POST:{0}", message);

            url.SendAsyncPostToURL(message.JsonData, (succeeded, data) =>
            {
                var json = data.ToNSString();
                var resp = JsonConvert.DeserializeObject<ARDMessageResponse>(json);
                var type = resp == null ? ARDMessageResultType.Unknown : resp.Type;
                var error = default(NSError);
                switch (type)
                {
                    case ARDMessageResultType.InvalidClient:
                        error = CreateError((int)ARDAppError.InvalidClient, "Invalid client.");
                        break;
                    case ARDMessageResultType.InvalidRoom:
                        error = CreateError((int)ARDAppError.InvalidRoom, "Invalid room.");
                        break;
                    case ARDMessageResultType.Unknown:
                        error = CreateError((int)ARDAppError.Unknown, "Unknown error.");
                        break;
                    case ARDMessageResultType.Success:
                        break;
                }

                if (error != null)
                    Delegate?.DidError(this, error);
                completionHandler?.Invoke(type);
            });
        }
        #endregion

        #region Collider methods
        private void SendSignalingMessageToCollider(ARDSignalingMessage message)
        {
            _channel.SendData(message.JsonData);
        }

        private void RegisterWithColliderIfReady()
        {
            if (!IsRegisteredWithRoomServer)
                return;
            _channel = new ARDWebSocketChannel(_webSocketUrl, _webSocketRestUrl, this);
            _channel.RegisterForRoomId(_roomId, _clientId);
        }
        #endregion

        private void SendSignalingMessage(ARDSignalingMessage message)
        {
            if (_isInitiator)
                SendSignalingMessageToRoomServer(message, null);
            else
                SendSignalingMessageToCollider(message);
        }


        private void StartSignalingIfReady()
        {
            if (!_isTurnComplete || !IsRegisteredWithRoomServer)
                return;
            State = ARDAppClientState.Connected;

            _peerConnection = _factory.PeerConnectionWithICEServers(
                NSArray.FromNSObjects(_iceServers.ToArray()), DefaultPeerConnectionConstraints, this);

            var localStream = CreateLocalMediaStream();
            _peerConnection.AddStream(localStream);

            if (_isInitiator)
                SendOffer();
            else
                WaitForAnswer();
        }

        private static NSError CreateError(int errorCode, string description)
        {
            var userInfo = NSDictionary.FromObjectAndKey(
                NSError.LocalizedDescriptionKey, new NSString(description));
            return new NSError(new NSString(nameof(ARDAppClient)), errorCode, userInfo);
        }

        private static NSError RoomServerNetworkError()
        {
            return CreateError((int)ARDAppError.Network, "Room server network error");
        }
    }
}