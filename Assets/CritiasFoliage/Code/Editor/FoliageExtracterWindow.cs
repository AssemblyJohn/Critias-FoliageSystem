/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

namespace CritiasFoliage
{   
    public delegate void OnExtractPressed(List<GameObject> toExtract, bool autoExtract, bool disable, bool delete);

    public class FoliageExtracterWindow : ScriptableWizard
    {
        private List<GameObject> m_Extract = new List<GameObject>();
        private OnExtractPressed m_Callback;

        private bool m_AutoExtractPrototypes = true;
        private bool m_DisableAfterExtraction = true;
        private bool m_DeleteAfterExtraction = false;        

        public void Init(OnExtractPressed extract)
        {
            m_Callback = extract;
        }

        protected override bool DrawWizardGUI()
        {
            // Have a size of 1 always
            if (m_Extract.Count == 0)
                m_Extract.Add(null);

            // Objects to extract
            EditorGUILayout.LabelField("Objects to extract trees from: ");
            using(new ScopedLayout(()=> { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_HORIZONTAL))
            {
                bool lastNonNull = false;

                for (int i = 0; i < m_Extract.Count; i++)
                {                    
                    GameObject extr = EditorGUILayout.ObjectField(m_Extract[i], typeof(GameObject), true) as GameObject;

                    if (extr != null)
                    {
                        if (extr.GetComponent<Terrain>() != null)
                        {
                            FoliageLog.i("Terrain detected!");
                            m_Extract[i] = extr;
                        }
                        else
                        {
                            if (extr.transform.parent == null)
                            {
                                FoliageLog.i("Tree holder detected!");
                                m_Extract[i] = extr;
                            }
                            else
                            {                                
                                EditorUtility.DisplayDialog("Warning!", "You can only extract foliage from terrains and objects that are at the root of the hierarchy!", "Ok");
                            }
                        }
                    }
                    
                    if (i == m_Extract.Count - 1 && m_Extract[i] != null)
                    {
                        lastNonNull = true;
                    }
                }

                // If the last item is not null, then make it larger
                if (lastNonNull)
                    m_Extract.Add(null);
            }

            // Settings 
            EditorGUILayout.LabelField("Settings: ");
            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {
                using (new ScopedLayout(()=> { EditorGUILayout.BeginHorizontal(); }, EBeginMode.BEGIN_HORIZONTAL))
                {
                    EditorGUILayout.LabelField(new GUIContent(
                        "Auto Extract Types (Terrain Only)",
                        "If this is checked and we are extracting foliage from a terrain then the system will atempt to create automatically " +
                        "the foliage types that it requires. It will not work for for objects that are not terrains!"),
                        GUILayout.ExpandWidth(true));

                    m_AutoExtractPrototypes = EditorGUILayout.Toggle(m_AutoExtractPrototypes, GUILayout.ExpandWidth(false));
                }

                m_DisableAfterExtraction = EditorGUILayout.Toggle(new GUIContent(
                    "Disable After Extraction",
                    "If we should disable the trees after we extracted them from the terrain or object. This will disable the extracted objects " +
                    "with 'SetActive(false)' and will set 'Terrain.drawTreesAndFoliage' to false."),
                    m_DisableAfterExtraction, GUILayout.ExpandWidth(true));

                bool deleteAfterExtraction = EditorGUILayout.Toggle(new GUIContent(
                    "Delete After Extraction",
                    "If this is checked it will delete all the objects that were extracter. Will delete the extracted trees and all the extracted " +
                    "tree instances from the terrains. not advisable!"),
                    m_DeleteAfterExtraction, GUILayout.ExpandWidth(true));

                if (m_DeleteAfterExtraction != deleteAfterExtraction)
                {
                    if (deleteAfterExtraction)
                    {
                        bool sure = EditorUtility.DisplayDialog("Warning", 
                            "Setting this to true will delete all the extracted trees and foliage! " +
                            "Not recomended if you want to try multiple iterations! Are you sure?",
                            "Yes", "No");

                        if (sure)
                            m_DeleteAfterExtraction = true;
                    }
                    else
                    {
                        m_DeleteAfterExtraction = deleteAfterExtraction;
                    }
                }                
            }
            
            return false;
        }
        
        void OnWizardCreate()
        {
            FoliageLog.d("On wizard create!");

            // Build the unique stuff
            List<GameObject> uniq = new List<GameObject>();

            for (int i = 0; i < m_Extract.Count; i++)
            {
                if (m_Extract[i] != null && uniq.Contains(m_Extract[i]) == false)
                    uniq.Add(m_Extract[i]);
            }

            if (uniq.Count > 0)
                m_Callback(uniq, m_AutoExtractPrototypes, m_DisableAfterExtraction, m_DeleteAfterExtraction);
            else
                FoliageLog.w("Could not extract anything from the list!");
        }
    }
}
