using System;
using Foundation;
using UIKit;
using WebRTCBinding;
using CoreGraphics;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using AVFoundation;

namespace AppRTC.Demo
{




    public partial class ARTCVideoChatViewController : UIViewController, IARDAppClientDelegate, IRTCEAGLVideoViewDelegate
    {

        private const string ServerHostUrl = "https://appr.tc";
		//private const string ServerHostUrl = "https://progetto-casa.bss-one.net:3021";

		private string _roomName;
        private IDisposable _orientationChangeHandler;
        private IDisposable _willResignHandler;

        private string RoomName
        {
            get => _roomName;
            set
            {
                _roomName = value;
                if (UrlLabel != null)
                    UrlLabel.Text = RoomUrl;
                if (RoomTextField != null)
                    RoomTextField.Text = value;
            }
        }

        private string RoomUrl => $"{ServerHostUrl}/r/{RoomName}";

        private ARDAppClient Client { get; set; }
        private RTCVideoTrack LocalVideoTrack { get; set; }
        private RTCVideoTrack RemoteVideoTrack { get; set; }
        private CGSize LocalVideoSize { get; set; }
        private CGSize RemoteVideoSize { get; set; }

        private bool IsZoom { get; set; }
        private bool IsBackCamera { get; set; }
        private bool IsAudioMute { get; set; }

        public ARTCVideoChatViewController(IntPtr handler) : base(handler)
        {

        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            JoinContainerView.Layer.ShadowOffset = new CGSize(0.4f, 0.3f);
            JoinContainerView.Layer.ShadowOpacity = 0.8f;
            JoinContainerView.Layer.ShadowRadius = 5f;
            JoinContainerView.Layer.ShadowColor = UIColor.Black.CGColor;


            RoomName = $"abcqwe{new Random(DateTime.Now.Second).Next(0, 30)}";
            AudioButton.Layer.CornerRadius = 20f;
            VideoButton.Layer.CornerRadius = 20f;
            HangupButton.Layer.CornerRadius = 20f;

            var tapGestureRecognizer = new UITapGestureRecognizer(ToggleButtonContainer)
            {
                NumberOfTapsRequired = 1
            };
            View.AddGestureRecognizer(tapGestureRecognizer);

            tapGestureRecognizer = new UITapGestureRecognizer(ZoomRemote)
            {
                NumberOfTapsRequired = 2
            };
            View.AddGestureRecognizer(tapGestureRecognizer);

            RemoteView.DidChangeVideoSize += ResizeRemoteView;
            LocalView.DidChangeVideoSize += ResizeLocalView;


            _orientationChangeHandler = UIDevice.Notifications
                                                .ObserveOrientationDidChange(OnOrientationChange);

            _willResignHandler = UIApplication.Notifications
                                              .ObserveWillResignActive(OnResignActive);

            AudioButton.TouchUpInside += AudioButtonPressed;
            VideoButton.TouchUpInside += VideoButtonPressed;
            HangupButton.TouchUpInside += HangupButtonPressed;

            JoinButton.TouchUpInside += (sender, e) =>
            {
                if (string.IsNullOrEmpty(RoomTextField.Text))
                    return;
                RoomName = RoomTextField.Text;
                JoinRoom();
            };
        }


        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            SetFullScreenLocalView();
        }

        public override void ViewWillDisappear(bool animated)
        {
            base.ViewWillDisappear(animated);
            _orientationChangeHandler.Dispose();
            _willResignHandler.Dispose();
            Disconnect();
        }

        private void JoinRoom()
        {
            RoomTextField.ResignFirstResponder();
            Disconnect();

            OverlayView.Alpha = 0f;

            Client = new ARDAppClient(this);
            Client.SetServerHostUrl(ServerHostUrl);
            Client.ConnectToRoomWithId(RoomName);

            UrlLabel.Text = RoomUrl;
        }

        #region ARDAppClientDelegate
        public void DidChangeState(IARDAppClient client, ARDAppClientState state)
        {
            switch (state)
            {
                case ARDAppClientState.Connected:
                    Debug.WriteLine("Client connected.");
                    break;

                case ARDAppClientState.Connecting:
                    Debug.WriteLine("Client connecting.");
                    break;
                case ARDAppClientState.Disconnected:
                    Debug.WriteLine("Client disconnected.");
                    RemoteDisconnected();
                    break;
            }
        }

        public void DidError(IARDAppClient client, NSError error)
        {
            var alertDialog = UIAlertController.Create(
                "", error?.ToString(), UIAlertControllerStyle.Alert);

            alertDialog.AddAction(
                UIAlertAction.Create("OK", UIAlertActionStyle.Cancel, null));

            PresentViewController(alertDialog, true, null);
            Disconnect();
        }

        public void DidReceiveLocalVideoTrack(IARDAppClient client, RTCVideoTrack localVideoTrack)
        {
            LocalVideoTrack?.RemoveRenderer(LocalView);
            LocalView.RenderFrame(null);

            LocalVideoTrack = localVideoTrack;
            LocalVideoTrack.AddRenderer(LocalView);
        }

        public void DidReceiveRemoteVideoTrack(IARDAppClient client, RTCVideoTrack remoteVideoTrack)
        {
            RemoteVideoTrack = remoteVideoTrack;

            remoteVideoTrack.AddRenderer(RemoteView);



            var videoRect = new CGRect(new CGPoint(0, 0), GetSizeWithOrientation(View.Frame.Size));
            var videoFrame = videoRect.WithAspectRatio(LocalView.Frame.Size);

            LocalViewWidthConstraint.Constant = videoFrame.Width;
            LocalViewHeightConstraint.Constant = videoFrame.Height;

            LocalViewBottomConstraint.Constant = 28f;
            LocalViewRightConstraint.Constant = 28f;

            FooterViewBottomConstraint.Constant = -80f;

            UIView.AnimateNotify(0.4f, View.LayoutIfNeeded, null);
        }
        #endregion

        #region RTCEAGLVideoEvents


        /// <summary>
        /// Resize the Local View depending if it is full screen or thumbnail
        /// </summary>
        private void ResizeLocalView(object sender, DidChangeVideoSizeEventArgs e)
        {
            var size = e.Size;
            var containerWidth = View.Frame.Size.Width;
            var containerHeight = View.Frame.Size.Height;
            var defaultAspectRatiuo = new CGSize(4, 3);

            //Resize the Local View depending if it is full screen or thumbnail
            LocalVideoSize = size;
            var aspectRatio = size == CGSize.Empty ? defaultAspectRatiuo : size;
            var videoRect = View.Bounds;
            if (RemoteVideoTrack != null)
            {
                videoRect = new CGRect(
                    new CGPoint(0, 0), GetSizeWithOrientation(videoRect.Size));
                var videoFrame = videoRect.WithAspectRatio(aspectRatio);

                //Resize the localView accordingly
                LocalViewWidthConstraint.Constant = videoFrame.Size.Width;
                LocalViewHeightConstraint.Constant = videoFrame.Size.Height;

                if (RemoteVideoTrack != null)
                {
                    LocalViewBottomConstraint.Constant = 28f;//bottom right corner
                    LocalViewRightConstraint.Constant = 28f;
                }
                else
                {
                    LocalViewBottomConstraint.Constant =
                        containerHeight / 2f - videoFrame.Size.Height / 2f;//center;
                    LocalViewRightConstraint.Constant =
                        containerWidth / 2f - videoFrame.Size.Width / 2f;//center;
                }
            }

            UIView.AnimateNotify(0.4f, View.LayoutIfNeeded, null);
        }

        private void ResizeRemoteView(object sender, DidChangeVideoSizeEventArgs e)
        {
            var size = e.Size;
            var containerWidth = View.Frame.Size.Width;
            var containerHeight = View.Frame.Size.Height;
            var defaultAspectRatiuo = new CGSize(4, 3);

            //Resize Remote View
            RemoteVideoSize = size;

            var aspectRatio = size == CGSize.Empty ? defaultAspectRatiuo : size;
            var videoRect = View.Bounds;
            var videoFrame = videoRect.WithAspectRatio(aspectRatio);
            if (IsZoom)
            {
                var scale = Math.Max(containerWidth / videoFrame.Size.Width,
                                     containerHeight / videoFrame.Size.Height);
                videoFrame.Size = new CGSize(videoFrame.Size.Width * scale,
                                             videoFrame.Height * scale);
            }
            var widthCenter = containerWidth / 2f - videoFrame.Size.Width / 2f;
            var heightCenter = containerHeight / 2f - videoFrame.Size.Height / 2f;
            RemoteViewTopConstraint.Constant = heightCenter;
            RemoteViewBottomConstraint.Constant = heightCenter;
            RemoteViewLeftConstraint.Constant = widthCenter;
            RemoteViewRightConstraint.Constant = widthCenter;

            UIView.AnimateNotify(0.4f, View.LayoutIfNeeded, null);
        }

        #endregion

        public override bool PrefersStatusBarHidden()
        {
            return true;
        }

        private void SetFullScreenLocalView()
        {
            LocalViewBottomConstraint.Constant = 0f;
            LocalViewRightConstraint.Constant = 0f;
            LocalViewWidthConstraint.Constant = View.Frame.Size.Width;
            LocalViewHeightConstraint.Constant = View.Frame.Size.Height;
        }

        private void OnOrientationChange(object sender, NSNotificationEventArgs notification)
        {
            ResizeLocalView(null, new DidChangeVideoSizeEventArgs(LocalVideoSize));
            ResizeRemoteView(null, new DidChangeVideoSizeEventArgs(RemoteVideoSize));
        }

        private void OnResignActive(object sender, NSNotificationEventArgs notification)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            if (Client == null)
                return;
            
            SetFullScreenLocalView();

            OverlayView.Alpha = 1f;

            LocalVideoTrack?.RemoveRenderer(LocalView);
            RemoteVideoTrack?.RemoveRenderer(RemoteView);

            LocalVideoTrack = null;
            RemoteVideoTrack = null;

            LocalView.RenderFrame(null);
            RemoteView.RenderFrame(null);

            Client.Disconnect();
        }

        private void RemoteDisconnected()
        {
            RemoteVideoTrack?.RemoveRenderer(RemoteView);
            RemoteVideoTrack = null;

            RemoteView.RenderFrame(null);
            ResizeLocalView(null, new DidChangeVideoSizeEventArgs(LocalVideoSize));
        }

        private void ToggleButtonContainer()
        {
            var isHidden = ButtonContainerViewLeftConstraint.Constant <= -40f;
            ButtonContainerViewLeftConstraint.Constant = isHidden ? 20f : -40f;
            ButtonContainerView.Alpha = isHidden ? 1f : 0f;
            UIView.AnimateNotify(0.3f, View.LayoutIfNeeded, null);
        }

        private void ZoomRemote()
        {
            IsZoom = !IsZoom;
            ResizeRemoteView(null, new DidChangeVideoSizeEventArgs(RemoteVideoSize));
        }

        private void AudioButtonPressed(object sender, EventArgs e)
        {
            Contract.Requires(Client != null);
            IsAudioMute = !IsAudioMute;
            var img = UIImage.FromBundle(IsAudioMute ? "audioOff" : "audioOn");
            AudioButton.SetImage(img, UIControlState.Normal);
            if (IsAudioMute)
                Client.MuteAudioIn();
            else
                Client.UnmuteAudioIn();
        }

        private void VideoButtonPressed(object sender, EventArgs e)
        {
            Contract.Requires(Client != null);
            IsBackCamera = !IsBackCamera;
            if (IsBackCamera)
                Client.SwapCameraToBack();
            else
                Client.SwapCameraToFront();
        }

        private void HangupButtonPressed(object sender, EventArgs e)
        {
            Disconnect();
        }

        private static CGSize GetSizeWithOrientation(CGSize size)
        {
            var orientation = UIDevice.CurrentDevice.Orientation;
            return orientation.IsLandscape() ?
                              new CGSize(size.Height / 4f, size.Width / 4f) :
                              new CGSize(size.Width / 4f, size.Height / 4f);
        }
    }
}