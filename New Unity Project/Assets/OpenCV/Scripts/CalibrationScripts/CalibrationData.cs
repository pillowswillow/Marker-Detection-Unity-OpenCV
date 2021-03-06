﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using File = System.IO.File;

[CreateAssetMenu(menuName = "CalibrationData")]
public class CalibrationData : ScriptableObject
{
    public double m00;
    public double m01;
    public double m02;
    public double m10;
    public double m11;
    public double m12;
    public double m20;
    public double m21;
    public double m22;

    public double dis0;
    public double dis1;
    public double dis2;
    public double dis3;
    public double dis4;

    public bool hasCalibratedBefore = false;

    public double projectionError = -1;
    
    public void RegisterMatrix(double[,] m)
    {
        m00 = m[0,0];
        m01 = m[0,1];
        m02 = m[0,2];
        m10 = m[1,0];
        m11 = m[1,1];
        m12 = m[1,2];
        m20 = m[2,0];
        m21 = m[2,1];
        m22 = m[2,2];
    }

    public void RegisterDistortionCoefficients(double[] d)
    {
        dis0 = d[0];
        dis1 = d[1];
        dis2 = d[2];
        dis3 = d[3];
        dis4 = d[4];
    }

    public double[,] GetCameraMatrix(ref double[,] m)
    {
        m[0,0] = m00;
        m[0,1] = m01;
        m[0,2] = m02;
        m[1,0] = m10;
        m[1,1] = m11;
        m[1,2] = m12;
        m[2,0] = m20;
        m[2,1] = m21;
        m[2,2] = m22;
        return m;
    }

     public double[,] GetCameraMatrix()
    {
        double[,] m = new double[3,3];
        m[0,0] = m00;
        m[0,1] = m01;
        m[0,2] = m02;
        m[1,0] = m10;
        m[1,1] = m11;
        m[1,2] = m12;
        m[2,0] = m20;
        m[2,1] = m21;
        m[2,2] = m22;
        return m;
    }

     public double[] GetDistortionCoefficients(ref double[] d)
     {
         d[0] = dis0;
         d[1] = dis1;
         d[2] = dis2;
         d[3] = dis3;
         d[4] = dis4;
         
         return d;
     }
     
     public double[] GetDistortionCoefficients()
     {
         double[] d = new double[5];
         
         d[0] = dis0;
         d[1] = dis1;
         d[2] = dis2;
         d[3] = dis3;
         d[4] = dis4;
         
         return d;
     }


     public void SaveData()
     {
         hasCalibratedBefore = true;
         string jsonString = JsonUtility.ToJson(this);
         Debug.Log("[SAVE] json string: " + jsonString);
         File.WriteAllText(Application.persistentDataPath + "/" + name + ".txt",jsonString);
         Debug.Log("[SAVE] SAVED: " + name);
     }

     public void LoadData()
     {
         if (File.Exists(Application.persistentDataPath + "/" + name + ".txt"))
         {
             string jsonString = File.ReadAllText(Application.persistentDataPath + "/" + name + ".txt");
             Debug.Log("[LOAD] json string: " + jsonString);
             JsonUtility.FromJsonOverwrite(jsonString,this);
         }
         else
         {
             Debug.Log("Can NOT load a calibration file that does not exist!");
         }
     }
}
