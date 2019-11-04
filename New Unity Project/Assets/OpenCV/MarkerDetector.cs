﻿using OpenCvSharp;
using OpenCvSharp.Aruco;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using ThreadPriority = System.Threading.ThreadPriority;

public class MarkerDetector : MonoBehaviour
{
    public WebCamera webCamera;

    //Events thrown each frame holding all ids found/lost
    public static event Action<int[]> OnMarkersDetected;
    public static event Action<int[]> OnMarkersLost;

    public Camera cam;
    public PredefinedDictionaryName markerDictionaryType;
    [SerializeField] private bool doCornerRefinement = true;
    public bool debugMode = false;

    public CalibrationData calibrationData;
    private DetectorParameters detectorParameters;
    private Dictionary dictionary;
    private Mat grayedImg = new Mat();
    private Mat img = new Mat();
    private Mat imgBuffer;

    private Dictionary<int, MarkerBehaviour> allDetectedMarkers = new Dictionary<int, MarkerBehaviour>();
    private List<int> lostIds = new List<int>();

    private Point2f[][] corners;
    private int[] ids;

    private Point2f[][] cornersCache;
    private int[] idsCache;

    private Point2f[][] rejectedImgPoints;

    private Thread detectMarkersThread;
    private Thread grayscaleImageThread;
    
    bool updateThread = false;

    private int threadCounter = 0;
    private bool outputImage = false;

    private Semaphore threadSemaphore = new Semaphore(1, 1);

    protected void Start()
    {
        Init();

        DetectMarkerAsync();
        //grayscaleImageThread.Priority = ThreadPriority.Highest;
        //detectMarkersThread.Priority = ThreadPriority.Highest;

    }

    private void OnEnable()
    {
        webCamera.OnProcessTexture += ProcessTexture;
    }

    private void OnDisable()
    {
        webCamera.OnProcessTexture -= ProcessTexture;
        updateThread = false;
        
        grayscaleImageThread.Abort();
        detectMarkersThread.Abort();

        if (!img.IsDisposed) img.Release();
        if (!imgBuffer.IsDisposed) imgBuffer.Release();
        if (!grayedImg.IsDisposed) grayedImg.Release();
    }

    void Init()
    {
        detectorParameters = DetectorParameters.Create();

        detectorParameters.DoCornerRefinement = doCornerRefinement;
        //detectorParameters.CornerRefinementMinAccuracy = 0.01f;

        dictionary = CvAruco.GetPredefinedDictionary(markerDictionaryType);
    }

    // Our sketch generation function
    private bool ProcessTexture(WebCamTexture input, ref Texture2D output,
        ARucoUnityHelper.TextureConversionParams textureParameters)
    {
        imgBuffer = ARucoUnityHelper.TextureToMat(input, textureParameters);
        //Debug.Log("New image Assigned");
        
        if (threadCounter == 0)
        {
            imgBuffer.CopyTo(img);
            Interlocked.Increment(ref threadCounter);
        }

        updateThread = true;
      
        if(outputImage)
        {
            output = ARucoUnityHelper.MatToTexture(img, output);
            //Debug.Log("Marker image Rendered");
            outputImage = false;
        }
        else
        {
            output = ARucoUnityHelper.MatToTexture(imgBuffer, output);
            //Debug.Log("Camera image Rendered");
        }
        
        imgBuffer.Release();
        return true;
    }


    private void DetectMarkerAsync()
    {
        if (detectMarkersThread == null || !detectMarkersThread.IsAlive)
        {
            detectMarkersThread = new Thread(DetectMarkers);
            detectMarkersThread.Start();
        }
        
        if (grayscaleImageThread == null || !grayscaleImageThread.IsAlive)
        {
            grayscaleImageThread = new Thread(GrayScaleImage);
            grayscaleImageThread.Start();
        }
    }

    private void CheckIfLostMarkers()
    {
        if (ids.Length == 0)
        {
            foreach (MarkerBehaviour lostMarker in allDetectedMarkers.Values)
            {
                lostMarker.OnMarkerLost.Invoke();
            }

            allDetectedMarkers.Clear();
        }
        else
        {
            foreach (int id in allDetectedMarkers.Keys)
            {
                int idNotFound = -1;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (id != ids[i])
                    {
                        idNotFound = id;
                    }
                    else
                    {
                        idNotFound = -1;
                        break;
                    }
                }

                if (idNotFound >= 0)
                {
                    allDetectedMarkers[idNotFound].OnMarkerLost.Invoke();
                    lostIds.Add(idNotFound);
                }
            }
        }

        OnMarkersLost?.Invoke(lostIds.ToArray());

        foreach (int i in lostIds)
        {
            allDetectedMarkers.Remove(i);
        }

        lostIds.Clear();
    }

    private void CheckIfDetectedMarkers()
    {
        if (ids.Length > 0 && OnMarkersDetected != null)
        {
            OnMarkersDetected.Invoke(ids);
        }

        for (int i = 0; i < ids.Length; i++)
        {
            //Cv2.CornerSubPix(grayedImg, corners[i], new Size(5, 5), new Size(-1, -1), TermCriteria.Both(30, 0.1));

            if (!MarkerManager.IsMarkerRegistered(ids[i]))
            {
                continue;
            }

            MarkerBehaviour m = MarkerManager.GetMarker(ids[i]);

            if (!allDetectedMarkers.ContainsKey(ids[i]))
            {
                m.OnMarkerDetected.Invoke();
                allDetectedMarkers.Add(m.GetMarkerID(), m);
            }

            // m.UpdateMarker(img.Cols, img.Rows, corners[i], rejectedImgPoints[i]);
            m.UpdateMarker(img.Rows, img.Cols, corners[i], calibrationData.GetCameraMatrix(),
                calibrationData.GetDistortionCoefficients(), grayedImg);
        }
    }

    private void DetectMarkers()
    {
        //Debug.Log(elapsed);
        while (true)
        {
            if (!updateThread)
            {
                //we skip updating the thread when not needed and also avoids memory exceptions when we disable the 
                //mono behaviour or we haven't updated the main thread yet!

                // Debug.Log("grayed img was disposed");
                continue;
            }

            if (threadCounter == 2)
            {
                CvAruco.DetectMarkers(grayedImg, dictionary, out corners, out ids, detectorParameters,
                    out rejectedImgPoints);
                
                outputImage = true;
                
                CheckIfLostMarkers();
                CheckIfDetectedMarkers();
                
                Interlocked.Exchange(ref threadCounter, 0);
            }
        }
    }

    private void GrayScaleImage()
    {
        while (true)
        {
            if (!updateThread) continue;

            if (threadCounter == 1)
            {
                Cv2.CvtColor(img, grayedImg, ColorConversionCodes.BGR2GRAY);
                Interlocked.Increment(ref threadCounter);
            }
        }
    }
    
    public void ToggleDebugMode()
    {
        debugMode = !debugMode;
    }
}