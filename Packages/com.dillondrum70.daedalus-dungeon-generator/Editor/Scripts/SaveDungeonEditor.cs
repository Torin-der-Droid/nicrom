/*
        Copyright (c) 2022 - 2023 Dillon Drummond

        Permission is hereby granted, free of charge, to any person obtaining
        a copy of this software and associated documentation files (the
        "Software"), to deal in the Software without restriction, including
        without limitation the rights to use, copy, modify, merge, publish,
        distribute, sublicense, and/or sell copies of the Software, and to
        permit persons to whom the Software is furnished to do so, subject to
        the following conditions:

        The above copyright notice and this permission notice shall be
        included in all copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
        EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
        MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
        NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
        LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
        OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
        WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

/*
        Daedalus Dungeon Generator: 3D Dungeon Generator Tool
	    By Dillon W. Drummond

	    SaveDungeonEditor.cs

	    ********************************************
	    ***      Editor Tool Implementation      ***
	    ********************************************
 */


using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;



#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

public class EditorProperties : ScriptableObject
{
    public string _savePath = "";
    public string _fileName = "";
    public GameObject _dungeonParent;
}

public class SaveDungeonEditor : EditorWindow
{
    EditorProperties obj;

    SerializedObject editorObject;
    SerializedProperty savePath;
    SerializedProperty fileName;
    SerializedProperty dungeonParent;

    //GameObject dungeonParent;

    DungeonGenerator generator;

    const string DUNGEON_GAMEOBJECT_NAME = "DungeonManager";
    const string DUNGEON_PARENT_NAME = "Dungeon";

    private void OnEnable()
    {
        AssemblyReloadEvents.afterAssemblyReload += Reload;

        Reload();
    }

    private void OnDisable()
    {
        AssemblyReloadEvents.afterAssemblyReload -= Reload;

        Reload();
    }

    void Reload()
    {
        obj = CreateInstance<EditorProperties>();
        editorObject = new SerializedObject(obj);

        savePath = editorObject.FindProperty("_savePath");
        fileName = editorObject.FindProperty("_fileName");
        dungeonParent = editorObject.FindProperty("_dungeonParent");

        dungeonParent.objectReferenceValue = GameObject.Find(DUNGEON_PARENT_NAME);

        //dungeonParent = GameObject.Find(DUNGEON_PARENT_NAME);
    }

    [MenuItem("Tools/Dungeon Editor")]
    public static void ShowDungeonEditor()
    {
        EditorWindow wnd = GetWindow<SaveDungeonEditor>();
        wnd.titleContent = new GUIContent("Dungeon Editor");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Run Generator"))
        {
            if (!generator)
            {
                //generator may have been reloaded after start or quit in editor
                generator = FindAnyObjectByType<DungeonGenerator>();
            }

            if (generator)
            {
                generator.Generate();
            }
            else
            {
                Debug.LogError("DungeonGenerator is null");
            }
        }

        if (GUILayout.Button("Clear Dungeon"))
        {
            if(!generator)
            {
                //generator may have been reloaded after start or quit in editor
                generator = FindAnyObjectByType<DungeonGenerator>();
            }

            if (generator)
            {
                generator.Clear();
            }
            else
            {
                Debug.LogError("DungeonGenerator is null");
            }
        }

        //EditorGUILayout.LabelField("Save Path", savePath.stringValue, EditorStyles.wordWrappedLabel);
        
        //if (GUILayout.Button("Choose Folder"))
        //{
        //    string path = EditorUtility.OpenFolderPanel("Choose Save Path", "", "");
        //    savePath.stringValue = path + "/";
        //}
        
        EditorGUILayout.PropertyField(fileName, new GUIContent("File Name"));
        
        EditorGUILayout.PropertyField(dungeonParent, new GUIContent("Dungeon Parent Object"));

        if (GUILayout.Button("Save to New Additive Scene"))
        {
            SaveScene();
        }
    }

    private void SaveScene()
    {
        if (fileName.stringValue == "")
        {
            Debug.LogError("Must specify a scene name");
            return;
        }

        string startPath = EditorSceneManager.GetActiveScene().path;
        string path = startPath.Substring(0, startPath.LastIndexOf('/'));
        path = path + "/" + fileName.stringValue + ".unity";

        Scene saveScene = EditorSceneManager.GetSceneByPath(path); //Look for existing scene first
        if (!saveScene.IsValid()) //If scene doesn't exist, make a new one
        {
            saveScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            saveScene.name = fileName.stringValue;

            //Make sure new scene is valid
            if (!saveScene.IsValid())
            {
                Debug.LogError("New scene is not valid");
                return;
            }

            //Save new scene before proceeding
            if (!EditorSceneManager.SaveScene(saveScene, path))
            {
                Debug.LogError("Failed to save new scene at " + path);
                EditorSceneManager.CloseScene(saveScene, true);
                return;
            }
        }

        //Move dungeon to scene
        EditorSceneManager.MoveGameObjectToScene((GameObject)dungeonParent.objectReferenceValue, saveScene);

        //Save changes
        if (!EditorSceneManager.SaveScene(saveScene, path))
        {
            Debug.LogError("Failed to save scene at " + path);
            EditorSceneManager.CloseScene(saveScene, true);
            return;
        }

        //PrefabUtility.SaveAsPrefabAsset((GameObject)dungeonParent.objectReferenceValue,
        //    savePath.stringValue + fileName.stringValue + ".prefab",
        //    out bool success);

        //if(!success)
        //{
        //    Debug.LogError("Save Failed");
        //}
        //else
        //{
        //    Debug.Log("Save Complete");
        //}
    }
}
#endif
