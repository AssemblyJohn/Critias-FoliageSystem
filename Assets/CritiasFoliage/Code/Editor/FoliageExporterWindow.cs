/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

namespace CritiasFoliage
{

    public class FoliageExporterWindow : ScriptableWizard
    {
        private System.Action<int[]> m_Callback;
        private FoliagePainter m_Painter;

        private List<FoliageTypeRuntime> m_Types;
        private bool[] m_Extracting;

        public void Init(FoliagePainter painter, System.Action<int[]> extract)
        {
            m_Painter = painter;
            m_Callback = extract;            
        }

        protected override bool DrawWizardGUI()
        {
            EditorGUILayout.LabelField("Objects to extract: ");

            m_Types = m_Painter.GetFoliageTypesRuntime();

            if (m_Extracting == null || m_Extracting.Length != m_Types.Count)
                m_Extracting = new bool[m_Types.Count];

            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {
                for (int i = 0; i < m_Types.Count; i++)
                {
                    m_Extracting[i] = GUILayout.Toggle(m_Extracting[i], m_Types[i].m_Name);
                }
            }
            
            return false;
        }

        void OnWizardCreate()
        {
            FoliageLog.d("On wizard create!");

            List<int> hashes = new List<int>();

            for(int i = 0; i < m_Types.Count; i++)
            {
                int hash = m_Types[i].m_Hash;
                if (m_Extracting[i] && m_Painter.HasFoliageType(hash) && hashes.Contains(hash) == false)
                {
                    hashes.Add(hash);
                }
            }

            if(hashes.Count > 0)
            {                
                m_Callback(hashes.ToArray());
            }
            else
                FoliageLog.w("Could not extract anything from the list!");                
        }
    }
}
