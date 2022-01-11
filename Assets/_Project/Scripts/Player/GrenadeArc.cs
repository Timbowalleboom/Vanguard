using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GrenadeArc : MonoBehaviour
{
    LineRenderer grenadeLR;
    Vector3 gravity;
    public int maxResolution = 100;
    private int time;
    public float timeMultiplier = 0.05f;

    private void Awake()
    {
        grenadeLR = GetComponent<LineRenderer>();
        gravity = Physics.gravity;
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }
    
    //call this to make the line rederer render the arc
    public void UpdateLineRenderer(Vector3 grenadeVelocity, Vector3 startPostion) {
        //Debug.Log("UpdateLineRenderer");
        Debug.Log("start position" + startPostion);
        Vector3[] arcPoints = GetGrenadeArcPoints(grenadeVelocity,startPostion);
        grenadeLR.positionCount = time + 1;
        Debug.Log("arc points list length:" + arcPoints.Length);
        foreach (Vector3 i in arcPoints) {
            Debug.Log(i);
        }
        /*
        for (int i = 0; i < maxResolution; i++) {
            grenadeLR.SetPosition(i, arcPoints[i]);
        }
        */
        grenadeLR.SetPositions(arcPoints); //SetPositions does not seem to be working as expected, only 1 or 2 points max seem to go through and they arent even points in arcPoints idk why
    }

    public void StopLineRenderer() {
        grenadeLR.positionCount = 0;
    }

    //Calculates all the points of the arc + collisions
    private Vector3[] GetGrenadeArcPoints(Vector3 grenadeVelocity, Vector3 startPostion) {
        time = 0;
        Vector3[] arcPoints = new Vector3[maxResolution];
        arcPoints[0] = startPostion;
        while (time < maxResolution - 1)
        {
            time += 1;


            Vector3 finalPosition = startPostion + grenadeVelocity * time*timeMultiplier + 0.5f * gravity * time * time * timeMultiplier * timeMultiplier;
            RaycastHit hit;

            if (Physics.Linecast(arcPoints[time - 1], finalPosition, out hit)) {
                arcPoints[time] = hit.point;
                return arcPoints;
            }
            else
            {
                arcPoints[time] = finalPosition;
            }
        }
        return arcPoints;
    }

}
