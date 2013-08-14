using UnityEngine;
using System;
using System.Collections;
using OM = OpenMetaverse;
using System.IO;
using System.Drawing;

public class Region : MonoBehaviour
{
    [NonSerialized]
    public GameObject Water;
    [NonSerialized]
    public OM.Simulator Sim;

    Instance Instance;
    [NonSerialized]
    public RegionTerrain Terrain;
    GameObject Sun;
    float waterHeight = 20f;
    void Start()
    {
        Instance = Instance.Singleton();

        var WaterPrefab = (GameObject)Resources.Load("Daylight Water");
        if (WaterPrefab != null)
        {
            Water = (GameObject)Instantiate(WaterPrefab);
            Water.transform.position = new Vector3(128f, waterHeight, 128f);
            Water.transform.localScale = new Vector3(512f, 1f, 512f);
            Water.transform.parent = transform;
        }

        Terrain = new RegionTerrain(Sim, transform);

        Sun = new GameObject("Sun");
        Sun.transform.position = new Vector3(256f, 5000f, 256f);
        Sun.transform.LookAt(transform);
        var light = Sun.AddComponent<Light>();
        light.type = LightType.Spot;
        light.intensity = 5.8f;
        light.range = 8192f;


    }

    void Update()
    {
        Terrain.Update();
        if (Sim.WaterHeight != waterHeight)
        {
            waterHeight = Sim.WaterHeight;
            Water.transform.position = new Vector3(128f, waterHeight, 128f);
        }
    }
}