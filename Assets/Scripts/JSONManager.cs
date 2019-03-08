#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
using Windows.Networking.Sockets;
#endif

#if !UNITY_EDITOR
using HoloPoseClient.Signalling;
#endif

using System;
using System.Collections;
using System.Runtime;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WSAUnity;

// Used to store the information to be sent by the JSON in a 
// general way
public struct JSONable
{
    public int id; // Id of the annotation
    public string command; // Performed command
    public List<Vector3> myPoints; // Points representing line
    public string annotation_name; // Name of the icon annotation
    public List<float> annotation_information; // Rot,scale,etc.
    public List<float> pose_information; // HoloLens Stabilization specific information
};

public class JSONManager : MonoBehaviour
{
    // Constant definitions used by the class
    private const string CREATE_ANNOTATION_COMMAND = "CreateAnnotationCommand";
    private const string UPDATE_ANNOTATION_COMMAND = "UpdateAnnotationCommand";
    private const string DELETE_ANNOTATION_COMMAND = "DeleteAnnotationCommand";
    private const string POLYLINE_ANNOTATION = "polyline";
    private const string ICON_ANNOTATION = "tool";
    private const float RESOLUTION_X = 1920.0F;
    private const float RESOLUTION_Y = 1080.0F;

    // List of JSON messages to be created
    private Queue<JSONable> JSONs_to_create;

    // Flag about if a JSON is being created right now
    private bool isJsonBeingCreated;
#if ENABLE_WINMD_SUPPORT
    // Socket to send the JSON info
    private StreamSocket JsonSocket;
#endif
    // Instance of the STAR WebRTC Handler
    public StarWebrtcContext starWebrtcContext { get; set; }

    // Whether or not to send the messages using WebRTC
    public bool JSONThroughWebRTC { get; set; }

    // Temp json files that have been sent
    private Queue<JObject> sentJSONs;

#if !UNITY_EDITOR
        public Conductor.Peer remotePeer;
#endif

    // Use this for initialization
    void Start ()
    {
        JSONs_to_create = new Queue<JSONable>();
        isJsonBeingCreated = false;
        sentJSONs = new Queue<JObject>();
        JSONThroughWebRTC = true;
    }

    // Update is called once per frame
    void Update ()
    {
        if (JSONs_to_create.Count() > 0)
        {
            if (!isJsonBeingCreated)
            {
                isJsonBeingCreated = true;

                JSONable to_create = JSONs_to_create.First();

                if (CREATE_ANNOTATION_COMMAND.Equals(to_create.command, System.StringComparison.Ordinal))
                {
                    if (to_create.annotation_name == null)
                    {
                        constructLineJSONMessage(to_create.id, to_create.command, to_create.myPoints, to_create.pose_information);
                    }
                    else
                    {
                        constructIconAnnotationJSONMessage(to_create.id, to_create.command, to_create.annotation_name, to_create.annotation_information, to_create.pose_information);
                    }
                }
                else if (UPDATE_ANNOTATION_COMMAND.Equals(to_create.command, System.StringComparison.Ordinal))
                {
                    if (to_create.annotation_name == null)
                    {
                        constructLineJSONMessage(to_create.id, to_create.command, to_create.myPoints, to_create.pose_information);
                    }
                    else
                    {
                        constructIconAnnotationJSONMessage(to_create.id, to_create.command, to_create.annotation_name, to_create.annotation_information, to_create.pose_information);
                    }
                }
                else if (DELETE_ANNOTATION_COMMAND.Equals(to_create.command, System.StringComparison.Ordinal))
                {
                    constructDeleteJSONMessage(to_create.id, to_create.command);
                }
                JSONs_to_create.Dequeue();
            }
        }
    }

    /*
         * Method Overview: Constructs a JSON Value object of a line
         * Parameters: Required values of the object to create
         * Return: None
         */
    public void createJSONable(int id, string command, List<Vector3> myPoints, string annotation_name,
    List<float> annotation_information, List<float> pose_information)
    {
        JSONable to_add = new JSONable();

        to_add.id = id;
        to_add.command = command;
        to_add.myPoints = myPoints;
        to_add.annotation_name = annotation_name;

        to_add.annotation_information = annotation_information;
        to_add.pose_information = pose_information;

        JSONs_to_create.Enqueue(to_add);
    }

    public void createReInitCamera()
    {
#if ENABLE_WINMD_SUPPORT
        JObject message = new JObject();

        /*JsonObject message = new JsonObject();
        JsonObject annotation_memory = new JsonObject();
        JsonObject initialAnnotation = new JsonObject();
        JsonArray annotationPoints = new JsonArray();*/

        message["command"] = "REINIT_CAMERA";

        //Writes JSON Value to a file
        sentJSONs.Enqueue(message);
        writeJSONonFile(message);
#endif
    }

    /*
     * Method Overview: Constructs a JSON Object object of a line
     * Parameters: Line Id, message command, points of the line
     * Return: None
     */
    private void constructLineJSONMessage(int id, string command, List<Vector3> myPoints, List<float> pose_information)
    {
#if ENABLE_WINMD_SUPPORT
        JObject message = new JObject();
        JObject annotation_memory = new JObject();
        JObject initialAnnotation = new JObject();
        JObject poseInformationJson =  new JObject();
        JArray annotationPoints = new JArray();

        /*JsonObject message = new JsonObject();
        JsonObject annotation_memory = new JsonObject();
        JsonObject initialAnnotation = new JsonObject();
        JsonArray annotationPoints = new JsonArray();*/

        message["id"] = id;
        message["command"] = command;

        foreach (Vector3 point in myPoints)
        {
            JObject newPointAnnotation = new JObject();
            newPointAnnotation["x"] = System.Math.Round(point.x / RESOLUTION_X, 4);
            newPointAnnotation["y"] = System.Math.Round(point.y / RESOLUTION_Y, 4);
            annotationPoints.Add(newPointAnnotation);
        }
        initialAnnotation.Add("annotationPoints", annotationPoints);

        initialAnnotation["annotationType"] = POLYLINE_ANNOTATION;

        annotation_memory["annotation"] = initialAnnotation;

        message["annotation_memory"] = annotation_memory;

        if (pose_information.Count > 0)
        {
            poseInformationJson["posX"] = pose_information[0];
            poseInformationJson["posY"] = pose_information[1];
            poseInformationJson["posZ"] = pose_information[2];
            poseInformationJson["rotX"] = pose_information[3];
            poseInformationJson["rotY"] = pose_information[4];
            poseInformationJson["rotZ"] = pose_information[5];
            poseInformationJson["rotW"] = pose_information[6];
            
        }
        else
        {
            poseInformationJson["posX"] = 0;
            poseInformationJson["posY"] = 0;
            poseInformationJson["posZ"] = 0;
            poseInformationJson["rotX"] = 0;
            poseInformationJson["rotY"] = 0;
            poseInformationJson["rotZ"] = 0;
            poseInformationJson["rotW"] = 0;
        }

        message["pose_information"] = poseInformationJson;

        //Writes JSON Value to a file
        sentJSONs.Enqueue(message);
        writeJSONonFile(message);
#endif
    }

    /*
     * Method Overview: Constructs a JSON Object object of an annotation
     * Parameters (1): Annotation Id, message command, annotation name
     * Parameters (2): Annotation's important information
     * Return: None
     */
    void constructIconAnnotationJSONMessage(int id, string command, string annotation_name, List<float> annotation_information, List<float> pose_information)
    {
#if ENABLE_WINMD_SUPPORT
        /*
         * The annotation_information structure contains:
         * annotation_information[0] = annotation center X coordinate
         * annotation_information[1] = annotation center Y coordinate
         * annotation_information[2] = annotation rotation value
         * annotation_information[3] = annotation zoom value
         */
        JObject message = new JObject();
        JObject annotation_memory = new JObject();
        JObject poseInformationJson =  new JObject();
        JObject initialAnnotation = new JObject();
        JArray annotationPoints = new JArray();

        message["id"] = id;
        message["command"] = command;

        JObject newPointAnnotation = new JObject();
        newPointAnnotation["x"] = System.Math.Round(annotation_information[0] / RESOLUTION_X, 4);
        newPointAnnotation["y"] = System.Math.Round(annotation_information[1] / RESOLUTION_Y, 4);
        annotationPoints.Add(newPointAnnotation);
        initialAnnotation.Add("annotationPoints", annotationPoints);

        initialAnnotation["rotation"] = -1.0f * System.Math.Round((annotation_information[2] - 45), 4);
        initialAnnotation["scale"] = System.Math.Round(annotation_information[3], 4);
        initialAnnotation["annotationType"] = ICON_ANNOTATION;
        initialAnnotation["toolType"] = annotation_name;
        initialAnnotation["selectableColor"] = 0;

        annotation_memory["annotation"] = initialAnnotation;

        message["annotation_memory"] = annotation_memory;

        if (pose_information.Count > 0)
        {
            poseInformationJson["posX"] = pose_information[0];
            poseInformationJson["posY"] = pose_information[1];
            poseInformationJson["posZ"] = pose_information[2];
            poseInformationJson["rotX"] = pose_information[3];
            poseInformationJson["rotY"] = pose_information[4];
            poseInformationJson["rotZ"] = pose_information[5];
            poseInformationJson["rotW"] = pose_information[6];
            
        }
        else
        {
            poseInformationJson["posX"] = 0;
            poseInformationJson["posY"] = 0;
            poseInformationJson["posZ"] = 0;
            poseInformationJson["rotX"] = 0;
            poseInformationJson["rotY"] = 0;
            poseInformationJson["rotZ"] = 0;
            poseInformationJson["rotW"] = 0;
        }

        message["pose_information"] = poseInformationJson;

        //Writes JSON Value to a file
        sentJSONs.Enqueue(message);
        writeJSONonFile(message);
#endif
    }

    /*
     * Method Overview: Creates a JSON Message of a delete command
     * Parameters: Command type, ID of the erased annotations
     * Return: None
     */
    void constructDeleteJSONMessage(int id, string command)
    {
#if ENABLE_WINMD_SUPPORT
        JObject message = new JObject();

        message["id"] = id;
        message["command"] = command;

        //Writes JSON Value to a file
        sentJSONs.Enqueue(message);
        writeJSONonFile(message);
#endif
    }
#if ENABLE_WINMD_SUPPORT
    private async void writeJSONonFile(JObject to_text)
    {
        // Create sample file; replace if exists.
        StorageFolder rootFolder = ApplicationData.Current.LocalFolder;
        StorageFile sampleFile = await rootFolder.CreateFileAsync("JsonToSend.json", CreationCollisionOption.ReplaceExisting);
        JObject messageContainer = new JObject();
        messageContainer["message"] = to_text;
        string string_to_send = messageContainer.ToString();//JsonConvert.SerializeObject(messageContainer, Formatting.None);
        await FileIO.WriteTextAsync(sampleFile, string_to_send);  
        
        if (JSONThroughWebRTC)
        {
            //Starts the process of sending the JSON value over WebRTC
            //starWebrtcContext.sendMessageToAnnotationReceiver(to_text);
            Conductor.Instance.SendMessage("star-trainee", Windows.Data.Json.JsonObject.Parse(string_to_send));

            //Let the CommanderCenter know that the message was sent
            isJsonBeingCreated = false;
        }
        else
        {
            //Starts the process of sending the JSON value over TCP/IP
            JSONtoNetwork(string_to_send);
        }
}
#endif
    /*
     * Method Overview: Routines to send JSON strings over the network
     * Parameters: String containing the JSON Value
     * Return: None
     */
    private void JSONtoNetwork(string string_to_send)
    {
#if ENABLE_WINMD_SUPPORT
        //Send the line back to the remote client.
        if (JsonSocket != null)
        {
            Stream outStream = JsonSocket.OutputStream.AsStreamForWrite();
            StreamWriter writer = new StreamWriter(outStream, System.Text.Encoding.ASCII, 100000);
            writer.WriteLineAsync(string_to_send);
            writer.FlushAsync();
        }
#endif
        //Let the CommanderCenter know that the message was sent
        isJsonBeingCreated = false;
    }

    /*
     * Method Overview: Assign the socket that is used to communicate through the network
     * Parameters: Socket to use
     * Return: None
     */
#if ENABLE_WINMD_SUPPORT
    public void receiveSocket(StreamSocket socket)
    {
        JsonSocket = socket;
    }
#endif
    /*
     * Method Overview: Method to convert a string into a streamto send over network
     * Parameters: String to convert
     * Return: None
     */
    public static Stream GenerateStreamFromString(string s)
    {
        MemoryStream stream = new MemoryStream();
        StreamWriter writer = new StreamWriter(stream);
        writer.Write(s);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    /*
     * Method Overview: Sends again all the JSON objects stores so far
     * Parameters: None
     * Return: None
     */
    public void resendJSON()
    {
        if (JSONThroughWebRTC)
        {
            while (sentJSONs.Count > 0)
            {
                //Starts the process of sending the JSON value over WebRTC
                //starWebrtcContext.sendMessageToAnnotationReceiver(sentJSONs.Dequeue());
            }
        }
    }

    /*
     * Method Overview: Cleans the accumulated Json queue
     * Parameters: None
     * Return: None
     */
    public void cleanJsonQueue()
    {
        sentJSONs.Clear();
    }
}
