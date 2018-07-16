using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.XR.WSA;
using Newtonsoft.Json.Linq;
using System.IO;

#if !UNITY_EDITOR
using Windows.UI.Core;
using Windows.Foundation;
using Windows.Media.Core;
using System.Linq;
using System.Threading.Tasks;
using HoloPoseClient.Signalling;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Streams;

using Newtonsoft.Json.Linq;
#endif

public class ControlScript : MonoBehaviour
{
    public string ServerAddress = "https://purduestarproj-webrtc-signal.herokuapp.com";
    public string ServerPort = "443";
    public string ClientName = "star-mentor"; // star-trainee, star-mentor, etc

    // if this is true:
    // - this client will be able to initiate a call to a peer
    // - this client will offer its own media
    // if this is false:
    // - this client will not be able to initiate a call to a peer (it will accept incoming calls)
    // - this client will not offer its own media
    // i.e.: the trainee should have LocalStreamEnabled = true, and the mentor should have LocalStreamEnabled = false
    public bool LocalStreamEnabled = false;

    public string PreferredVideoCodec = "VP8"; // options are "VP8" and "H264". Currently (as of 5/28/2018) we only support HoloLens pose on VP8.

    public uint LocalTextureWidth = 160;
    public uint LocalTextureHeight = 120;

    public uint RemoteTextureWidth = 640;
    public uint RemoteTextureHeight = 480;

    public RawImage LocalVideoImage;
    public RawImage RemoteVideoImage;

    public InputField ServerAddressInputField;
    public InputField ServerPortInputField;
    public InputField ClientNameInputField;

    public Button ConnectButton;
    public Button CallButton;

    public RectTransform PeerContent;
    public RectTransform SelfConnectedAsContent;

    public Text PreferredCodecLabel;

    public Text LastReceivedMessageLabel;

    public Text LastPeerPoseLabel;
    public Text LastSelfPoseLabel;

    private int MainTextureWidth = 1344;
    private int MainTextureHeight = 756;
    private Texture2D MainTex, YTex, UTex, VTex;

    public GameObject TextItemPrefab;

    private enum Status
    {
        NotConnected,
        Connecting,
        Disconnecting,
        Connected,
        Calling,
        EndingCall,
        InCall
    }

    private enum CommandType
    {
        Empty,
        SetNotConnected,
        SetConnected,
        SetInCall,
        AddRemotePeer,
        RemoveRemotePeer
    }

    private struct Command
    {
        public CommandType type;
#if !UNITY_EDITOR
        public Conductor.Peer remotePeer;
#endif
    }

    private Status status = Status.NotConnected;
    private List<Command> commandQueue = new List<Command>();
    private int selectedPeerIndex = -1;

    private GameObject g_BackgroundImage;
    private GameObject StabilizedQuad;
    private GameObject g_WebRTCButton;
    private GameObject g_EventSystem;
    private TouchEvents g_EventsScript;
    public Camera mainCamera;

    private Texture2D primaryPlaybackTexture;

    public bool Hololens = true;

    public ControlScript()
    {
    }

    void Awake()
    {
    }

    void Start()
    {
        /*
        Debug.Log("NOTE: creating some mock remote peers to test UI");
        for (int i = 0; i < 5; i++)
        {
            string mockName = "mock-peer-" + UnityEngine.Random.value;
            if (i == 0)
            {
                mockName = ClientName; // testing to make sure we can't accidentally call ourselves
            }
            AddRemotePeer(mockName);
        }
        */
        if (g_BackgroundImage == null)
        {
            g_BackgroundImage = GameObject.Find("BackgroundImage");
            if (g_BackgroundImage == null)
            {
                Debug.LogError("Could not load background Image");
            }
        }

        if (StabilizedQuad == null)
        {
            StabilizedQuad = GameObject.Find("StabilizedQuad");
            if (StabilizedQuad == null)
            {
                Debug.LogError("Could not load Stabilized Quad");
            }
        }

        if (g_EventSystem == null)
        {
            g_EventSystem = GameObject.Find("EventSystem");
            if (g_EventSystem == null)
            {
                Debug.LogError("Could not load Event System");
            }
        }

        if (g_EventsScript == null)
        {
            g_EventsScript = g_EventSystem.GetComponent<TouchEvents>();
            if (g_EventsScript == null)
            {
                Debug.LogError("Could not load Event System Script");
            }
        }

        if (Hololens == true)
            Stabilization.Instance.SetPlane(StabilizedQuad);

#if !UNITY_EDITOR
        if (LocalStreamEnabled) {
            Debug.Log("because this is the TRAINEE app, we enable the local stream so we can send video to the mentor.");
        } else {
            Debug.Log("because this is the MENTOR app, we disable the local stream so we are not sending any video back to the trainee.");
        }

        Conductor.Instance.LocalStreamEnabled = LocalStreamEnabled;
#endif

#if !UNITY_EDITOR
        // Set up spatial coordinate system for sending pose metadata
        Debug.Log("setting up spatial coordinate system");
        IntPtr spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();
        if (spatialCoordinateSystemPtr.ToInt32() != 0)
        {
            Debug.Log("spatialCoordinateSystemPtr: " + spatialCoordinateSystemPtr.ToString());
            Conductor.Instance.InitializeSpatialCoordinateSystem(spatialCoordinateSystemPtr);
            Debug.Log("SetSpatialCoordinateSystem done");
        } else
        {
            Debug.Log("spatialCoordinateSystemPtr was null. Probably not running on a Mixed Reality headset. Skipping initing video pose data.");
        }
        

        Debug.Log("setting up the rest of the conductor...");

        Conductor.Instance.IncomingRawMessage += Conductor_IncomingRawMessage;
        Conductor.Instance.OnSelfRawFrame += Conductor_OnSelfRawFrame;
        Conductor.Instance.OnPeerRawFrame += Conductor_OnPeerRawFrame;

        Conductor.Instance.Initialized += Conductor_Initialized;
        Conductor.Instance.Initialize(CoreApplication.MainView.CoreWindow.Dispatcher);
        Conductor.Instance.EnableLogging(Conductor.LogLevel.Verbose);
        Debug.Log("done setting up the rest of the conductor");
#endif
        ServerAddressInputField.text = ServerAddress;
        ServerPortInputField.text = ServerPort;
        ClientNameInputField.text = ClientName;

        GameObject g_WebRTCPanel = GameObject.Find("WebRTCPanel");
        if (g_WebRTCPanel == null)
        {
            Debug.LogError("Could not load WebRTC Panel");
        }

        g_WebRTCPanel.GetComponent<CanvasGroup>().alpha = 0f;
        g_WebRTCPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
    }

    private void OnEnable()
    {
        if (LocalStreamEnabled)
        {
            Plugin.CreateLocalMediaPlayback();
            IntPtr nativeTex = IntPtr.Zero;
            Plugin.GetLocalPrimaryTexture(LocalTextureWidth, LocalTextureHeight, out nativeTex);
            var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)LocalTextureWidth, (int)LocalTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
            if (LocalVideoImage != null)
            {
                LocalVideoImage.texture = primaryPlaybackTexture;
            }
        }

        if (RemoteVideoImage != null)
        {
            Plugin.CreateRemoteMediaPlayback();
            IntPtr nativeTex = IntPtr.Zero;
            Plugin.GetRemotePrimaryTexture(RemoteTextureWidth, RemoteTextureHeight, out nativeTex);
            primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)RemoteTextureWidth, (int)RemoteTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
            RemoteVideoImage.texture = primaryPlaybackTexture;
            if (Hololens == true)
            {
                RemoteVideoImage.transform.gameObject.SetActive(false);

                MainTex = new Texture2D(MainTextureWidth, MainTextureHeight, TextureFormat.Alpha8, false);
                Stabilization.Instance.MainTex = MainTex;
                YTex = new Texture2D(MainTextureWidth, MainTextureHeight, TextureFormat.Alpha8, false);
                Stabilization.Instance.YTex = YTex;
                UTex = new Texture2D(MainTextureWidth / 2, MainTextureHeight / 2, TextureFormat.Alpha8, false);
                Stabilization.Instance.UTex = UTex;
                VTex = new Texture2D(MainTextureWidth / 2, MainTextureHeight / 2, TextureFormat.Alpha8, false);
                Stabilization.Instance.VTex = VTex;
            }
            //juan andres drone
            //byte[] imagebytes = primaryPlaybackTexture.GetRawTextureData();

        }
    }

    private void OnDisable()
    {
        if (LocalStreamEnabled)
        {
            if (LocalVideoImage != null)
            {
                LocalVideoImage.texture = null;
            }
            Plugin.ReleaseLocalMediaPlayback();
        }

        if (RemoteVideoImage != null)
        {
            RemoteVideoImage.texture = null;
        }
        Plugin.ReleaseRemoteMediaPlayback();
    }


    private void AddRemotePeer(string peerName)
    {
        bool isSelf = (peerName == ClientName); // when we connect, our own user appears as a peer. we don't want to accidentally try to call ourselves.

        Debug.Log("AddRemotePeer: " + peerName);
        GameObject textItem = (GameObject)Instantiate(TextItemPrefab);

        textItem.GetComponent<Text>().text = peerName;

        if (isSelf)
        {
            textItem.transform.SetParent(SelfConnectedAsContent, false);
        } else
        {
            textItem.transform.SetParent(PeerContent, false);

            EventTrigger trigger = textItem.GetComponentInChildren<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerDown;
            entry.callback.AddListener((data) => { OnRemotePeerItemClick((PointerEventData)data); });
            trigger.triggers.Add(entry);

            if (selectedPeerIndex == -1)
            {
                textItem.GetComponent<Text>().fontStyle = FontStyle.Bold;
                selectedPeerIndex = PeerContent.transform.childCount - 1;
            }
        }
    }

    private void RemoveRemotePeer(string peerName)
    {
        bool isSelf = (peerName == ClientName); // when we connect, our own user appears as a peer. we don't want to accidentally try to call ourselves.

        Debug.Log("RemoveRemotePeer: " + peerName);

        if (isSelf)
        {
            for (int i = 0; i < SelfConnectedAsContent.transform.childCount; i++)
            {
                if (SelfConnectedAsContent.GetChild(i).GetComponent<Text>().text == peerName)
                {
                    SelfConnectedAsContent.GetChild(i).SetParent(null);
                    break;
                }
            }
        } else
        {
            for (int i = 0; i < PeerContent.transform.childCount; i++)
            {
                if (PeerContent.GetChild(i).GetComponent<Text>().text == peerName)
                {
                    PeerContent.GetChild(i).SetParent(null);
                    if (selectedPeerIndex == i)
                    {
                        if (PeerContent.transform.childCount > 0)
                        {
                            PeerContent.GetChild(0).GetComponent<Text>().fontStyle = FontStyle.Bold;
                            selectedPeerIndex = 0;
                        }
                        else
                        {
                            selectedPeerIndex = -1;
                        }
                    }
                    break;
                }
            }
        }
    }


    private void Update()
    {
        //LastPeerPoseLabel.text = Plugin.getFloat().toString();
        //Plugin.initChessPoseController();

        lock (this)
        {
            switch (status)
            {
                case Status.NotConnected:
                    if (!ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = true;
                    if (!ConnectButton.enabled)
                        ConnectButton.enabled = true;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.Connecting:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.Disconnecting:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.Connected:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (!ConnectButton.enabled)
                        ConnectButton.enabled = true;
                    if (CallButton.enabled != LocalStreamEnabled)
                        CallButton.enabled = LocalStreamEnabled; // only allow pressing the Call button (when not in a call) if our client is set up to initiate a call
                    break;
                case Status.Calling:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.EndingCall:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.InCall:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (!CallButton.enabled)
                        CallButton.enabled = true;
                    break;
                default:
                    break;
            }

#if !UNITY_EDITOR
            while (commandQueue.Count != 0)
            {
                Command command = commandQueue.First();
                commandQueue.RemoveAt(0);
                switch (status)
                {
                    case Status.NotConnected:
                        if (command.type == CommandType.SetNotConnected)
                        {
                            ConnectButton.GetComponentInChildren<Text>().text = "Connect";
                            CallButton.GetComponentInChildren<Text>().text = "Call";
                        }
                        break;
                    case Status.Connected:
                        if (command.type == CommandType.SetConnected)
                        {
                            ConnectButton.GetComponentInChildren<Text>().text = "Disconnect";
                            CallButton.GetComponentInChildren<Text>().text = "Call";
                        }
                        break;
                    case Status.InCall:
                        if (command.type == CommandType.SetInCall)
                        {
                            ConnectButton.GetComponentInChildren<Text>().text = "Disconnect";
                            CallButton.GetComponentInChildren<Text>().text = "Hang Up";
                        }
                        break;
                    default:
                        break;
                }
                if (command.type == CommandType.AddRemotePeer)
                {
                    string remotePeerName = command.remotePeer.Name;
                    AddRemotePeer(remotePeerName);
                }
                else if (command.type == CommandType.RemoveRemotePeer)
                {
                    string remotePeerName = command.remotePeer.Name;
                    RemoveRemotePeer(remotePeerName);
                }
            }
#endif
        }
    }

    int counter = 0;

    // fired whenever we get a video frame from the remote peer.
    // if there is pose data, posXYZ and rotXYZW will have non-zero values.
    private void Conductor_OnPeerRawFrame(uint width, uint height,
            byte[] yPlane, uint yPitch, byte[] vPlane, uint vPitch, byte[] uPlane, uint uPitch,
            float posX, float posY, float posZ, float rotX, float rotY, float rotZ, float rotW)
    {
        if (Hololens == true)
        {
            if (g_EventsScript.isUserAnnotating == false)
            {
                UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                {
                //Set property on UI thread
                //Debug.Log("ControlScript: OnPeerRawFrame " + width + " " + height + " " + posX + " " + posY + " " + posZ + " " + rotX + " " + rotY + " " + rotZ + " " + rotW);

                if (LastPeerPoseLabel != null)
                    {
                        LastPeerPoseLabel.text = posX + " " + posY + " " + posZ + "\n" + rotX + " " + rotY + " " + rotZ + " " + rotW;
                    }

                    Stabilization.Instance.Stablize(new Quaternion(rotX, rotY, rotZ, rotW), new Vector3(posX, posY, posZ));
                    YTex.LoadRawTextureData(yPlane);
                    YTex.Apply();
                    UTex.LoadRawTextureData(uPlane);
                    UTex.Apply();
                    VTex.LoadRawTextureData(vPlane);
                    VTex.Apply();

                    /*byte[] bytes = Stabilization.Instance.MainTex.EncodeToPNG();

#if !UNITY_EDITOR
                    if (bytes != null)
                    {
                        StorageFolder rootFolder = ApplicationData.Current.LocalFolder;
                        StorageFile sampleFile = await rootFolder.CreateFileAsync("testVideo"+counter.ToString()+".png", CreationCollisionOption.ReplaceExisting);
                        File.WriteAllBytes(sampleFile.Path, bytes);
                        counter++;
                    }*
#endif*/
                }, false);
                //GetComponent<Renderer>().material.mainTexture = tex;
                //Debug.Log(posX + " " + posY + " " + posZ + "\n" + rotX + " " + rotY + " " + rotZ + " " + rotW);

                //juan andres drone;
                //var primaryPlaybackTexture2 = Texture2D.CreateExternalTexture((int)RemoteTextureWidth, (int)RemoteTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
                //Stabilization.Instance.SetTexture(primaryPlaybackTexture2);
                //byte[] imagebytes = RemoteVideoImage.texture.GetRawTextureData();
            }
        }
    }

    // fired whenever we encode one of our own video frames before sending it to the remote peer.
    // if there is pose data, posXYZ and rotXYZW will have non-zero values.
    private void Conductor_OnSelfRawFrame(uint width, uint height,
            byte[] yPlane, uint yPitch, byte[] vPlane, uint vPitch, byte[] uPlane, uint uPitch,
            float posX, float posY, float posZ, float rotX, float rotY, float rotZ, float rotW)
    {
        UnityEngine.WSA.Application.InvokeOnAppThread(() =>
        {
            //Set property on UI thread
            //Debug.Log("ControlScript: OnSelfRawFrame " + width + " " + height + " " + posX + " " + posY + " " + posZ + " " + rotX + " " + rotY + " " + rotZ + " " + rotW);

            if (LastSelfPoseLabel != null)
            {
                LastSelfPoseLabel.text = posX + " " + posY + " " + posZ + "\n" + rotX + " " + rotY + " " + rotZ + " " + rotW;
            }
        }, false);
    }

    private void Conductor_IncomingRawMessage(string rawMessageString)
    {
        if (Hololens == true)
        {
            JObject message = JObject.Parse(rawMessageString);
            if ((string)message["type"] == "I")
            {
                int CameraWidth = Convert.ToInt32((string)message["camera"]["width"]);
                int CameraHeight = Convert.ToInt32((string)message["camera"]["height"]);
                float Camerafx = Convert.ToSingle((string)message["camera"]["fx"]);
                float Camerafy = Convert.ToSingle((string)message["camera"]["fy"]);
                float Cameracx = Convert.ToSingle((string)message["camera"]["cx"]);
                float Cameracy = Convert.ToSingle((string)message["camera"]["cy"]);

                JArray jsonarray = (JArray)message["plane"];

                Vector4 col1 = new Vector4(Convert.ToSingle((string)jsonarray[0]), Convert.ToSingle((string)jsonarray[4]), Convert.ToSingle((string)jsonarray[8]), Convert.ToSingle((string)jsonarray[12]));
                Vector4 col2 = new Vector4(Convert.ToSingle((string)jsonarray[1]), Convert.ToSingle((string)jsonarray[5]), Convert.ToSingle((string)jsonarray[9]), Convert.ToSingle((string)jsonarray[13]));
                Vector4 col3 = new Vector4(Convert.ToSingle((string)jsonarray[2]), Convert.ToSingle((string)jsonarray[6]), Convert.ToSingle((string)jsonarray[10]), Convert.ToSingle((string)jsonarray[14]));
                Vector4 col4 = new Vector4(Convert.ToSingle((string)jsonarray[3]), Convert.ToSingle((string)jsonarray[7]), Convert.ToSingle((string)jsonarray[11]), Convert.ToSingle((string)jsonarray[15]));
                Matrix4x4 planePoints = new Matrix4x4(col1, col2, col3, col4);

                JArray jsonarray2 = (JArray)message["cameraMatrix"];

                Vector4 orgcol1 = new Vector4(Convert.ToSingle((string)jsonarray2[0]), Convert.ToSingle((string)jsonarray2[4]), Convert.ToSingle((string)jsonarray2[8]), Convert.ToSingle((string)jsonarray2[12]));
                Vector4 orgcol2 = new Vector4(Convert.ToSingle((string)jsonarray2[1]), Convert.ToSingle((string)jsonarray2[5]), Convert.ToSingle((string)jsonarray2[9]), Convert.ToSingle((string)jsonarray2[13]));
                Vector4 orgcol3 = new Vector4(Convert.ToSingle((string)jsonarray2[2]), Convert.ToSingle((string)jsonarray2[6]), Convert.ToSingle((string)jsonarray2[10]), Convert.ToSingle((string)jsonarray2[14]));
                Vector4 orgcol4 = new Vector4(Convert.ToSingle((string)jsonarray2[3]), Convert.ToSingle((string)jsonarray2[7]), Convert.ToSingle((string)jsonarray2[11]), Convert.ToSingle((string)jsonarray2[15]));
                Matrix4x4 planePoints2 = new Matrix4x4(orgcol1, orgcol2, orgcol3, orgcol4);

                UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                {
                    Stabilization.Instance.InitCamera(CameraWidth, CameraHeight, Camerafx, Camerafy, Cameracx, Cameracy);
                    Stabilization.Instance.InitPlane(planePoints);
                    Stabilization.Instance.MainCamera = planePoints2;
                    Graphics.CopyTexture(YTex, MainTex);
                    mainCamera.transform.SetPositionAndRotation(planePoints2.MultiplyPoint(Vector3.zero), Quaternion.LookRotation(planePoints2.GetColumn(2), planePoints2.GetColumn(1)));
                }, false);

                //Debug.Log(CameraWidth + " " + CameraHeight + " " + Camerafx + " " + Camerafy + " " + Cameracx + " " + Cameracy);
            }
            else
            {
                JArray jsonarray = (JArray)message["cameraMatrix"];
                Vector4 col1 = new Vector4(Convert.ToSingle((string)jsonarray[0]), Convert.ToSingle((string)jsonarray[4]), Convert.ToSingle((string)jsonarray[8]), Convert.ToSingle((string)jsonarray[12]));
                Vector4 col2 = new Vector4(Convert.ToSingle((string)jsonarray[1]), Convert.ToSingle((string)jsonarray[5]), Convert.ToSingle((string)jsonarray[9]), Convert.ToSingle((string)jsonarray[13]));
                Vector4 col3 = new Vector4(Convert.ToSingle((string)jsonarray[2]), Convert.ToSingle((string)jsonarray[6]), Convert.ToSingle((string)jsonarray[10]), Convert.ToSingle((string)jsonarray[14]));
                Vector4 col4 = new Vector4(Convert.ToSingle((string)jsonarray[3]), Convert.ToSingle((string)jsonarray[7]), Convert.ToSingle((string)jsonarray[11]), Convert.ToSingle((string)jsonarray[15]));
                Matrix4x4 pose = new Matrix4x4(col1, col2, col3, col4);
                Stabilization.Instance.Stablize(pose);

                //Debug.Log(pose.ToString());
            }
        }
    }

    private void Conductor_Initialized(bool succeeded)
    {
        if (succeeded)
        {
            Initialize();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Conductor initialization failed");
        }
    }

    public void OnConnectClick()
    {
#if !UNITY_EDITOR
        lock (this)
        {
            if (status == Status.NotConnected)
            {
                new Task(() =>
                {
                    Conductor.Instance.StartLogin(ServerAddressInputField.text, ServerPortInputField.text, ClientNameInputField.text);
                }).Start();
                g_BackgroundImage.SetActive(false);
                ConnectButton.gameObject.SetActive(false);
                status = Status.Connecting;
                
            }
            else if (status == Status.Connected)
            {
                new Task(() =>
                {
                    var task = Conductor.Instance.DisconnectFromServer();
                }).Start();

                status = Status.Disconnecting;
                selectedPeerIndex = -1;
                PeerContent.DetachChildren();
                g_BackgroundImage.SetActive(true);
                SelfConnectedAsContent.DetachChildren();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OnConnectClick() - wrong status - " + status);
            }
        }
#endif
    }

    // Sends a test JSON message to the peer with which we are connected. Requires that we be both connected to the server and "in a call" with another peer before we can send.
    public void OnSendTestMessageClick()
    {
        lock (this)
        {
            if (status == Status.InCall)
            {
                // NOTE: this is the raw message to be sent
                //
                JObject messageToSend = new JObject();
                messageToSend["hello"] = "world";
                messageToSend["timestamp"] = Time.time;
                //


                // To handle the message properly, it should be wrapped in an outer JSON object where the "message" key points to your actual message.
                JObject messageContainer = new JObject();
                messageContainer["message"] = messageToSend;

                string jsonString = messageContainer.ToString();

                Debug.Log("sending test message " + jsonString);
#if !UNITY_EDITOR
                Conductor.Instance.SendMessage(Windows.Data.Json.JsonObject.Parse(jsonString));
#endif
            }
            else
            {
                Debug.LogError("attempted to send test message while not in call");
            }
        }
    }

    public void OnCallClick()
    {
#if !UNITY_EDITOR
        lock (this)
        {
            if (status == Status.Connected)
            {
                if (selectedPeerIndex == -1)
                    return;

                Debug.Log("selectedPeerIndex: " + selectedPeerIndex);
                string selectedRemotePeerName = PeerContent.GetChild(selectedPeerIndex).GetComponent<Text>().text;
                Debug.Log("selectedRemotePeerName: " + selectedRemotePeerName);

                new Task(() =>
                {
                    // given the selectedPeerIndex, find which remote peer that matches. 
                    // Note: it's not just that index in Conductor.Instance.GetPeers() because that list contains both remote peers and ourselves.
                    Conductor.Peer selectedConductorPeer = null;

                    var conductorPeers = Conductor.Instance.GetPeers();
                    foreach (var conductorPeer in conductorPeers)
                    {
                        if (conductorPeer.Name == selectedRemotePeerName)
                        {
                            selectedConductorPeer = conductorPeer;
                            break;
                        }
                    }

                    Debug.Log("selectedConductorPeer: " + selectedConductorPeer.Name);
                    Debug.Log("going to try to connect to peer");

                    if (selectedConductorPeer != null)
                    {
                        Conductor.Instance.ConnectToPeer(selectedConductorPeer);
                    }
                }).Start();
                status = Status.Calling;
            }
            else if (status == Status.InCall)
            {
                new Task(() =>
                {
                    var task = Conductor.Instance.DisconnectFromPeer();
                }).Start();
                status = Status.EndingCall;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OnCallClick() - wrong status - " + status);
            }
        }
#endif
    }

    public void OnRemotePeerItemClick(PointerEventData data)
    {
        for (int i = 0; i < PeerContent.transform.childCount; i++)
        {
            if (PeerContent.GetChild(i) == data.selectedObject.transform)
            {
                data.selectedObject.GetComponent<Text>().fontStyle = FontStyle.Bold;
                selectedPeerIndex = i;
            }
            else
            {
                PeerContent.GetChild(i).GetComponent<Text>().fontStyle = FontStyle.Normal;
            }
        }
    }

#if !UNITY_EDITOR
    public async Task OnAppSuspending()
    {
        Conductor.Instance.CancelConnectingToPeer();

        await Conductor.Instance.DisconnectFromPeer();
        await Conductor.Instance.DisconnectFromServer();

        Conductor.Instance.OnAppSuspending();
    }

    private IAsyncAction RunOnUiThread(Action fn)
    {
        return CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(fn));
    }
#endif

    public void Initialize()
    {
#if !UNITY_EDITOR
        // A Peer is connected to the server event handler
        Conductor.Instance.Signaller.OnPeerConnected += (peerId, peerName) =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    Conductor.Peer peer = new Conductor.Peer { Id = peerId, Name = peerName };
                    Conductor.Instance.AddPeer(peer);
                    commandQueue.Add(new Command { type = CommandType.AddRemotePeer, remotePeer = peer });
                }
            });
        };

        // A Peer is disconnected from the server event handler
        Conductor.Instance.Signaller.OnPeerDisconnected += peerId =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    var peerToRemove = Conductor.Instance.GetPeers().FirstOrDefault(p => p.Id == peerId);
                    if (peerToRemove != null)
                    {
                        Conductor.Peer peer = new Conductor.Peer { Id = peerToRemove.Id, Name = peerToRemove.Name };
                        Conductor.Instance.RemovePeer(peer);
                        commandQueue.Add(new Command { type = CommandType.RemoveRemotePeer, remotePeer = peer });
                    }
                }
            });
        };

        // The user is Signed in to the server event handler
        Conductor.Instance.Signaller.OnSignedIn += () =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Connecting)
                    {
                        status = Status.Connected;
                        commandQueue.Add(new Command { type = CommandType.SetConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnSignedIn() - wrong status - " + status);
                    }
                }
            });
        };

        // Failed to connect to the server event handler
        Conductor.Instance.Signaller.OnServerConnectionFailure += () =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Connecting)
                    {
                        status = Status.NotConnected;
                        commandQueue.Add(new Command { type = CommandType.SetNotConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnServerConnectionFailure() - wrong status - " + status);
                    }
                }
            });
        };

        // The current user is disconnected from the server event handler
        Conductor.Instance.Signaller.OnDisconnected += () =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Disconnecting)
                    {
                        status = Status.NotConnected;
                        commandQueue.Add(new Command { type = CommandType.SetNotConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnDisconnected() - wrong status - " + status);
                    }
                }
            });
        };

        Conductor.Instance.OnAddRemoteStream += Conductor_OnAddRemoteStream;
        Conductor.Instance.OnRemoveRemoteStream += Conductor_OnRemoveRemoteStream;
        Conductor.Instance.OnAddLocalStream += Conductor_OnAddLocalStream;

        // Connected to a peer event handler
        Conductor.Instance.OnPeerConnectionCreated += () =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Calling)
                    {
                        status = Status.InCall;
                        commandQueue.Add(new Command { type = CommandType.SetInCall });
                    }
                    else if (status == Status.Connected)
                    {
                        status = Status.InCall;
                        commandQueue.Add(new Command { type = CommandType.SetInCall });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Conductor.OnPeerConnectionCreated() - wrong status - " + status);
                    }
                }
            });
        };

        // Connection between the current user and a peer is closed event handler
        Conductor.Instance.OnPeerConnectionClosed += () =>
        {
            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.EndingCall)
                    {
                        Plugin.UnloadLocalMediaStreamSource();
                        Plugin.UnloadRemoteMediaStreamSource();
                        status = Status.Connected;
                        commandQueue.Add(new Command { type = CommandType.SetConnected });
                    }
                    else if (status == Status.InCall)
                    {
                        Plugin.UnloadLocalMediaStreamSource();
                        Plugin.UnloadRemoteMediaStreamSource();
                        status = Status.Connected;
                        commandQueue.Add(new Command { type = CommandType.SetConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Conductor.OnPeerConnectionClosed() - wrong status - " + status);
                    }
                }
            });
        };

        // Ready to connect to the server event handler
        Conductor.Instance.OnReadyToConnect += () => { var task = RunOnUiThread(() => { }); };

        List<Conductor.IceServer> iceServers = new List<Conductor.IceServer>();
        iceServers.Add(new Conductor.IceServer { Host = "stun.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
        iceServers.Add(new Conductor.IceServer { Host = "stun1.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
        iceServers.Add(new Conductor.IceServer { Host = "stun2.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
        iceServers.Add(new Conductor.IceServer { Host = "stun3.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
        iceServers.Add(new Conductor.IceServer { Host = "stun4.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
        Conductor.IceServer turnServer = new Conductor.IceServer { Host = "turnserver3dstreaming.centralus.cloudapp.azure.com:5349", Type = Conductor.IceServer.ServerType.TURN };
        turnServer.Credential = "3Dtoolkit072017";
        turnServer.Username = "user";
        iceServers.Add(turnServer);
        Conductor.Instance.ConfigureIceServers(iceServers);

        var audioCodecList = Conductor.Instance.GetAudioCodecs();
        Conductor.Instance.AudioCodec = audioCodecList.FirstOrDefault(c => c.Name == "opus");
        System.Diagnostics.Debug.WriteLine("Selected audio codec - " + Conductor.Instance.AudioCodec.Name);

        var videoCodecList = Conductor.Instance.GetVideoCodecs();
        Conductor.Instance.VideoCodec = videoCodecList.FirstOrDefault(c => c.Name == PreferredVideoCodec);

        UnityEngine.WSA.Application.InvokeOnAppThread(() =>
        {
            //Set property on UI thread
            PreferredCodecLabel.text = Conductor.Instance.VideoCodec.Name;
        }, false);

        System.Diagnostics.Debug.WriteLine("Selected video codec - " + Conductor.Instance.VideoCodec.Name);

        uint preferredWidth = 896;
        uint preferredHeght = 504;
        uint preferredFrameRate = 15;
        uint minSizeDiff = uint.MaxValue;
        Conductor.CaptureCapability selectedCapability = null;
        var videoDeviceList = Conductor.Instance.GetVideoCaptureDevices();
        foreach (Conductor.MediaDevice device in videoDeviceList)
        {
            Conductor.Instance.GetVideoCaptureCapabilities(device.Id).AsTask().ContinueWith(capabilities =>
            {
                foreach (Conductor.CaptureCapability capability in capabilities.Result)
                {
                    uint sizeDiff = (uint)Math.Abs(preferredWidth - capability.Width) + (uint)Math.Abs(preferredHeght - capability.Height);
                    if (sizeDiff < minSizeDiff)
                    {
                        selectedCapability = capability;
                        minSizeDiff = sizeDiff;
                    }
                    System.Diagnostics.Debug.WriteLine("Video device capability - " + device.Name + " - " + capability.Width + "x" + capability.Height + "@" + capability.FrameRate);
                }
            }).Wait();
        }

        if (selectedCapability != null)
        {
            selectedCapability.FrameRate = preferredFrameRate;
            Conductor.Instance.VideoCaptureProfile = selectedCapability;
            Conductor.Instance.UpdatePreferredFrameFormat();
            System.Diagnostics.Debug.WriteLine("Selected video device capability - " + selectedCapability.Width + "x" + selectedCapability.Height + "@" + selectedCapability.FrameRate);
        }

#endif
    }

    private void Conductor_OnAddRemoteStream()
    {
#if !UNITY_EDITOR
        var task = RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                    IMediaSource source;
                    if (Conductor.Instance.VideoCodec.Name == "H264")
                        source = Conductor.Instance.CreateRemoteMediaStreamSource("H264");
                    else
                        source = Conductor.Instance.CreateRemoteMediaStreamSource("I420");
                    Plugin.LoadRemoteMediaStreamSource((MediaStreamSource)source);
                }
                else if (status == Status.Connected)
                {
                    IMediaSource source;
                    if (Conductor.Instance.VideoCodec.Name == "H264")
                        source = Conductor.Instance.CreateRemoteMediaStreamSource("H264");
                    else
                        source = Conductor.Instance.CreateRemoteMediaStreamSource("I420");
                    Plugin.LoadRemoteMediaStreamSource((MediaStreamSource)source);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnAddRemoteStream() - wrong status - " + status);
                }
            }
        });
#endif
    }

    private void Conductor_OnRemoveRemoteStream()
    {
#if !UNITY_EDITOR
        var task = RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                }
                else if (status == Status.Connected)
                {
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnRemoveRemoteStream() - wrong status - " + status);
                }
            }
        });
#endif
    }

    private void Conductor_OnAddLocalStream()
    {
#if !UNITY_EDITOR
        var task = RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                    var source = Conductor.Instance.CreateLocalMediaStreamSource("I420");
                    Plugin.LoadLocalMediaStreamSource((MediaStreamSource)source);

                    Conductor.Instance.EnableLocalVideoStream();
                    Conductor.Instance.UnmuteMicrophone();
                }
                else if (status == Status.Connected)
                {
                    var source = Conductor.Instance.CreateLocalMediaStreamSource("I420");
                    Plugin.LoadLocalMediaStreamSource((MediaStreamSource)source);

                    Conductor.Instance.EnableLocalVideoStream();
                    Conductor.Instance.UnmuteMicrophone();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnAddLocalStream() - wrong status - " + status);
                }
            }
        });
#endif
    }

    private static class Plugin
    {
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateLocalMediaPlayback")]
        internal static extern void CreateLocalMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateRemoteMediaPlayback")]
        internal static extern void CreateRemoteMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "ReleaseLocalMediaPlayback")]
        internal static extern void ReleaseLocalMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "ReleaseRemoteMediaPlayback")]
        internal static extern void ReleaseRemoteMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetLocalPrimaryTexture")]
        internal static extern void GetLocalPrimaryTexture(UInt32 width, UInt32 height, out System.IntPtr playbackTexture);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetRemotePrimaryTexture")]
        internal static extern void GetRemotePrimaryTexture(UInt32 width, UInt32 height, out System.IntPtr playbackTexture);


        [DllImport("HoloOpenCVHelper", CallingConvention = CallingConvention.StdCall, EntryPoint = "initChessPoseController")]
        internal static extern void initChessPoseController();

#if !UNITY_EDITOR
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadLocalMediaStreamSource")]
        internal static extern void LoadLocalMediaStreamSource(MediaStreamSource IMediaSourceHandler);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "UnloadLocalMediaStreamSource")]
        internal static extern void UnloadLocalMediaStreamSource();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadRemoteMediaStreamSource")]
        internal static extern void LoadRemoteMediaStreamSource(MediaStreamSource IMediaSourceHandler);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "UnloadRemoteMediaStreamSource")]
        internal static extern void UnloadRemoteMediaStreamSource();
#endif

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LocalPlay")]
        internal static extern void LocalPlay();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "RemotePlay")]
        internal static extern void RemotePlay();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LocalPause")]
        internal static extern void LocalPause();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "RemotePause")]
        internal static extern void RemotePause();
    }
}
