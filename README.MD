# AppRTC - iOS implementation of the Google WebRTC Demo

## About
A native iOS video chat app based on WebRTC using Xamarin based on https://github.com/ISBX/apprtc-ios

#### Initialize SSL Peer Connection
WebRTC can communicate securely over SSL. This is required if you want to test over https://apprtc.appspot.com. You'll need to modify your `AppDelegate.cs` class with the following:

1. Import the RTCPeerConnectionFactory.h
```csharp
using WebRTCBinding;
```

2. Add the following to your `FinishedLaunching(UIApplication application, NSDictionary launchOptions)` method:
```csharp
RTCPeerConnectionFactory.InitializeSSL();
```

3. Add the following to your `WillTerminate(UIApplication application)` method:
```objective-c
RTCPeerConnectionFactory.DeinitializeSSL();
```

#### Add Video Chat
To add video chat to your app you will need 2 views:
* Local Video View - Where the video is rendered from your device camera
* Remote Video View - where the video is rendered for the remote camera

To do this, perform the following:

1. In your ViewController or whatever class you are using that contains the 2 views defined above add the following headers imports:
```csharp
using WebRTCBinding;
using AppRTC;
```

2. The class should implement the `IARDAppClientDelegate` and `IRTCEAGLVideoViewDelegate` interfaces:
```csharp
  class ARTCVideoChatViewController : UIViewController, IARDAppClientDelegate, IRTCEAGLVideoViewDelegate
```
* `IARDAppClientDelegate` - Handles events when remote client connects and disconnect states. Also, handles events when local and remote video feeds are received.
* `IRTCEAGLVideoViewDelegate` - Handles event for determining the video frame size.

3. Define the following properties in your class:
```csharp
  private ARDAppClient Client { get; set; }
  private RTCVideoTrack LocalVideoTrack { get; set; }
  private RTCVideoTrack RemoteVideoTrack { get; set; }
  private RTCEAGLVideoView LocalView { get; set; }//Outlet from storyboard or need to be created
  private RTCEAGLVideoView RemoteView { get; set; }//Outlet from storyboard or need to be created
```
* *ARDAppClient* - Performs the connection to the AppRTC Server and joins the chat room
* *RemoteView* - Renders the Remote Video in the view
* *LocalView* - Renders the Local Video in the view

4. When initializing the the property variables make sure to set the delegates:
```csharp
  /* Initializes the ARDAppClient with the delegate assignment */
  Client = new ARDAppClient(this);

  /* RTCEAGLVideoViewDelegate provides notifications on video frame dimensions */
  RemoteView.WeakDelegate = this;
  LocalView.WeakDelegate = this;
```

5. Connect to a Video Chat Room
```csharp
  Client.SetServerHostUrl(ServerHostUrl);
  Client.ConnectToRoomWithId(RoomName);
```

6. Handle the delegate methods for `ARDAppClientDelegate`
```csharp
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
public void DidReceiveLocalVideoTrack(IARDAppClient client, RTCVideoTrack localVideoTrack)
{
  LocalVideoTrack = localVideoTrack;
  LocalVideoTrack.AddRenderer(LocalView);
}

public void DidReceiveRemoteVideoTrack(IARDAppClient client, RTCVideoTrack remoteVideoTrack)
{
  RemoteVideoTrack = remoteVideoTrack;
  RemoteVideoTrack.AddRenderer(RemoteView);
}
public void DidError(IARDAppClient client, NSError error)
{
  /* Handle the error */
}
```

7. Handle the delegate callbacks for `RTCEAGLVideoViewDelegate`
```csharp
[Export("videoView:didChangeVideoSize:")]
public void DidChangeVideoSize(RTCEAGLVideoView videoView, CGSize size)
{
  /* resize LocalView or RemoteView based on the size returned */
}
```
