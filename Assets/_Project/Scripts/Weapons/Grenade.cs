using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Note: when making a grendae, you must also remember to call GiveGrenadeSpeed
public class Grenade : MonoBehaviour
{
    public float nadeLife = 5;
    public GameObject particleSystem;
    public Rigidbody rb;
    public float nadeVelocity = 20;

    // Start is called before the first frame update
    void Start()
    {
        Destroy(gameObject,nadeLife);
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnDestroy()
    {
        Instantiate(particleSystem,gameObject.transform.position,new Quaternion(0,1,0,0));
    }

    public void GiveGrenadeSpeed(Vector3 playerVelocity)
    {
        rb.velocity = playerVelocity;
        rb.AddRelativeForce(0, 0, nadeVelocity, ForceMode.VelocityChange);
    }
}
