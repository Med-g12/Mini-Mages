using UnityEngine;
using UnityEditor;

public class AssignMusicTool
{
    [MenuItem("Mini-Mages/Assign Background Music")]
    public static void AssignMusic()
    {
        // Find GameDirector in the active scene
        GameDirector gd = GameObject.FindAnyObjectByType<GameDirector>();
        if (gd == null)
        {
            Debug.LogError("Could not find GameDirector in the active scene.");
            return;
        }

        AudioClip normalBGM = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/STI Hymn Instrumental.mp3");
        AudioClip bossBGM = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/STI Hymn.mp3");

        if (normalBGM == null || bossBGM == null)
        {
            Debug.LogError("Could not find one or both of the music files. Are they placed exactly in Assets/Music/ ?");
            return;
        }

        gd.normalBGM = normalBGM;
        gd.bossBGM = bossBGM;

        EditorUtility.SetDirty(gd);
        // Also save the scene changes
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gd.gameObject.scene);

        Debug.Log("Successfully assigned normal BGM and boss BGM to GameDirector!");
    }
}
