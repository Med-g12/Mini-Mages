using UnityEditor;

[CustomEditor(typeof(PickupableItem))]
public class PickupableItemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("healthToAdd"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("manaToAdd"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isBuff"));

        if (serializedObject.FindProperty("isBuff").boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("speedToAdd"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("damageToAdd"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("buffDuration"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pickupRadius"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("destroyOnPickup"));

        serializedObject.ApplyModifiedProperties();
    }
}
