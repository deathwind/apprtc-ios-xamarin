using System;
using Foundation;
using Newtonsoft.Json;
using SocketRocketBinding;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using AppRTC.Extensions;

namespace AppRTC
{
    public enum ARDWebSocketChannelState
    {
        Closed,
        // State when connection is established but not ready for use.
        Open,
        // State when connection is established and registered.
        Registered,
        // State when connection encounters a fatal error.
        Error
    }

    public class ARDWebSocketChannel : SRWebSocketDelegate 
    {
        private IARDWebSocketChannelDelegate _delegate;
        private string _webSocketUrl;
        private string _webSockerRestUrl;
        private string _roomId;
        private string _clientId;
        private readonly SRWebSocket _socket;
        private ARDWebSocketChannelState _state;

        private string WebRestFormated => $"{_webSockerRestUrl}/{_roomId}/{_clientId}";

        public ARDWebSocketChannel(string webSocketUrl, string webSockerRestUrl, IARDWebSocketChannelDelegate @delegate)
        {
            _webSocketUrl = webSocketUrl;
            _webSockerRestUrl = webSockerRestUrl;
            _delegate = @delegate;
            _socket = new SRWebSocket(new NSUrl(_webSocketUrl))
            {
                Delegate = this
            };
            Debug.WriteLine("Opening WebSocket.");
            _socket.Open();
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disconnect();
            }
            base.Dispose(disposing);
        }


        public ARDWebSocketChannelState State
        {
            get => _state;
            set
            {
                if (_state == value)
                    return;
                _state = value;
                _delegate?.DidChangeState(value);
            }
        }

        public void RegisterForRoomId(string roomId, string clientId)
        {
            Contract.Requires(!string.IsNullOrEmpty(roomId));
            Contract.Requires(!string.IsNullOrEmpty(clientId));

            _roomId = roomId;
            _clientId = clientId;

            if (State == ARDWebSocketChannelState.Open)
                RegisterWithCollider();
        }

        public void SendData(NSData data)
        {
            Contract.Requires(!string.IsNullOrEmpty(_roomId));
            Contract.Requires(!string.IsNullOrEmpty(_clientId));
            var payload = new NSString(data, NSStringEncoding.UTF8);
            switch (State)
            {
                case ARDWebSocketChannelState.Registered:
                    var message = new NSDictionary(
                        "cmd", "send",
                        "msg", payload);

                    var messageJSON = NSJsonSerialization.Serialize(
                        message, NSJsonWritingOptions.PrettyPrinted, out NSError err);

                    var messageString = new NSString(
                        messageJSON, NSStringEncoding.UTF8);

                    Debug.WriteLine($"C->WSS:{messageString}");
                    _socket.Send(messageString);
                    break;
                default:
                    Debug.WriteLine($"C->WSS:{payload}");
                    var url = new NSUrl(WebRestFormated);
                    url.SendAsyncPostToURL(data, null);
                    break;
            }

        }

        public void Disconnect()
        {
            if (State == ARDWebSocketChannelState.Closed ||
               State == ARDWebSocketChannelState.Error)
                return;
            _socket.Close();
            Debug.WriteLine($"C->WSS DELETE rid:{_roomId} cid:{_clientId}");
            var url = new NSUrl(WebRestFormated);
            var request = new NSMutableUrlRequest(url)
            {
                HttpMethod = "DELETE",
                Body = null
            };
            request.SendAsyncRequest(null);
        }

        #region SRWebSocketDelegate

        public override void WebSocketDidOpen(SRWebSocket webSocket)
        {
            Debug.WriteLine("WebSocket connection opened.");
            State = ARDWebSocketChannelState.Open;
            RegisterWithCollider();
        }

        public override void WebSocketDidReceiveMessage(SRWebSocket webSocket, NSObject message)
        {
            var socketResponse = default(SocketResponse);

            try
            {
                socketResponse = JsonConvert.DeserializeObject<SocketResponse>(message?.ToString());
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Invalid json error: {ex.Message} message:{message}");
                return;
            }

            if(!string.IsNullOrEmpty(socketResponse.error))
            {
                Debug.WriteLine($"WSS error: {socketResponse.error}");
                return;
            }

            var payload = ARDSignalingMessage.MessageFromJSONString(socketResponse.msg);
            Debug.WriteLine($"WSS->C: {payload}");

            _delegate?.DidReceiveMessage(payload);
        }

        public override void WebSocketDidFailWithError(SRWebSocket webSocket, NSError error)
        {
            Debug.WriteLine($"WebSocket error: {error}");
            State = ARDWebSocketChannelState.Error;
        }

        public override void WebSocketDidClose(SRWebSocket webSocket, nint code, string reason, bool wasClean)
        {
            Debug.WriteLine($"WebSocket closed with code: {code} reason:{reason} wasClean:{wasClean}");
            Contract.Requires(State != ARDWebSocketChannelState.Error);
            State = ARDWebSocketChannelState.Closed;
        }
        #endregion


        private void RegisterWithCollider()
        {
            Contract.Requires(!string.IsNullOrEmpty(_roomId));
            Contract.Requires(!string.IsNullOrEmpty(_clientId));
            if (State == ARDWebSocketChannelState.Registered)
                return;
            var registerMessage = new NSDictionary(
                "cmd", "register",
                "roomid", _roomId,
                "clientid", _clientId);

            var message = NSJsonSerialization.Serialize(
                registerMessage, NSJsonWritingOptions.PrettyPrinted, out NSError err);

            Debug.WriteLine($"Registering on WSS for rid:{_roomId} cid:{_clientId}");
            //// Registration can fail if server rejects it. For example, if the room is
            //// full.
            _socket.Send(new NSString(message, NSStringEncoding.UTF8));
            State = ARDWebSocketChannelState.Registered;
        }


    }
}
