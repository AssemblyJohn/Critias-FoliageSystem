/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

namespace CritiasFoliage
{
    public delegate void OnExtractDetailsPressed(List<FoliageDetailExtracterMapping> toExtract, IEnumerable<Terrain> terrain, bool disable, bool delete);

    public class FoliageExtracterDetailsWindow : ScriptableWizard
    {
        private class DetailPrototypeData
        {
            public int m_DetailLayer;

            public bool m_ShouldExtract = true;

            // If we have a 'None' mapping extraction
            public bool m_NoneMapping = true;

            public int m_FoliageHashMapping;
            public string m_FoliageTypeNameMapping;

            public float m_ExtractedDensity = 1;
        }
        
        private List<Terrain> m_TerrainsExtract = new List<Terrain>();
        private OnExtractDetailsPressed m_Callback;

        private FoliagePainter m_Painter;
        
        private bool m_DisableAfterExtraction = true;
        private bool m_DeleteAfterExtraction = false;

        private List<FoliageTypeRuntime> m_TypesRuntime;
        private DetailPrototype[] m_Prototypes;
        private DetailPrototypeData[] m_PrototypesData;

        public void Init(FoliagePainter painter, OnExtractDetailsPressed extract)
        {
            m_Painter = painter;
            m_Callback = extract;

            m_TypesRuntime = m_Painter.GetFoliageTypesRuntime();
        }
        
        private bool HasSameDetails(Terrain main, Terrain other)
        {
            DetailPrototype[] protoMain = main.terrainData.detailPrototypes;
            DetailPrototype[] protoOther = other.terrainData.detailPrototypes;

            if (protoOther == null || protoMain.Length != protoOther.Length)
                return false;

            for(int i = 0; i < protoMain.Length; i++)
            {
                if (protoMain[i].usePrototypeMesh != protoOther[i].usePrototypeMesh)
                    return false;

                if (protoMain[i].usePrototypeMesh)
                {
                    if (protoMain[i].prototype != protoOther[i].prototype)
                        return false;
                }
                else
                {
                    if (protoMain[i].prototypeTexture != protoOther[i].prototypeTexture)
                        return false;
                }
            }

            return true;
        }
        
        private static void RecursivelyExtractTerrains(GameObject possibleTerrain, List<Terrain> terrains)
        {
            if(possibleTerrain.GetComponent<Terrain>() != null)
            {
                terrains.Add(possibleTerrain.GetComponent<Terrain>());
            }

            for (int i = 0; i < possibleTerrain.transform.childCount; i++)
                RecursivelyExtractTerrains(possibleTerrain.transform.GetChild(i).gameObject, terrains);
        }

        protected override bool DrawWizardGUI()
        {
            EditorGUILayout.LabelField("Add terrains or objects here: ");

            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_HORIZONTAL))
            {                
                GameObject possibleTerrains = EditorGUILayout.ObjectField(null, typeof(GameObject), true) as GameObject;

                if (possibleTerrains != null)
                {
                    List<Terrain> terrains = new List<Terrain>();
                    RecursivelyExtractTerrains(possibleTerrains, terrains);

                    for (int i = 0; i < terrains.Count; i++)
                    {
                        Terrain extr = terrains[i];

                        if (m_TerrainsExtract.Contains(extr) == false)
                        {
                            if (extr.GetComponent<Terrain>() != null)
                            {
                                // If it's the first (main) terrain
                                if (m_TerrainsExtract.Count == 0)
                                {
                                    DetailPrototype[] protos = extr.GetComponent<Terrain>().terrainData.detailPrototypes;

                                    if (protos != null && protos.Length > 0)
                                    {
                                        m_TerrainsExtract.Add(extr);
                                    }
                                    else
                                        EditorUtility.DisplayDialog("Warning!", "The first added (main) terrain does not have any terrain details!", "Ok");
                                }
                                else
                                {
                                    // Check if the same details appear
                                    if (HasSameDetails(m_TerrainsExtract[0].GetComponent<Terrain>(), extr.GetComponent<Terrain>()))
                                        m_TerrainsExtract.Add(extr);
                                    else
                                        EditorUtility.DisplayDialog("Warning!", "The added terrain does not have the same details as the first (main) terrain!", "Ok");
                                }
                            }
                            else
                                EditorUtility.DisplayDialog("Warning!", "You can only extract details from terrains!", "Ok");
                        }
                    }
                }
            }
            
            // Display all the detail data
            if (m_TerrainsExtract.Count == 0)
                return false;

            EditorGUILayout.Space();

            // Extracted terrains
            EditorGUILayout.LabelField("Terrain to extract details from: ");
            
            // Show the data
            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_HORIZONTAL))
            {
                for (int i = 0; i < m_TerrainsExtract.Count; i++)
                    EditorGUILayout.LabelField("(T) " + m_TerrainsExtract[i].name + (i == 0 ? " [Main Details]" : ""));
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Details mappings: ");

            if (m_Prototypes == null)
            {
                m_Prototypes = m_TerrainsExtract[0].GetComponent<Terrain>().terrainData.detailPrototypes;

                if(m_Prototypes == null || m_Prototypes.Length == 0)
                {
                    FoliageLog.e("No terrain details found!");
                    return false;
                }

                // Generate the UI data
                m_PrototypesData = new DetailPrototypeData[m_Prototypes.Length];
                for (int i = 0; i < m_PrototypesData.Length; i++)
                {
                    m_PrototypesData[i] = new DetailPrototypeData();
                    m_PrototypesData[i].m_DetailLayer = i;

                    string protoName = m_Prototypes[i].prototype != null ? m_Prototypes[i].prototype.name : m_Prototypes[i].prototypeTexture.name;

                    // Attempt to search through the data to check for a name or something
                    int foundIdx = m_TypesRuntime.FindIndex((x) =>
                    {
                        return x.m_Name.ToLowerInvariant().Replace(" ", "").Contains(protoName.ToLowerInvariant().Replace(" ", ""));
                    });

                    if (foundIdx >= 0)
                    {
                        m_PrototypesData[i].m_NoneMapping = false;
                        m_PrototypesData[i].m_FoliageHashMapping = m_TypesRuntime[foundIdx].m_Hash;
                        m_PrototypesData[i].m_FoliageTypeNameMapping = m_TypesRuntime[foundIdx].m_Name;
                    }
                }
            }

            // Set all the data related to that terrain and stuff            
            DetailPrototype[] prototypes = m_Prototypes;
            
            using(new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {
                for (int i = 0; i < prototypes.Length; i++)
                {
                    DetailPrototype proto = prototypes[i];
                    DetailPrototypeData protoData = m_PrototypesData[i];

                    using (new ScopedLayout(() => { EditorGUILayout.BeginVertical(); }, EBeginMode.BEGIN_HORIZONTAL))
                    {
                        string name;

                        if (proto.prototype != null)
                            name = proto.prototype.name;
                        else
                            name = proto.prototypeTexture.name;

                        // Each prototype data
                        using (new ScopedLayout(() => { EditorGUILayout.BeginHorizontal(); }, EBeginMode.BEGIN_HORIZONTAL))
                        {                            
                            protoData.m_ShouldExtract = EditorGUILayout.Toggle(new GUIContent("Extract: [" + name + "]", "If we should extract that type"), protoData.m_ShouldExtract);

                            if (protoData.m_ShouldExtract)
                            {
                                EditorGUILayout.LabelField("as: ", GUILayout.Width(30));

                                bool dropdown = EditorGUILayout.DropdownButton(new GUIContent(protoData.m_NoneMapping ? "None" : protoData.m_FoliageTypeNameMapping,
                                    "To what foliage type this detail will be changed transformed when extracting from the terrain"), FocusType.Passive);

                                if (dropdown)
                                {
                                    GenericMenu menu = new GenericMenu();

                                    menu.AddItem(new GUIContent("None"), protoData.m_NoneMapping, (object obj) =>
                                    {
                                        protoData.m_NoneMapping = true;
                                    }, null);

                                    menu.AddSeparator("");

                                    for (int r = 0; r < m_TypesRuntime.Count; r++)
                                    {
                                        FoliageTypeRuntime rt = m_TypesRuntime[r];
                                        bool on = protoData.m_NoneMapping == false && protoData.m_FoliageHashMapping == rt.m_Hash;

                                        menu.AddItem(new GUIContent(rt.m_Name), on, (object obj) =>
                                        {
                                            protoData.m_NoneMapping = false;
                                            protoData.m_FoliageHashMapping = ((FoliageTypeRuntime)obj).m_Hash;
                                            protoData.m_FoliageTypeNameMapping = ((FoliageTypeRuntime)obj).m_Name;
                                        }, rt);
                                    }

                                    menu.ShowAsContext();
                                }
                            }
                        }

                        if (protoData.m_ShouldExtract)
                        {
                            protoData.m_ExtractedDensity = EditorGUILayout.Slider(new GUIContent("[" + name + "] Density [0..1]",
                                "A multiplier for the count of extracted details from the terrain. 1 means extract all instances, 0.5 means extract half of them, 0 extract none. " +
                                "Use mostly for extracted grass, but not details like rocks or anything else like that. Since on the terrain we only have a lot of billboards as " +
                                "grass and we might map them to some 3D mesh clumps it's higly likely that we will only need a half (0.5) or a third (0.3) of that existing terrain data. "),
                                protoData.m_ExtractedDensity, 0, 1);
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            // Settings 
            EditorGUILayout.LabelField("Settings: ");
            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {                
                m_DisableAfterExtraction = EditorGUILayout.Toggle(new GUIContent(
                    "Disable After Extraction",
                    "If we should disable the details after we extracted them from the terrain. This will will set 'Terrain.drawTreesAndFoliage' to false."),
                    m_DisableAfterExtraction, GUILayout.ExpandWidth(true));

                bool deleteAfterExtraction = EditorGUILayout.Toggle(new GUIContent(
                    "Delete After Extraction",
                    "If this is checked it will delete all the details that were extracted. Will delete the extracted details. Not advisable!"),
                    m_DeleteAfterExtraction, GUILayout.ExpandWidth(true));

                if (m_DeleteAfterExtraction != deleteAfterExtraction)
                {
                    if (deleteAfterExtraction)
                    {
                        bool sure = EditorUtility.DisplayDialog("Warning", 
                            "Setting this to true will delete all the extracted details from the terrain! " +
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
            if (m_TerrainsExtract.Count > 0)
            {
                if (m_TerrainsExtract.Count > 1)
                {
                    for (int i = 1; i < m_TerrainsExtract.Count; i++)
                    {
                        if (HasSameDetails(m_TerrainsExtract[0].GetComponent<Terrain>(), m_TerrainsExtract[i].GetComponent<Terrain>()) == false)
                        {
                            FoliageLog.e("Missing type when verified! Don't modify the types/details while extracting details!");
                            return;
                        }
                    }
                }

                Terrain terrain = m_TerrainsExtract[0].GetComponent<Terrain>();
                DetailPrototype[] proto = terrain.terrainData.detailPrototypes;
                
                List<FoliageDetailExtracterMapping> mappings = new List<FoliageDetailExtracterMapping>();
                
                // Build all the extracting data
                for(int i = 0; i < m_PrototypesData.Length; i++)
                {
                    var data = m_PrototypesData[i];

                    if (data.m_ShouldExtract == true && data.m_NoneMapping == false)
                    {
                        FoliageDetailExtracterMapping mapping = new FoliageDetailExtracterMapping();
                        mapping.m_DetailLayer = data.m_DetailLayer;
                        mapping.m_FoliageTypeHash = data.m_FoliageHashMapping;
                        mapping.m_ExtractedDensity = data.m_ExtractedDensity;

                        mappings.Add(mapping);
                    }
                }

                if (mappings.Count > 0)
                {
                    // Check that we have all the types and nothing has been tampered between the create
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        if (m_Painter.HasFoliageType(mappings[i].m_FoliageTypeHash) == false)
                        {
                            FoliageLog.e("Missing type when created! Don't modify the types while extracting details!");
                            return;
                        }

                        if (mappings[i].m_DetailLayer < 0 || mappings[i].m_DetailLayer >= proto.Length)
                        {
                            FoliageLog.e("Missing type when created! Don't modify the types while extracting details!");
                            return;
                        }
                    }
                    
                    // Proceed with the extraction
                    m_Callback(mappings, m_TerrainsExtract, m_DisableAfterExtraction, m_DeleteAfterExtraction);
                }
                else
                {
                    FoliageLog.i("Nothing to extract!");
                }
            }
            else
            {
                FoliageLog.i("Nothing to extract!");
            }
        }
    }
}
