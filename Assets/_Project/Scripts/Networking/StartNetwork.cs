using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class StartNetwork : MonoBehaviour
{
    //it connects to the specified ip when the scene is loaded
    void Start()
    {
        NetworkManager nm = GetComponent<NetworkManager>();
        nm.networkAddress = ParameterPass.ip;
        nm.StartClient();
        print(ParameterPass.ip);
    }
}
