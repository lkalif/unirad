using UnityEngine;
using System.Collections;

public class Main : MonoBehaviour
{
    void Awake()
    {
        var instance = new GameObject("Instance");
        instance.AddComponent<Instance>();
    }
}
