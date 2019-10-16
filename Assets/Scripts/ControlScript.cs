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
using Windows.Graphics.Imaging;

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
    public bool LocalStreamEnabled = true;

    public string PreferredVideoCodec = "VP8"; // options are "VP8" and "H264". Currently (as of 5/28/2018) we only support HoloLens pose on VP8.

    public uint LocalTextureWidth = 160;
    public uint LocalTextureHeight = 120;

    public uint RemoteTextureWidth = 640;
    public uint RemoteTextureHeight = 480;

    public float OrgUltrasoundTextX = 1636f;
    public float OrgUltrasoundTextY = 752f;

    /*public RawImage LocalVideoImage;
    public RawImage RemoteVideoImage;*/

    public RawImage LocalVideoImage_StarMentor;
    private const string StarMentorName = "star-mentor";

    public RawImage RemoteVideoImage_StarTrainee;
    private const string StarTraineeName = "star-trainee";

    public RawImage RemoteVideoImage_StarTrainee2;
    private const string StarTrainee2Name = "star-trainee2";

    public string RemoteNameForOfferingLocalStream = StarTraineeName;

    private const string LocalName = StarMentorName; // change for trainee

    public Dictionary<string, uint> SourceIDs = new Dictionary<string, uint> { { StarMentorName, 0 }, { StarTraineeName, 1 }, { StarTrainee2Name, 2 } };
    
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

    private int MainTextureWidth = 896;
    private int MainTextureHeight = 504;
    private Texture2D MainTex, YTex, UTex, VTex;

    protected float LastUpdate = 0f;

    private byte[] g_plane;

    public GameObject TextItemPrefab;

    private bool g_SavePoseInfo;

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
    private GameObject g_UltrasoundButton;
    private GameObject g_EventSystem;
    private TouchEvents g_EventsScript;
    private GameObject g_VSPlacerSystem;
    private VitalSignsPlacer g_VSPlacerScript;
    private GameObject g_UltrasoundProbe;
    private GameObject g_UI;
    public Camera mainCamera;

    private Texture2D primaryPlaybackTexture;

    public bool Hololens = true;

    private FileStream VideoFile;
    private BinaryWriter videoWriter;

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
        
        /*if (g_UltrasoundProbe == null)
        {
            g_UltrasoundProbe = GameObject.Find("UltrasoundProbe");
            if (g_UltrasoundProbe == null)
            {
                Debug.LogError("Could not load Ultrasound Probe");
            }
        }*/
        

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

        if (g_VSPlacerSystem == null)
        {
            g_VSPlacerSystem = GameObject.Find("VitalSigns");
            if (g_VSPlacerSystem == null)
            {
                Debug.LogError("Could not load Vital Signs Placer");
            }
        }

        if (g_VSPlacerScript == null)
        {
            g_VSPlacerScript = g_VSPlacerSystem.GetComponent<VitalSignsPlacer>();
            if (g_VSPlacerScript == null)
            {
                Debug.LogError("Could not load Vital Signs Placer Script");
            }
        }

        if (g_UltrasoundButton == null)
        {
            g_UltrasoundButton = GameObject.Find("Show/Hide Ultrasound Button");
            if (g_UltrasoundButton == null)
            {
                Debug.LogError("Could not load Show/Hide Ultrasound Button");
            }
        }

        if (g_UI == null)
        {
            g_UI = GameObject.Find("User Interface");
            if (g_UI == null)
            {
                Debug.LogError("Could not load UI");
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
        Debug.Log("Setting LocalStreamEnabled to " + LocalStreamEnabled);

        Conductor.Instance.LocalStreamEnabled = LocalStreamEnabled;
        Conductor.Instance.RemoteNameForOfferingLocalStream = RemoteNameForOfferingLocalStream;
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
    }

    private void InitMediaTexture(uint id, RawImage videoImage, uint width, uint height, bool isMainTex)
    {
        Plugin.CreateMediaPlayback(id);
        IntPtr nativeTex = IntPtr.Zero;
        Plugin.GetPrimaryTexture(id, width, height, out nativeTex);
        var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)width, (int)height, TextureFormat.BGRA32, false, false, nativeTex);

        if (videoImage != null)
        {
            videoImage.texture = primaryPlaybackTexture;

            if (isMainTex)
            {
                MainTex = new Texture2D(MainTextureWidth, MainTextureHeight, TextureFormat.Alpha8, false);
                YTex = new Texture2D(MainTextureWidth, MainTextureHeight, TextureFormat.Alpha8, false);
                UTex = new Texture2D(MainTextureWidth / 2, MainTextureHeight / 2, TextureFormat.Alpha8, false);
                VTex = new Texture2D(MainTextureWidth / 2, MainTextureHeight / 2, TextureFormat.Alpha8, false);
                if (Hololens == true)
                {
                    videoImage.transform.gameObject.SetActive(false);

                    Stabilization.Instance.MainTex = MainTex;
                    Stabilization.Instance.YTex = YTex;
                    Stabilization.Instance.UTex = UTex;
                    Stabilization.Instance.VTex = VTex;
                }
            }
        }

    //}

        GameObject g_WebRTCPanel = GameObject.Find("WebRTCPanel");
        if (g_WebRTCPanel == null)
        {
            Debug.LogError("Could not load WebRTC Panel");
        }

        g_WebRTCPanel.GetComponent<CanvasGroup>().alpha = 0f;
        g_WebRTCPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;

        g_SavePoseInfo = true;

    }

    private void OnEnable()
    {
        if (LocalStreamEnabled)
        {
            /*Plugin.CreateLocalMediaPlayback();
            IntPtr nativeTex = IntPtr.Zero;
            Plugin.GetLocalPrimaryTexture(LocalTextureWidth, LocalTextureHeight, out nativeTex);
            var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)LocalTextureWidth, (int)LocalTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
            if (LocalVideoImage != null)
            {
                LocalVideoImage.texture = primaryPlaybackTexture;
            }*/
            //InitMediaTexture(SourceIDs[StarMentorName], LocalVideoImage_StarMentor, LocalTextureWidth, LocalTextureHeight, false);
        }

        //if (RemoteVideoImage != null)
        //{
            InitMediaTexture(SourceIDs[StarTraineeName], RemoteVideoImage_StarTrainee, RemoteTextureWidth, RemoteTextureHeight, true);
            InitMediaTexture(SourceIDs[StarTrainee2Name], RemoteVideoImage_StarTrainee2, RemoteTextureWidth, RemoteTextureHeight, false);

        /*Plugin.CreateRemoteMediaPlayback();
        IntPtr nativeTex = IntPtr.Zero;
        Plugin.GetRemotePrimaryTexture(RemoteTextureWidth, RemoteTextureHeight, out nativeTex);
        primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)RemoteTextureWidth, (int)RemoteTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
        RemoteVideoImage.texture = primaryPlaybackTexture;

        MainTex = new Texture2D(MainTextureWidth, MainTextureHeight, TextureFormat.Alpha8, false);
        YTex = new Texture2D(MainTextureWidth, MainTextureHeight, TextureFormat.Alpha8, false);
        UTex = new Texture2D(MainTextureWidth / 2, MainTextureHeight / 2, TextureFormat.Alpha8, false);
        VTex = new Texture2D(MainTextureWidth / 2, MainTextureHeight / 2, TextureFormat.Alpha8, false);
        if (Hololens == true)
        {
            RemoteVideoImage.transform.gameObject.SetActive(false);

            Stabilization.Instance.MainTex = MainTex;          
            Stabilization.Instance.YTex = YTex;                
            Stabilization.Instance.UTex = UTex;               
            Stabilization.Instance.VTex = VTex;
        }*/
        //juan andres drone
        //byte[] imagebytes = primaryPlaybackTexture.GetRawTextureData();

        //}
        
    }

    private void TeardownMediaTexture(uint id, RawImage videoImage)
    {
        if (videoImage != null)
        {
            videoImage.texture = null;
        }
        Plugin.ReleaseMediaPlayback(id);
    }

    private void OnDisable()
    {
        if (LocalStreamEnabled)
        {
            //TeardownMediaTexture(SourceIDs[StarMentorName], LocalVideoImage_StarMentor);
        }

        TeardownMediaTexture(SourceIDs[StarTraineeName], RemoteVideoImage_StarTrainee);
        TeardownMediaTexture(SourceIDs[StarTrainee2Name], RemoteVideoImage_StarTrainee2);

        //g_EventsScript.isPartnerConnected = false;
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
        g_EventsScript.isPartnerConnected = true;
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
        g_EventsScript.isPartnerConnected = false;
    }

    /*
     * This section modifies the position and scale of the Ultrasound texture.
     */

    public void onClickUltrasoundContainerButton()
    {
        if (RemoteVideoImage_StarTrainee2.GetComponent<CanvasGroup>().alpha == 1f)
        {
            float tempWidth = RemoteVideoImage_StarTrainee2.GetComponent<RectTransform>().rect.width;
            float tempHeight = RemoteVideoImage_StarTrainee2.GetComponent<RectTransform>().rect.height;

            if (g_UI.GetComponent<CanvasGroup>().alpha == 0f)
            {
                g_UI.GetComponent<CanvasGroup>().alpha = 1f;
                RemoteVideoImage_StarTrainee2.GetComponent<RectTransform>().sizeDelta = new Vector2(tempWidth / 3f, tempHeight / 3f);
                RemoteVideoImage_StarTrainee2.GetComponent<RectTransform>().position = new Vector3(OrgUltrasoundTextX, OrgUltrasoundTextY, 1f);
            }
            else
            {
                g_UI.GetComponent<CanvasGroup>().alpha = 0f;
                RemoteVideoImage_StarTrainee2.GetComponent<RectTransform>().sizeDelta = new Vector2(tempWidth * 3f, tempHeight * 3f);
                RemoteVideoImage_StarTrainee2.GetComponent<RectTransform>().position = new Vector3(960f, 540f, 1f);
            }   
        }

    }

    System.Random rnd = new System.Random();

    private void Update()
    {
        //LastPeerPoseLabel.text = Plugin.getFloat().toString();
        //Plugin.initChessPoseController();

        //just for testing
        /*int n1 = rnd.Next(1, 99);
        HR.Value = n1.ToString();
        int n2 = rnd.Next(1, 99);
        SpO2.Value = n2.ToString();*/

        if (g_EventsScript.UltrasoundButtonClicked)
        {
            g_UltrasoundButton.GetComponent<RectTransform>().Rotate(new Vector3(0f, 0f, 180f));

            if (RemoteVideoImage_StarTrainee2.GetComponent<CanvasGroup>().alpha == 0f)
            {
                RemoteVideoImage_StarTrainee2.GetComponent<CanvasGroup>().alpha = 1f;
            }
            else
            {
                RemoteVideoImage_StarTrainee2.GetComponent<CanvasGroup>().alpha = 0f;
            }
            g_EventsScript.UltrasoundButtonClicked = false;
        }    

        if (Input.GetKeyDown("space"))
        {
            Texture2D textin = new Texture2D(1344, 756, TextureFormat.RGBA32, false);

            byte[] myBytes = MainTex.GetRawTextureData();
            byte[] newbyte = new byte[myBytes.Length * 4];
            int i;
            for (i = 0; i < myBytes.Length; i++)
            {
                newbyte[i*4] = myBytes[i];
                newbyte[(i*4)+1] = myBytes[i];
                newbyte[(i*4)+2] = myBytes[i];
                newbyte[(i*4)+3] = 0;
            }

            //encode as png and blah.

#if !UNITY_EDITOR
        counter++;
        UnityEngine.WSA.Application.InvokeOnAppThread(async () =>
        {
            StorageFolder rootFolder = ApplicationData.Current.LocalFolder;
            StorageFile sampleFile = await rootFolder.CreateFileAsync("testImage"+counter.ToString()+".jpg",CreationCollisionOption.GenerateUniqueName);
            await FileIO.WriteBytesAsync(sampleFile,newbyte);
        }, false);
#endif
        }


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
                            videoWriter.Dispose();
                            VideoFile.Dispose();
                        }
                        break;
                    case Status.Connected:
                        if (command.type == CommandType.SetConnected)
                        {
                            ConnectButton.GetComponentInChildren<Text>().text = "Disconnect";
                            CallButton.GetComponentInChildren<Text>().text = "Call";
                            String fileToWrite = Path.Combine(Application.persistentDataPath, DateTime.Now.ToString("yyMMdd_HHmmss") + ".raw");
                            VideoFile = File.Create(fileToWrite);
                            videoWriter = new BinaryWriter(VideoFile);
                            Debug.Log(Application.persistentDataPath);
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
    bool ToInit = false;

    // fired whenever we get a video frame from the remote peer.
    // if there is pose data, posXYZ and rotXYZW will have non-zero values.
    private async void Conductor_OnPeerRawFrame(string peerName, uint width, uint height,
            byte[] yPlane, uint yPitch, byte[] vPlane, uint vPitch, byte[] uPlane, uint uPitch,
            float posX, float posY, float posZ, float rotX, float rotY, float rotZ, float rotW)
    {
        if (peerName == StarTraineeName)
        {
            g_plane = yPlane;
            if (Hololens == true)
            {        
                if (g_EventsScript.isUserIconAnnotating == false && g_EventsScript.isUserLineAnnotating == false)
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

                        videoWriter.Write('F');
                        videoWriter.Write(rotX);
                        videoWriter.Write(rotY);
                        videoWriter.Write(rotZ);
                        videoWriter.Write(rotW);
                        videoWriter.Write(posX);
                        videoWriter.Write(posY);
                        videoWriter.Write(posZ);
                        videoWriter.Write(yPlane);
                        videoWriter.Write(uPlane);
                        videoWriter.Write(vPlane);

                        if (ToInit)
                        {
                            videoWriter.Write('I');
                            videoWriter.Write(Stabilization.Instance.Width);
                            videoWriter.Write(Stabilization.Instance.Height);
                            videoWriter.Write(Stabilization.Instance.Fx);
                            videoWriter.Write(Stabilization.Instance.Fy);
                            videoWriter.Write(Stabilization.Instance.Cx);
                            videoWriter.Write(Stabilization.Instance.Cy);
                            Matrix4x4 m = Stabilization.Instance.PlanePose;
                            for (int i = 0; i < 16; ++i)
                            videoWriter.Write(m[i]);

                            Matrix4x4 cam = Matrix4x4.identity;
                            cam.SetTRS(new Vector3(posX, posY, posZ), new Quaternion(rotX, rotY, rotZ, rotW), Vector3.one);
                            cam.m02 = -cam.m02;
                            cam.m12 = -cam.m12;
                            cam.m20 = -cam.m20;
                            cam.m21 = -cam.m21;
                            cam.m23 = -cam.m23;
                            Stabilization.Instance.MainCamera = cam;
                            Graphics.CopyTexture(YTex, MainTex);//calling this to update background
                            mainCamera.transform.SetPositionAndRotation(cam.MultiplyPoint(Vector3.zero), Quaternion.LookRotation(cam.GetColumn(2), cam.GetColumn(1)));

                            JObject message = new JObject();
                            message["posX"] = posX;
                            message["posY"] = posY;
                            message["posZ"] = posZ;
                            message["rotX"] = rotX;
                            message["rotY"] = rotY;
                            message["rotZ"] = rotZ;
                            message["rotW"] = rotW;

                            JObject container = new JObject();
                            container["message"] = message;
#if !UNITY_EDITOR
                            Conductor.Instance.SendMessage("star-trainee", Windows.Data.Json.JsonObject.Parse(container.ToString()));
#endif

                            ToInit = false;
                        }

                        g_SavePoseInfo = true;

                        if (Stabilization.Instance.g_UpdatePose == true)
                        {
                            //calling this to update background
                            Graphics.CopyTexture(YTex, MainTex);
                            Stabilization.Instance.g_UpdatePose = false;
                        }

                    }, false);
                    //GetComponent<Renderer>().material.mainTexture = tex;
                    //Debug.Log(posX + " " + posY + " " + posZ + "\n" + rotX + " " + rotY + " " + rotZ + " " + rotW);

                    //juan andres drone;
                    //var primaryPlaybackTexture2 = Texture2D.CreateExternalTexture((int)RemoteTextureWidth, (int)RemoteTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
                    //Stabilization.Instance.SetTexture(primaryPlaybackTexture2);
                    //byte[] imagebytes = RemoteVideoImage.texture.GetRawTextureData();
                }
                else
                {
                    if (g_SavePoseInfo)
                    {
                        g_EventsScript.setPoseWhileAnnotating(posX, posY, posZ, rotX, rotY, rotZ, rotW);
                        g_SavePoseInfo = false;
                    }
                }
            }
        }
        else
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

    private void Conductor_IncomingRawMessage(string peerName, string rawMessageString)
    {
        if (Hololens == true)
        {
            try
            {
                /*if (Time.time - LastUpdate > 2.0f)
                {
                    g_VSPlacerScript.HRValue = "--";
                    g_VSPlacerScript.HRValue = "--";
                }*/

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
                        ToInit = true;
                        //Stabilization.Instance.MainCamera = planePoints2;
                        //Graphics.CopyTexture(YTex, MainTex);//calling this to update gackground
                        //mainCamera.transform.SetPositionAndRotation(planePoints2.MultiplyPoint(Vector3.zero), Quaternion.LookRotation(planePoints2.GetColumn(2), planePoints2.GetColumn(1)));
                    }, false);

                    //Debug.Log(CameraWidth + " " + CameraHeight + " " + Camerafx + " " + Camerafy + " " + Cameracx + " " + Cameracy);
                }
                else if ((string)message["type"] == "O")
                {
                    /*UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                    {
                        LastUpdate = Time.time;
                    }, true);

                    Debug.Log("I am here");*/

                    g_VSPlacerScript.HRValue = (string)message["HR"];
                    g_VSPlacerScript.SpO2Value = (string)message["SpO2"];
                }
                else if ((string)message["type"] == "T")
                {
                    JArray jsonarray = (JArray)message["matrix"];
                    Vector4 col1 = new Vector4(Convert.ToSingle((string)jsonarray[0]), Convert.ToSingle((string)jsonarray[4]), Convert.ToSingle((string)jsonarray[8]), Convert.ToSingle((string)jsonarray[12]));
                    Vector4 col2 = new Vector4(Convert.ToSingle((string)jsonarray[1]), Convert.ToSingle((string)jsonarray[5]), Convert.ToSingle((string)jsonarray[9]), Convert.ToSingle((string)jsonarray[13]));
                    Vector4 col3 = new Vector4(Convert.ToSingle((string)jsonarray[2]), Convert.ToSingle((string)jsonarray[6]), Convert.ToSingle((string)jsonarray[10]), Convert.ToSingle((string)jsonarray[14]));
                    Vector4 col4 = new Vector4(Convert.ToSingle((string)jsonarray[3]), Convert.ToSingle((string)jsonarray[7]), Convert.ToSingle((string)jsonarray[11]), Convert.ToSingle((string)jsonarray[15]));
                    Matrix4x4 UltrasoundPose = new Matrix4x4(col1, col2, col3, col4);
                    //Debug.Log(UltrasoundPose.ToString());



                    //TestObject.transform.position = UltrasoundPose.MultiplyPoint3x4(TestObject.transform.position);
                    //TestObject.transform.rotation *= Quaternion.LookRotation(UltrasoundPose.GetColumn(2), TransformationMatrix.GetColumn(1));

                    //transform.SetPositionAndRotation((Camera.transform.position + Camera.transform.forward * Distantce) + (Camera.transform.up * cornerOffsetY) + (Camera.transform.right * cornerOffsetX),
                    //Quaternion.LookRotation(Camera.transform.forward, Camera.transform.up));
                    float posX = (960.0f + 2 * (960.0f * Convert.ToSingle((string)jsonarray[3])));
                    float posY = (540.0f + 2 * (540.0f * Convert.ToSingle((string)jsonarray[7])));
                    //Debug.Log("Transformed: " + posX + "," + posY);
                    
                    Vector3 newPos = new Vector3(Convert.ToSingle(Math.Round((decimal)posX, 0)), Convert.ToSingle(Math.Round((decimal)posY, 0)), 1f);
                    Debug.Log("Vectorized: " + newPos.x + "," + newPos.y + "," + newPos.z);
                    UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                    {
                        RemoteVideoImage_StarTrainee2.GetComponent<RectTransform>().position = newPos;
                    }, false);

                    

                    /*float tempWidth = RemoteVideoImage_StarTrainee2.GetComponent<RectTransform>().rect.width;
                    float tempHeight = RemoteVideoImage_StarTrainee2.GetComponent<RectTransform>().rect.height;
                    Debug.Log("Original: " + tempWidth + "," + tempHeight);*/

                    //g_UltrasoundProbe.GetComponent<RectTransform>().position = new Vector3(posX, posY, 1f);
                }
                else
                {
                    JArray jsonarray = (JArray)message["cameraMatrix"];
                    Vector4 col1 = new Vector4(Convert.ToSingle((string)jsonarray[0]), Convert.ToSingle((string)jsonarray[4]), Convert.ToSingle((string)jsonarray[8]), Convert.ToSingle((string)jsonarray[12]));
                    Vector4 col2 = new Vector4(Convert.ToSingle((string)jsonarray[1]), Convert.ToSingle((string)jsonarray[5]), Convert.ToSingle((string)jsonarray[9]), Convert.ToSingle((string)jsonarray[13]));
                    Vector4 col3 = new Vector4(Convert.ToSingle((string)jsonarray[2]), Convert.ToSingle((string)jsonarray[6]), Convert.ToSingle((string)jsonarray[10]), Convert.ToSingle((string)jsonarray[14]));
                    Vector4 col4 = new Vector4(Convert.ToSingle((string)jsonarray[3]), Convert.ToSingle((string)jsonarray[7]), Convert.ToSingle((string)jsonarray[11]), Convert.ToSingle((string)jsonarray[15]));
                    Matrix4x4 pose = new Matrix4x4(col1, col2, col3, col4);
                    UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                    {
                        Stabilization.Instance.Stablize(pose);
                    }, false);

                    //Debug.Log(pose.ToString());
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.GetType().ToString());
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
                Conductor.Instance.SendMessage("star-trainee", Windows.Data.Json.JsonObject.Parse(jsonString));
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
                    //var task = Conductor.Instance.DisconnectFromPeer(peerName);
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

        await Conductor.Instance.DisconnectFromPeer(StarTraineeName);
        await Conductor.Instance.DisconnectFromPeer(StarTrainee2Name);
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
        Conductor.Instance.OnPeerConnectionCreated += (peerName) =>
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
        Conductor.Instance.OnPeerConnectionClosed += (remotePeerName) =>
        {
            var localId = SourceIDs[LocalName];
            var remoteId = SourceIDs[remotePeerName];

            var task = RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.EndingCall)
                    {
                        Plugin.UnloadMediaStreamSource(localId);
                        Plugin.UnloadMediaStreamSource(remoteId);
                        status = Status.Connected;
                        commandQueue.Add(new Command { type = CommandType.SetConnected });
                    }
                    else if (status == Status.InCall)
                    {
                        Plugin.UnloadMediaStreamSource(localId);
                        Plugin.UnloadMediaStreamSource(remoteId);
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

            selectedCapability.MrcEnabled = false;
            Conductor.Instance.VideoCaptureProfile = selectedCapability;
            Conductor.Instance.UpdatePreferredFrameFormat();
            System.Diagnostics.Debug.WriteLine("Selected video device capability - " + selectedCapability.Width + "x" + selectedCapability.Height + "@" + selectedCapability.FrameRate);
        }

#endif
    }

    private void Conductor_OnAddRemoteStream(string remotePeerName)
    {
        var remoteId = SourceIDs[remotePeerName];

#if !UNITY_EDITOR
        var task = RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                    IMediaSource source;
                    if (Conductor.Instance.VideoCodec.Name == "H264")
                        source = Conductor.Instance.CreateRemoteMediaStreamSource(remotePeerName, "H264");
                    else
                        source = Conductor.Instance.CreateRemoteMediaStreamSource(remotePeerName, "I420");
                    Plugin.LoadMediaStreamSource(remoteId, (MediaStreamSource)source);
                }
                else if (status == Status.Connected)
                {
                    IMediaSource source;
                    if (Conductor.Instance.VideoCodec.Name == "H264")
                        source = Conductor.Instance.CreateRemoteMediaStreamSource(remotePeerName, "H264");
                    else
                        source = Conductor.Instance.CreateRemoteMediaStreamSource(remotePeerName, "I420");
                    Plugin.LoadMediaStreamSource(remoteId, (MediaStreamSource)source);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnAddRemoteStream() - wrong status - " + status);
                }
            }
        });
#endif
    }

    private void Conductor_OnRemoveRemoteStream(string peerName)
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
        var localId = SourceIDs[LocalName];

#if !UNITY_EDITOR
        var task = RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                    var source = Conductor.Instance.CreateLocalMediaStreamSource("I420");
                    Plugin.LoadMediaStreamSource(localId, (MediaStreamSource)source);

                    Conductor.Instance.EnableLocalVideoStream();
                    Conductor.Instance.UnmuteMicrophone();
                }
                else if (status == Status.Connected)
                {
                    var source = Conductor.Instance.CreateLocalMediaStreamSource("I420");
                    Plugin.LoadMediaStreamSource(localId, (MediaStreamSource)source);

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
        /*[DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateLocalMediaPlayback")]
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
        internal static extern void GetRemotePrimaryTexture(UInt32 width, UInt32 height, out System.IntPtr playbackTexture);*/

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateMediaPlayback")]
        internal static extern void CreateMediaPlayback(UInt32 id);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "ReleaseMediaPlayback")]
        internal static extern void ReleaseMediaPlayback(UInt32 id);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetPrimaryTexture")]
        internal static extern void GetPrimaryTexture(UInt32 id, UInt32 width, UInt32 height, out System.IntPtr playbackTexture);


        [DllImport("HoloOpenCVHelper", CallingConvention = CallingConvention.StdCall, EntryPoint = "initChessPoseController")]
        internal static extern void initChessPoseController();

#if !UNITY_EDITOR
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadMediaStreamSource")]
        internal static extern void LoadMediaStreamSource(UInt32 id, MediaStreamSource IMediaSourceHandler);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "UnloadMediaStreamSource")]
        internal static extern void UnloadMediaStreamSource(UInt32 id);
#endif

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "Play")]
        internal static extern void Play(UInt32 id);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "Pause")]
        internal static extern void Pause(UInt32 id);
    }
}
