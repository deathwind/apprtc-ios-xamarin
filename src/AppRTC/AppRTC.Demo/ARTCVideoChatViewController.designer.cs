// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace AppRTC.Demo
{
	[Register ("ARTCVideoChatViewController")]
	partial class ARTCVideoChatViewController
	{
		[Outlet]
		UIKit.UIButton AudioButton { get; set; }

		[Outlet]
		UIKit.UIView ButtonContainerView { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint ButtonContainerViewLeftConstraint { get; set; }

		[Outlet]
		UIKit.UIView ChatView { get; set; }

		[Outlet]
		UIKit.UIView FooterView { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint FooterViewBottomConstraint { get; set; }

		[Outlet]
		UIKit.UIButton HangupButton { get; set; }

		[Outlet]
		UIKit.UIButton JoinButton { get; set; }

		[Outlet]
		UIKit.UIView JoinContainerView { get; set; }

		[Outlet]
		WebRTCBinding.RTCEAGLVideoView LocalView { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint LocalViewBottomConstraint { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint LocalViewHeightConstraint { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint LocalViewRightConstraint { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint LocalViewWidthConstraint { get; set; }

		[Outlet]
		UIKit.UIView OverlayView { get; set; }

		[Outlet]
		WebRTCBinding.RTCEAGLVideoView RemoteView { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint RemoteViewBottomConstraint { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint RemoteViewLeftConstraint { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint RemoteViewRightConstraint { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint RemoteViewTopConstraint { get; set; }

		[Outlet]
		UIKit.UITextField RoomTextField { get; set; }

		[Outlet]
		UIKit.UILabel UrlLabel { get; set; }

		[Outlet]
		UIKit.UIButton VideoButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (JoinContainerView != null) {
				JoinContainerView.Dispose ();
				JoinContainerView = null;
			}

			if (ChatView != null) {
				ChatView.Dispose ();
				ChatView = null;
			}

			if (OverlayView != null) {
				OverlayView.Dispose ();
				OverlayView = null;
			}

			if (JoinButton != null) {
				JoinButton.Dispose ();
				JoinButton = null;
			}

			if (RoomTextField != null) {
				RoomTextField.Dispose ();
				RoomTextField = null;
			}

			if (AudioButton != null) {
				AudioButton.Dispose ();
				AudioButton = null;
			}

			if (ButtonContainerView != null) {
				ButtonContainerView.Dispose ();
				ButtonContainerView = null;
			}

			if (ButtonContainerViewLeftConstraint != null) {
				ButtonContainerViewLeftConstraint.Dispose ();
				ButtonContainerViewLeftConstraint = null;
			}

			if (FooterView != null) {
				FooterView.Dispose ();
				FooterView = null;
			}

			if (FooterViewBottomConstraint != null) {
				FooterViewBottomConstraint.Dispose ();
				FooterViewBottomConstraint = null;
			}

			if (HangupButton != null) {
				HangupButton.Dispose ();
				HangupButton = null;
			}

			if (LocalView != null) {
				LocalView.Dispose ();
				LocalView = null;
			}

			if (LocalViewBottomConstraint != null) {
				LocalViewBottomConstraint.Dispose ();
				LocalViewBottomConstraint = null;
			}

			if (LocalViewHeightConstraint != null) {
				LocalViewHeightConstraint.Dispose ();
				LocalViewHeightConstraint = null;
			}

			if (LocalViewRightConstraint != null) {
				LocalViewRightConstraint.Dispose ();
				LocalViewRightConstraint = null;
			}

			if (LocalViewWidthConstraint != null) {
				LocalViewWidthConstraint.Dispose ();
				LocalViewWidthConstraint = null;
			}

			if (RemoteView != null) {
				RemoteView.Dispose ();
				RemoteView = null;
			}

			if (RemoteViewBottomConstraint != null) {
				RemoteViewBottomConstraint.Dispose ();
				RemoteViewBottomConstraint = null;
			}

			if (RemoteViewLeftConstraint != null) {
				RemoteViewLeftConstraint.Dispose ();
				RemoteViewLeftConstraint = null;
			}

			if (RemoteViewRightConstraint != null) {
				RemoteViewRightConstraint.Dispose ();
				RemoteViewRightConstraint = null;
			}

			if (RemoteViewTopConstraint != null) {
				RemoteViewTopConstraint.Dispose ();
				RemoteViewTopConstraint = null;
			}

			if (UrlLabel != null) {
				UrlLabel.Dispose ();
				UrlLabel = null;
			}

			if (VideoButton != null) {
				VideoButton.Dispose ();
				VideoButton = null;
			}
		}
	}
}
