using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleRuntimeTreeAdder : MonoBehaviour
{
    private void OnGUI()
    {
        Vector2 center = Camera.main.ViewportToScreenPoint(new Vector2(0.5f, 0.5f));
        GUI.Label(new Rect(center.x - 5, center.y - 5, 50, 50), "o");
    }

    /**
     * Runs basic code to add a tree at runtime. You should implement your own logic and not call this each frame
     * since it will cause a severe FPS drop.     
     */
    void Update()
    {
        if (Input.GetMouseButtonDown(0) == false)
            return;

        CritiasFoliage.FoliagePainterRuntime runtime = FindObjectOfType<CritiasFoliage.FoliagePainter>().GetRuntime;

        var types = runtime.GetFoliageTypes();
        CritiasFoliage.FoliageTypeRuntime treeType = default(CritiasFoliage.FoliageTypeRuntime);
        bool foundTree = false;

        for (int i = 0; i < types.Count; i++)
        {
            if (types[i].m_IsGrassType == false)
            {
                treeType = types[i];
                foundTree = true;
            }
        }

        if (foundTree == false)
        {
            Debug.LogError("Could not find a tree type! Please add it in the inspector!");
            return;
        }

        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, 100, ~0))
        {
            if (hit.collider)
            {
                CritiasFoliage.FoliageInstance inst = new CritiasFoliage.FoliageInstance();

                // All the data that we need to set
                inst.m_Position = hit.point;
                inst.m_Scale = Vector3.one;
                inst.m_Rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

                runtime.AddFoliageInstance(treeType.m_Hash, inst);                
            }
        }
    }
}
