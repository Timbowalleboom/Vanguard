using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

//TODO: Maybe implement a grenade ready animation (animation or something gis played when grenade key is held down)


// Equivalent to PlayerGunManager but for grenades. no weapon object is needed

namespace Vanguard
{
    public class GrenadeManager : NetworkBehaviour
    {
        public GameObject grenadePrefab;
        public Rigidbody playerRigdbody;
        public GameObject grenadeThrowPoint;
        private GrenadeArc grenadeArcScript;


        delegate void GrenadeUpdate(Vector3 grenadeVelocity, Vector3 startPostion);
        GrenadeUpdate grenadeUpdate;

        // Start is called before the first frame update
        void Start()
        {
            if (!isLocalPlayer)
            {
                enabled = false;
            }
            else
            {
                
                InputManager.OnGrenadeStopped += GrenadeTriggerUp;
                InputManager.OnGrenadeStarted += GrenadeTriggerDown;
                grenadeArcScript = grenadeThrowPoint.GetComponent<GrenadeArc>();
            }
        }


        private void GrenadeTriggerUp()
        {
            Debug.Log("Grenade trigger up");
            grenadeUpdate -= grenadeArcScript.UpdateLineRenderer; // Will this cause an error if you somehow manage to trigger up without doing a trigger down?
            grenadeArcScript.StopLineRenderer();
            CmdThrowCommand(grenadeThrowPoint.transform.position, Camera.main.transform.rotation, playerRigdbody.velocity);

        }

        private void GrenadeTriggerDown() {
            Debug.Log("Grenade trigger down");
            grenadeUpdate += grenadeArcScript.UpdateLineRenderer; // will this cause it to add the function to the delegate multiple times?

        }
        // Update is called once per frame

        void Update()
        {
            if (grenadeUpdate != null) { grenadeUpdate(playerRigdbody.velocity + Camera.main.transform.rotation*(grenadePrefab.GetComponent<Grenade>().nadeVelocity * Vector3.forward), grenadeThrowPoint.transform.position); }
        }


        [Command]
        public void CmdThrowCommand(Vector3 Position, Quaternion Rotation, Vector3 playerVelocity)
        {
            GameObject grenadeObject = Instantiate(grenadePrefab, Position, Rotation);
            grenadeObject.GetComponent<Rigidbody>().AddForce(playerVelocity, ForceMode.VelocityChange);
            NetworkServer.Spawn(grenadeObject);
        }
    }
}