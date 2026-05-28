using UnityEngine;

[CreateAssetMenu(fileName = "NewWand", menuName = "MiniMages/Wand")]
public class WandData : ScriptableObject
{
    public string wandName;
    public ElementType elementType;
    public Sprite elementIcon; // Add this line for your overhead graphic!

    [Header("Basic Attack")]
    public GameObject basicProjectilePrefab;
    public GameObject heldBasicProjectilePrefab;
    public float basicManaCost = 0f;

    [Header("Q Skill")]
    public GameObject qProjectilePrefab;
    public float qManaCost = 15f;

    [Header("E Skill")]
    public GameObject eProjectilePrefab;
    public float eManaCost = 30f;
}
