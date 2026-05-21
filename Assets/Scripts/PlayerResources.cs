using UnityEngine;
using UnityEngine.UI;

public class PlayerResources : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;
    public float maxMana = 100f;
    public float currentMana;
    public float manaRegenRate = 10f;

    public Slider healthSlider;
    public Slider manaSlider;

    void Start()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
    }

    void Update()
    {
        if (currentMana < maxMana)
        {
            currentMana += manaRegenRate * Time.deltaTime;
            manaSlider.value = currentMana / maxMana;
        }
    }

    public bool SpendMana(float amount)
    {
        if (currentMana >= amount)
        {
            currentMana -= amount;
            manaSlider.value = currentMana / maxMana;
            return true;
        }
        return false;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        healthSlider.value = currentHealth / maxHealth;
        if (currentHealth <= 0) Debug.Log("Player Died!");
    }
}
