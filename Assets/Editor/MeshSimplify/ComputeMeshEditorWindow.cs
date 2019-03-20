using Chaos;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
/// <summary>
/// 计算Mesh坍塌信息 Job Winodw
/// @author:DXS
/// @data:2018/10/29
/// </summary>
public class ComputeMeshEditorWindow : EditorWindow
{
    static Rect windowRect = new Rect(100, 200, 600, 400);
    [MenuItem("Tools/ComputeMesh")]
    static void OnInit()
    {
        ComputeMeshEditorWindow window = GetWindowWithRect<ComputeMeshEditorWindow>(windowRect, false, "减面工具");
        window.Show();
    }

    Vector2 scrollPos;
    /// <summary>
    /// 计算的Mesh的集合
    /// </summary>
    [SerializeField]
    List<GameObject> computeMeshGoList = new List<GameObject>();

    SerializedObject _serializedObject;
    SerializedProperty _computeSerialzedProperty;
    bool isAllPrefab = false;
    bool cacheTogChange = false;
    bool isClickButton = false;
    string[] searchFolderPath = new string[] { "Assets/Indian" };
    int index = 0;
    private void OnEnable()
    {
        _serializedObject = new SerializedObject(this);
        _computeSerialzedProperty = _serializedObject.FindProperty("computeMeshGoList");
    }

    void OnGUI()
    {
        _serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        GUILayout.Label("计算减面的Prefab--提示:不要重复计算一个模型数据");
        isAllPrefab = GUILayout.Toggle(isAllPrefab, "是否计算所有Prefab");
        if (isAllPrefab != cacheTogChange)
        {
            cacheTogChange = isAllPrefab;
            computeMeshGoList.Clear();
        }
        if (isAllPrefab)
        {
            //查找工程中所有的prefab模型数据
            string[] ids = AssetDatabase.FindAssets("t:Prefab", searchFolderPath);
            for (int i = 0; i < ids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(ids[i]);
                GameObject objPrefab = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
                if (objPrefab == null)
                {
                    Debug.Log("Prefab为空");
                }
                else if (!computeMeshGoList.Contains(objPrefab))
                {
                    computeMeshGoList.Add(objPrefab);
                }
            }
            EditorGUILayout.PropertyField(_computeSerialzedProperty, true);
        }
        else
        {
            EditorGUILayout.PropertyField(_computeSerialzedProperty, true);
        }
        if (GUILayout.Button("开始计算"))
        {
            index = 0;
            OnComputeMesh();
        }
        if (EditorGUI.EndChangeCheck())
        {
            _serializedObject.ApplyModifiedProperties();
        }
        //Close();
        //EditorUtility.ClearProgressBar();
    }
    void OnComputeMesh()
    {
        for (int i = 0; i < computeMeshGoList.Count; i++)
        {
            //1.清除原来的数据
            GameObject go = computeMeshGoList[i];
            MeshSimplify simplify = go.GetComponent<MeshSimplify>();
            if (go == null || simplify == null)
            {
                continue;
            }
            simplify.RestoreOriginalMesh(true, simplify.m_meshSimplifyRoot == null);
            RemoveChildMeshSimplifyComponents(simplify);
            //2.开始计算
            try
            {
                if (simplify.DataDirty || simplify.HasData() == false || simplify.HasNonMeshSimplifyGameObjectsInTree())
                {
                    simplify.RestoreOriginalMesh(true, simplify.m_meshSimplifyRoot == null);
                    bool mMeshSimplefyRoot = simplify.m_meshSimplifyRoot == null;
                    simplify.ComputeData(mMeshSimplefyRoot, null);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error generating mesh: " + e.Message + " Stack: " + e.StackTrace);
                EditorUtility.ClearProgressBar();
                Simplifier.Cancelled = false;
            }
            //3.最后应用Prefab
        }
        isClickButton = true;
    }
    void Update()
    {
        if (isClickButton)
        {
            for (int i = 0; i < computeMeshGoList.Count; i++)
            {
                EditorUtility.DisplayProgressBar("OnComputeMesh", "mesh|" + computeMeshGoList[i] + " " + index, (float)index / computeMeshGoList.Count);
                GameObject go = computeMeshGoList[i];
                if (go == null)
                {
                    continue;
                }
                for (int ichild = 0; ichild < go.transform.childCount; ichild++)
                {
                    Transform childTrans = go.transform.GetChild(ichild);
                    MeshSimplify simplify = childTrans.GetComponent<MeshSimplify>();
                    if (simplify != null && simplify.GetSimplifyComputeHeap() != null)
                    {
                        ComputeHeap computeHeap = simplify.GetSimplifyComputeHeap();
                        if (computeHeap.GetJobHandleIsComplete())
                        {
                            computeHeap.JobComputeComplete();
                            index++;
                        }
                    }
                }
            }
        }
        if (index != 0 && index == computeMeshGoList.Count)
        {
            isClickButton = false;
            index = 0;
            EditorUtility.ClearProgressBar();
            Debug.Log("<color=yellow>所有的模型数据计算完成————————</color>");
        }
    }

    void RemoveChildMeshSimplifyComponents(MeshSimplify meshSimplify)
    {
        RemoveChildMeshSimplifyComponentsRecursive(meshSimplify.gameObject, meshSimplify.gameObject, true);
    }
    void RemoveChildMeshSimplifyComponentsRecursive(GameObject root, GameObject gameObject, bool bRecurseIntoChildren)
    {
        MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();
        if (meshSimplify != null && meshSimplify.m_meshSimplifyRoot != null)
        {
            if (Application.isEditor && Application.isPlaying == false)
            {
                UnityEngine.Object.DestroyImmediate(meshSimplify, true);
            }
            else
            {
                UnityEngine.Object.Destroy(meshSimplify);
            }
        }
        if (bRecurseIntoChildren)
        {
            for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
            {
                RemoveChildMeshSimplifyComponentsRecursive(root, gameObject.transform.GetChild(nChild).gameObject, true);
            }
        }
    }
}
