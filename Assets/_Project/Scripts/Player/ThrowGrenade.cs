using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrowGrenade : MonoBehaviour
{
    public Grenade grenade;
    public Rigidbody rb;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.G)) 
        {
            Grenade newGrenade = Instantiate(grenade, gameObject.transform.position + gameObject.transform.forward * 2, gameObject.transform.rotation);
            newGrenade.GiveGrenadeSpeed(rb.velocity);
        }
    }
}
