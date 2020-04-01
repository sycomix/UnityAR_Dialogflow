using System.Collections.Generic;
using UnityEngine;
using JsonData;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Linq;
using System.Collections;

[RequireComponent(typeof(AudioSource))]

// public class Tuple<T,U>
//     {
//         public T Item1 { get; private set; }
//         public U Item2 { get; private set; }

//         public Tuple(T item1, U item2)
//         {
//         Item1 = item1;
//         Item2 = item2;
//         }
//     }

//     public static class Tuple
//     {
//         public static Tuple<T, U> Create<T, U>(T item1, U item2)
//         {
//         return new Tuple<T, U>(item1, item2);
//         }
//     }
public class MainScript : MonoBehaviour
{

    // public List<Tuple<GameObject,int>> l_intent = new  List<Tuple<GameObject,int>>();
    // public int counter = 0;
    // public int total_intent;
    
    //A boolean that flags whether there's a connected microphone
    private bool micConnected = false;

    //The maximum and minimum available recording frequencies
    private int minFreq;
    private int maxFreq;

    //A handle to the attached AudioSource
    private AudioSource goAudioSource;

    //Public variable for saving recorded sound clip
    public AudioClip recordedClip;
    private float[] samples;
    private byte[] bytes;
    //dialogflow
    private AudioSource audioSource; //dialogflow-recording-audiosource
    private readonly object thisLock = new object(); //audiosource-lock
    private volatile bool recordingActive; //audiosource-flag
    public UnityEngine.UI.Text buttonText;
    public GameObject introPanel;
    public GameObject comparisionPanel;
    public GameObject reviewsPanel;
    public GameObject similarPanel;
    public GameObject recipiesPanel;
    public GameObject quantityPanel;
    
    // Start is called before the first frame update
    void Start()
    {
        // hide_panels();
        // collect_intents();
        //Check if there is at least one microphone connected
        if (Microphone.devices.Length <= 0)
        {
            //Throw a warning message at the console if there isn't
            Debug.LogWarning("Microphone not connected!");
        }
        else //At least one microphone is present
        {
            //Set 'micConnected' to true
            micConnected = true;

            //Get the default microphone recording capabilities
            Microphone.GetDeviceCaps(null, out minFreq, out maxFreq);

            //According to the documentation, if minFreq and maxFreq are zero, the microphone supports any frequency...
            if (minFreq == 0 && maxFreq == 0)
            {
                //...meaning 44100 Hz can be used as the recording sampling rate
                maxFreq = 44100;
            }

            //Get the attached AudioSource component
            goAudioSource = this.GetComponent<AudioSource>();
        }
    }

    void recording_button()
    {
        if (micConnected)
        {
            //If the audio from any microphone isn't being captured
            if (!Microphone.IsRecording(null))
            {
                buttonText.text = "Stop";
                StartListening(goAudioSource);
                //this.transform.GetChild(0).GetComponent<Text>()="Record!";
            }
            else//Recording is in progress
            {
                buttonText.text = "Record";
                //this.transform.GetChild(0).GetComponent<Text>()="Stop";
                StopListening();
            }
        }
        else // No microphone
        {
            //Print a red "Microphone not connected!" message at the center of the screen
            GUI.contentColor = Color.red;
            // GUI.Label(new Rect(Screen.height / 4 - 25, Screen.width / 4 - 100, 500, 150), "Microphone not connected!");
        }
    }
    
    public void StartListening(AudioSource audioSource)
    {
        lock (thisLock)
        {
            if (!recordingActive)
            {
                this.audioSource = audioSource;
                StartRecording();
            }
            else
            {
                Debug.LogWarning("Can't start new recording session while another recording session active");
            }
        }
    }

    private void StartRecording()
    {
        audioSource.clip = Microphone.Start(null, true, 3, 16000);
        recordingActive = true;
    }

    public void StopListening()
    {
        if (recordingActive)
        {

            //float[] samples = null;

            lock (thisLock)
            {
                if (recordingActive)
                {
                    StopRecording();
                    //samples = new float[audioSource.clip.samples];

                    //audioSource.clip.GetData(samples, 0);
                    bytes = WavUtility.FromAudioClip(audioSource.clip);
                    // audioSource.Play();
                    Debug.Log("This is the audiosource clip length: "+ bytes.Length);
                    // audioSource = null;
                }
            }

            //new Thread(StartVoiceRequest).Start(samples);
            StartCoroutine(StartVoiceRequest("https://dialogflow.googleapis.com/v2/projects/coffee-wnsicn/agent/sessions/1234:detectIntent",
                "",
                bytes));
        }
    }

    private void StopRecording()
    {
        Microphone.End(null);
        recordingActive = false;
    }

    IEnumerator StartVoiceRequest(string url, string AccessToken, object parameter)
    {
        byte[] samples = (byte[])parameter;
        //TODO: convert float[] samples into bytes[]
        //byte[] sampleByte = new byte[samples.Length * 4];
        //Buffer.BlockCopy(samples, 0, sampleByte, 0, sampleByte.Length);

        string sampleString = System.Convert.ToBase64String(samples);
        if (samples != null)
        {
            UnityWebRequest postRequest = new UnityWebRequest(url, "POST");
            RequestBody requestBody = new RequestBody();
            requestBody.queryInput = new QueryInput();
            requestBody.queryInput.audioConfig = new InputAudioConfig();
            requestBody.queryInput.audioConfig.audioEncoding = AudioEncoding.AUDIO_ENCODING_UNSPECIFIED;
            //TODO: check if that the sample rate hertz
            requestBody.queryInput.audioConfig.sampleRateHertz = 16000;
            requestBody.queryInput.audioConfig.languageCode = "en";
            requestBody.inputAudio = sampleString;

            string jsonRequestBody = JsonUtility.ToJson(requestBody, true);
            Debug.Log(jsonRequestBody);

            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
            postRequest.SetRequestHeader("Authorization", "Bearer " + AccessToken);
            postRequest.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            postRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            postRequest.SetRequestHeader("Content-Type", "application/json");

            yield return postRequest.SendWebRequest();

            if (postRequest.isNetworkError || postRequest.isHttpError)
            {
                Debug.Log(postRequest.responseCode);
                Debug.Log(postRequest.error);
            }
            else
            {

                Debug.Log("Response: " + postRequest.downloadHandler.text);

                // Or retrieve results as binary data
                byte[] resultbyte = postRequest.downloadHandler.data;
                string result = System.Text.Encoding.UTF8.GetString(resultbyte);
                Debug.Log("Result String: " + result);
                ResponseBody content = (ResponseBody)JsonUtility.FromJson<ResponseBody>(postRequest.downloadHandler.text);
                Debug.Log(content.queryResult.fulfillmentText);
                Debug.Log(content.queryResult.intent.displayName);
                
                string responseintent = content.queryResult.intent.displayName;
                Debug.Log("Return: " + responseintent);

                showPanel(responseintent);
                // Debug.Log(content.outputAudio);
                // Debug.Log(audioSource);
                audioSource.clip = WavUtility.ToAudioClip(System.Convert.FromBase64String(content.outputAudio), 0);
                audioSource.Play();
            }
        }else{
            Debug.LogError("The audio file is null");
        }
    }


    public void showPanel(string responseintent)
    {
        switch(responseintent)
        {
            case "q1":
            {
                introPanel.SetActive(true);
                comparisionPanel.SetActive(false);
                reviewsPanel.SetActive(false);
                similarPanel.SetActive(false);
                recipiesPanel.SetActive(false);
                quantityPanel.SetActive(false);
                break;
            }
            case "Q2":
            {
                introPanel.SetActive(false);
                comparisionPanel.SetActive(true);
                reviewsPanel.SetActive(false);
                similarPanel.SetActive(false);
                recipiesPanel.SetActive(false);
                quantityPanel.SetActive(false);
                break;
            }
            case "Q3":
            {
                introPanel.SetActive(false);
                comparisionPanel.SetActive(false);
                reviewsPanel.SetActive(true);
                similarPanel.SetActive(false);
                recipiesPanel.SetActive(false);
                quantityPanel.SetActive(false);
                break;
            }
            case "Q4":
            {
                introPanel.SetActive(false);
                comparisionPanel.SetActive(false);
                reviewsPanel.SetActive(false);
                similarPanel.SetActive(true);
                recipiesPanel.SetActive(false);
                quantityPanel.SetActive(false);
                break;
            }
            case "Q5":
            {
                introPanel.SetActive(false);
                comparisionPanel.SetActive(false);
                reviewsPanel.SetActive(false);
                similarPanel.SetActive(false);
                recipiesPanel.SetActive(false);
                quantityPanel.SetActive(true);
                break;
            }
            case "Q6":
            {
                introPanel.SetActive(false);
                comparisionPanel.SetActive(false);
                reviewsPanel.SetActive(false);
                similarPanel.SetActive(false);
                recipiesPanel.SetActive(true);
                quantityPanel.SetActive(false);
                break;
            }
            default:
            {
                break;
            }
        }
    }

    // public void call_intents(string responseintent){
    //     //qnbuttonhandler q = gameObject.AddComponent<qnbuttonhandler>();
    //     //string question=q.CaptureQuestion();
    //     int num;
        
    //     if (responseintent.Equals("q1"))
    //     {	num=1;
    //         //q.resetfield();
    //         show_card(num);
            
    //     } 
    //     if (responseintent.Equals("Q2"))
    //     {	num=2;
    //         //q.resetfield();
    //         show_card(num);
    //     } 
    //     if (responseintent.Equals("Q3"))
    //     {	num=3;
    //         //q.resetfield();
    //         show_card(num);
    //     } 
    //     if (responseintent.Equals("Q4"))
    //     {	num=4;
    //         //q.resetfield();
    //         show_card(num);
    //     } 
    //     if (responseintent.Equals("Q5"))
    //     {	num=5;
    //         //q.resetfield();
    //         show_card(num);
    //     } 
        
    // }



    public void Update()
    {   
        // Exit the app when the 'back' button is pressed.
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

}