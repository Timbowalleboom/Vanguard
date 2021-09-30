using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
namespace Vanguard
{
    //Equivalent to Projectile
    public class Grenade : NetworkBehaviour
    {
        public float nadeLife = 5;
        public GameObject particleSystem; //Make particleSystems automatically destroy themselves
        public Rigidbody rb;
        public float nadeVelocity = 20;
        public float range, damage;


        // Start is called before the first frame update
        void Start()
        {
            rb.AddRelativeForce(Vector3.forward * nadeVelocity, ForceMode.VelocityChange);
        }

        // Update is called once per frame
        void Update()
        {

        }
        public override void OnStartServer()
        {
            Invoke(nameof(DestroySelf), nadeLife);
        }
        [Server]
        void DestroySelf()
        {
            NetworkServer.Destroy(gameObject);
            GameObject newGrenadeExplosion = Instantiate(particleSystem, gameObject.transform.position, new Quaternion(0, 1, 0, 0));
            foreach (Collider col in Physics.OverlapSphere(transform.position, range))
            {
                if (col.tag == "Player")
                {
                    RaycastHit rayHit;
                    Physics.Raycast(transform.position, col.transform.position - transform.position, out rayHit, range);
                    if (rayHit.collider == col) col.GetComponent<Health>().getShot(damage * (Vector3.Distance(transform.position, col.transform.position) / range));
                }
            }
            NetworkServer.Spawn(newGrenadeExplosion);
        }


    }
}