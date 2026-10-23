using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformManager : MonoBehaviour
{


    private static PlatformManager instance;

    public static PlatformManager Instance
    {
        get
        {

            if (instance == null)
                instance = new PlatformManager();
            return instance;

        }


    }


    public List<Platform_Basic> platformList;

    public void AddPlatform(Platform_Basic platform)
    {

        platformList.Add(platform);


    }
    public void RemovePlatform(Platform_Basic platform)
    {
        if(platformList.Contains(platform))
        {
            platformList.Remove(platform);
        }
    }

    public void ClearData()
    {

        platformList.Clear();

    }

    
}
