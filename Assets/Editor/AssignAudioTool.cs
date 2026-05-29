using UnityEngine;
using UnityEditor;
using System.IO;

public class AssignAudioTool
{
    [MenuItem("Mini-Mages/Assign Element Sounds")]
    public static void AssignSounds()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (playerPrefab == null)
        {
            Debug.LogError("Could not find Player prefab.");
            return;
        }

        WeaponManager wm = playerPrefab.GetComponent<WeaponManager>();
        if (wm == null)
        {
            Debug.LogError("No WeaponManager found on Player prefab.");
            return;
        }

        AudioClip wind = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/wind_shoot.wav");
        AudioClip water = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/water_shoot.wav");
        AudioClip earth = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/earth_shoot.wav");
        AudioClip fire = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/fire_shoot.wav");

        if (wind == null || water == null || earth == null || fire == null)
        {
            Debug.LogWarning("Some audio clips are missing. Did they finish importing?");
        }

        wm.elementShootSounds = new AudioClip[4];
        wm.elementShootSounds[0] = wind;
        wm.elementShootSounds[1] = water;
        wm.elementShootSounds[2] = earth;
        wm.elementShootSounds[3] = fire;

        EditorUtility.SetDirty(playerPrefab);
        AssetDatabase.SaveAssets();

        // Also assign to any WeaponManager in the active scene!
        WeaponManager[] sceneWms = GameObject.FindObjectsByType<WeaponManager>(FindObjectsSortMode.None);
        foreach (WeaponManager sceneWm in sceneWms)
        {
            sceneWm.elementShootSounds = new AudioClip[4];
            sceneWm.elementShootSounds[0] = wind;
            sceneWm.elementShootSounds[1] = water;
            sceneWm.elementShootSounds[2] = earth;
            sceneWm.elementShootSounds[3] = fire;
            
            AudioSource source = sceneWm.GetComponent<AudioSource>();
            if (source == null)
            {
                source = sceneWm.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            EditorUtility.SetDirty(sceneWm);
        }
        
        Debug.Log("Successfully assigned audio clips to the Player's WeaponManager (both Prefab and Scene)!");
    }
}
