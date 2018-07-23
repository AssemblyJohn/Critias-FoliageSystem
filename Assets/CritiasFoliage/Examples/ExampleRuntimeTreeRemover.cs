using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleRuntimeTreeRemover : MonoBehaviour
{
    private void OnGUI()
    {
        Vector2 center = Camera.main.ViewportToScreenPoint(new Vector2(0.5f, 0.5f));
        GUI.Label(new Rect(center.x - 5, center.y - 5, 50, 50), "o");
    }

    /**
     * Runs basic code to remove a tree at runtime. You should implement your own logic and not call this each frame
     * since it will cause a severe FPS drop.
     * 
     * NOTE: Even if the maximum distance is set to '1000' MAKE SURE that you also set the maximum distance of the collision data in the
     * 'Foliage Colliders' script to something larger than the default 7! Else colliders will only have 7m of colliders and therefore you 
     * can remove only trees at 7m distance.
     */
    void Update ()
    {
        if (Input.GetMouseButtonDown(0) == false)
            return;

        CritiasFoliage.FoliagePainterRuntime runtime = FindObjectOfType<CritiasFoliage.FoliagePainter>().GetRuntime;

        // Change this to whatever layer you use for trees
        const string queryLayer = "Default";

        RaycastHit hit;
        if(Physics.Raycast(transform.position, transform.forward, out hit, 1000, LayerMask.GetMask(queryLayer)))
        {
            if(hit.collider)
            {
                // Try and get the value if the collider is sticked here
                var data = hit.collider.gameObject.GetComponent<CritiasFoliage.FoliageColliderData>();
                
                if(data)
                {
                    CritiasFoliage.FoliageInstance instance = data.m_FoliageInstance;
                    runtime.RemoveFoliageInstance(data.m_FoliageType, instance.m_UniqueId, instance.m_Position);
                }

                // Try and get the value if maybe it is in the owner in case we are an owned collider
                data = hit.collider.gameObject.GetComponentInParent<CritiasFoliage.FoliageColliderData>();

                if(data)
                {
                    CritiasFoliage.FoliageInstance instance = data.m_FoliageInstance;
                    runtime.RemoveFoliageInstance(data.m_FoliageType, instance.m_UniqueId, instance.m_Position);
                }
            }
        }
	}
}
