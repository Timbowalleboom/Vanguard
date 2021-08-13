using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetIp : MonoBehaviour
{
    public void setIp(string ip)
    {
        if (ip != "")
            ParameterPass.ip = ip;
        else ParameterPass.ip = "projectvanguard.uk.to";
    }
}
