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
        (r'(?<=health: )[\d.]+', r'45'),
        (r'(?<=baseSpeed: )[\d.]+', r'1.8'),
        (r'(?<=moveSpeed: )[\d.]+', r'1.8'), # moveSpeed is in EnemyMovement
        (r'(?<=contactDamage: )[\d.]+', r'15')
    ])

    # WaterEnemy: HP 30, Dmg 10, Speed 2.5
    patch_file("WaterEnemy.prefab", [
        (r'(?<=health: )[\d.]+', r'30'),
        (r'(?<=baseSpeed: )[\d.]+', r'2.5'),
        (r'(?<=moveSpeed: )[\d.]+', r'2.5'),
        (r'(?<=contactDamage: )[\d.]+', r'10')
    ])

    # FireEnemy: HP 20, Dmg 18, Speed 3.2
    patch_file("FireEnemy.prefab", [
        (r'(?<=health: )[\d.]+', r'20'),
        (r'(?<=baseSpeed: )[\d.]+', r'3.2'),
        (r'(?<=moveSpeed: )[\d.]+', r'3.2'),
        (r'(?<=contactDamage: )[\d.]+', r'18')
    ])

if __name__ == "__main__":
    main()
