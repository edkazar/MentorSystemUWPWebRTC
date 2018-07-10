#if ENABLE_WINMD_SUPPORT
using Windows.UI.Core;
using Windows.Graphics.Imaging;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using WSAUnity;
#endif
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

public class MediaPlayer : MonoBehaviour
{
#if ENABLE_WINMD_SUPPORT
    ///////////////// Variables for the socket communication
    private JSONManager g_JsonManager;

    private StreamSocketListener tcpVideoListener;
    private string videoPort;
    private StreamSocketListener tcpJsonListener;
    private string jsonPort;
    StorageFolder rootFolder;
    private bool connectionHappened;

    private StreamSocketListener g_Sender;
    private StreamSocketListenerConnectionReceivedEventArgs g_Args;
    private Texture2D g_TempTex;
    private byte[] g_NewBytes;
    private bool isTextureLoaded;

    private byte[] g_StreamInfo;
    private uint tabletResX;
    private uint tabletResY;//400 for tablet //480 for drone
    private uint orgNumChan;
    private uint targetNumChan;
    private uint orgRes;
    private uint targetRes;
    ///////////////////////
#else
    private WebCamTexture g_WebcamTexture;
#endif
    private GameObject g_BackgroundImage;

    // Use this for initialization
    void Start ()
    {
        if (g_BackgroundImage == null)
        {
            g_BackgroundImage = GameObject.Find("BackgroundImage");
            if (g_BackgroundImage == null)
            {
                Debug.LogError("Could not load background Image");
            }
        }

        GameObject JsonContainer = GameObject.Find("EventSystem");
        if (JsonContainer == null)
        {
            Debug.LogError("Could not load Event System");
        }

#if ENABLE_WINMD_SUPPORT
        g_JsonManager = JsonContainer.GetComponent<JSONManager>();

        onInitiated();
#else
        g_WebcamTexture = new WebCamTexture();
        g_BackgroundImage.GetComponent<Renderer>().material.mainTexture = g_WebcamTexture;
        g_WebcamTexture.Play();
#endif
    }


    // Update is called once per frame
    void Update()
    {
#if ENABLE_WINMD_SUPPORT
        if (connectionHappened)
        {
            if (isTextureLoaded)
            {
                
                g_TempTex.LoadRawTextureData(g_NewBytes);
                g_TempTex.Apply();
                g_BackgroundImage.GetComponent<Renderer>().material.mainTexture = g_TempTex;
                isTextureLoaded = false;
            }
            else
            {
                textureThroughStream();
            }            
        }
#endif
    }

#if ENABLE_WINMD_SUPPORT
    private async void onInitiated()
    {
        videoPort = "8900";
        jsonPort = "8988";
        rootFolder = ApplicationData.Current.LocalFolder;
        connectionHappened = false;
        isTextureLoaded = false;
        
        tabletResX = 640;
        tabletResY = 400;//400 for tablet //480 for drone
        orgNumChan = 3;
        targetNumChan = 4;
        orgRes = tabletResX * tabletResY * orgNumChan;
        targetRes = tabletResX * tabletResY * targetNumChan;
        g_StreamInfo = new byte[orgRes];
        g_NewBytes = new byte[targetRes];
        g_TempTex = new Texture2D((int)tabletResX, (int)tabletResY, TextureFormat.RGBA32, false);

        tcpVideoListener = new StreamSocketListener();
        tcpVideoListener.ConnectionReceived += onConnected;
        await tcpVideoListener.BindEndpointAsync(null, videoPort);

        tcpJsonListener = new StreamSocketListener();
        tcpJsonListener.ConnectionReceived += jsonOnConnected;
        await tcpJsonListener.BindEndpointAsync(null, jsonPort);       
    }

    private async void onConnected(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
    {
        connectionHappened = true;
        g_Sender = sender;
        g_Args = args;
    }

    private void jsonOnConnected(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
    {
        g_JsonManager.receiveSocket(args.Socket);
    }

    private async void textureThroughStream()
    {
            DataReader reader = new DataReader(g_Args.Socket.InputStream);

            try
            {
                // Read first 4 bytes (length of the subsequent string).
                uint sizeFieldCount = await reader.LoadAsync(sizeof(byte) * orgRes);
                if (sizeFieldCount != sizeof(byte) * orgRes)
                {
                    // The underlying socket was closed before we were able to read the whole data.
                    return;
                }
                // Read the string.
                reader.ReadBytes(g_StreamInfo);

                

                for (int Ycntr = ((int)tabletResY)-1; Ycntr >= 0; Ycntr--)
                {
                    int unflipped = (((int)tabletResY)-1) - Ycntr;
                    for (int Xcntr = 0; Xcntr < tabletResX; Xcntr++) 
                    {
                        g_NewBytes[((Ycntr * tabletResX + Xcntr) * targetNumChan) + 0] = g_StreamInfo[((unflipped * tabletResX + Xcntr) * orgNumChan) + 2];
                        g_NewBytes[((Ycntr * tabletResX + Xcntr) * targetNumChan) + 1] = g_StreamInfo[((unflipped * tabletResX + Xcntr) * orgNumChan) + 1];
                        g_NewBytes[((Ycntr * tabletResX + Xcntr) * targetNumChan) + 2] = g_StreamInfo[((unflipped * tabletResX + Xcntr) * orgNumChan) + 0];
                        g_NewBytes[((Ycntr * tabletResX + Xcntr) * targetNumChan) + 3] = 0;                   
                    }
                }                
                isTextureLoaded = true;     
            }
            catch (Exception exception)
            {
                // If this is an unknown status it means that the error is fatal and retry will likely fail.
            }
    }
#endif
}