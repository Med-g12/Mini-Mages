using UnityEngine;

[CreateAssetMenu(fileName = "NewWand", menuName = "MiniMages/Wand")]
public class WandData : ScriptableObject
{
    public string wandName;
    public GameObject basicProjectilePrefab;
    public float basicManaCost = 0f;
    [Header("Q Skill")]
    public GameObject qProjectilePrefab;
    public float qManaCost = 15f;
    [Header("E Skill")]
    public GameObject eProjectilePrefab;
    public float eManaCost = 30f;
}
