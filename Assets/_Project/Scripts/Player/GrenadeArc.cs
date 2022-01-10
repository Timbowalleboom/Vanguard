using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GrenadeArc : MonoBehaviour
{
    LineRenderer grenadeLR;
    Vector3 gravity;
    public int maxResolution = 10;
    private int time;

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
        Debug.Log("UpdateLineRenderer");
        Vector3[] arcPoints = GetGrenadeArcPoints(grenadeVelocity,startPostion);
        grenadeLR.positionCount = time;
        Debug.Log(arcPoints);
        grenadeLR.SetPositions(arcPoints);
    }
    //Calculates all the points of the arc + collisions
    private Vector3[] GetGrenadeArcPoints(Vector3 grenadeVelocity, Vector3 startPostion) {
        time = 0;
        Vector3[] arcPoints = new Vector3[maxResolution];
        arcPoints[0] = startPostion;
        while (time < maxResolution - 1)
        {
            time += 1;


            Vector3 finalPosition = startPostion + grenadeVelocity * time + 0.5f * gravity * time * time;
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
