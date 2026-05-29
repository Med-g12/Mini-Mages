import os
import re

prefabs_dir = r"d:\Dev\Mini-Mages\Assets\Prefabs"

def patch_file(filename, patterns):
    filepath = os.path.join(prefabs_dir, filename)
    if not os.path.exists(filepath):
        print(f"Not found: {filepath}")
        return

    with open(filepath, 'r') as f:
        content = f.read()

    original_content = content
    for pattern, replacement in patterns:
        content = re.sub(pattern, replacement, content)

    if content != original_content:
        with open(filepath, 'w') as f:
            f.write(content)
        print(f"Updated {filename}")
    else:
        print(f"No changes for {filename}")

def main():
    # EarthEnemy: HP 45, Dmg 15, Speed 1.8
    patch_file("EarthEnemy.prefab", [
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?health:\s*)[\d.]+', r'\g<1>45'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?baseSpeed:\s*)[\d.]+', r'\g<1>1.8'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?contactDamage:\s*)[\d.]+', r'\g<1>15')
    ])

    # WaterEnemy: HP 30, Dmg 10, Speed 2.5 (No changes needed if it's already 30/10/2.5, but just to be sure)
    patch_file("WaterEnemy.prefab", [
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?health:\s*)[\d.]+', r'\g<1>30'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?baseSpeed:\s*)[\d.]+', r'\g<1>2.5'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?contactDamage:\s*)[\d.]+', r'\g<1>10')
    ])

    # FireEnemy: HP 20, Dmg 18, Speed 3.2
    patch_file("FireEnemy.prefab", [
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?health:\s*)[\d.]+', r'\g<1>20'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?baseSpeed:\s*)[\d.]+', r'\g<1>3.2'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?contactDamage:\s*)[\d.]+', r'\g<1>18')
    ])

    # Earth_Boss: HP 350, Dmg 15, Boulder Dmg 25
    patch_file("Earth_Boss.prefab", [
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?health:\s*)[\d.]+', r'\g<1>350'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?contactDamage:\s*)[\d.]+', r'\g<1>15'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EarthBossThrowAttack[\s\S]*?projectileDamage:\s*)[\d.]+', r'\g<1>25')
    ])

    # Water_Boss: HP 250, Dmg 10, Dash Dmg 20
    patch_file("Water_Boss.prefab", [
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?health:\s*)[\d.]+', r'\g<1>250'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?contactDamage:\s*)[\d.]+', r'\g<1>10'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::WaterBossDash[\s\S]*?dashDamage:\s*)[\d.]+', r'\g<1>20')
    ])

    # Fire_Boss: HP 220, Dmg 12, Proj Dmg 18, Heat Wave 25
    patch_file("Fire_Boss.prefab", [
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?health:\s*)[\d.]+', r'\g<1>220'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::EnemyHealth[\s\S]*?contactDamage:\s*)[\d.]+', r'\g<1>12'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::FireBossProjectileAttack[\s\S]*?projectileDamage:\s*)[\d.]+', r'\g<1>18'),
        (r'(\s+m_EditorClassIdentifier:\s*Assembly-CSharp::FireBossHeatWave[\s\S]*?heatWaveDamage:\s*)[\d.]+', r'\g<1>25')
    ])

if __name__ == "__main__":
    main()
