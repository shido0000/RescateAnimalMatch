#!/usr/bin/env python3
"""Generate the 30 LevelData ScriptableObject assets in Assets/Resources/Levels/.

Field names/values must stay in sync with Assets/Scripts/Progression/LevelData.cs and
LevelObjective.cs (serialized private fields -> appear verbatim in the YAML).

Difficulty curve:
  L1-10  easy   (tutorial): 25..30 moves, 1 objective, low targets
  L11-20 medium: 20..24 moves, 2 objectives
  L21-30 hard   : 15..19 moves, 2-3 objectives, high targets
"""
import os

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(ROOT, "..", "rescue-animal-match", "Assets", "Resources", "Levels")

# metaFileIDs for m_Script references (stable guids of the .cs .meta files)
LEVELDATA_GUID = "f1a2b3c4d5e6f708192a3b4c5d6e7f01"   # LevelData.cs
LEVELOBJ_TYPE = "RescueAnimalMatch.Progression.LevelObjective"

PIECES = ["Paw", "Bone", "Heart", "Fish", "Star", "Leaf"]
OBJ_TYPES = ["RescueAnimals", "CollectResources", "ClearDebris", "FeedAnimals"]

NAMES_EASY = [
    "Primer Rescate", "Huellas en el Parque", "El Perro Perdido", "Gatitos del Refugio",
    "Ave Lastimada", "Conejos Libres", "El Gallinero", "Amigos del Lago",
    "Patitos Salvados", "La Cebra Curiosa",
]
NAMES_MED = [
    "Río Adentro", "Selva de Noche", "Montaña Nevada", "El Humedal",
    "Granja Antigua", "Bosque de Pinos", "Cañón Seco", "Isla Tortuga",
    "Llanura Vientos", "Ciudad Gris",
]
NAMES_HARD = [
    "Tormenta Final", "Rescate Nocturno", "Volcán Activo", "Desierto Rojo",
    "Fiebre del Rescate", "Puente Roto", "Minas Profundas", "Glaciar Frío",
    "Jungla Profunda", "Gran Rescate Animal",
]

ANIMALS = [
    "perro_max", "gata_luna", "loro_pepe", "conejo_nube", "tortuga_tula",
    "caballo_trueno", "patito_piu", "cerdito_bacon", "oveja_nube2", "gallina_rota",
]


def level_plan(n):
    """Return dict of fields for level n following the difficulty curve."""
    if n <= 10:
        diff = 0
        name = NAMES_EASY[n - 1]
        moves = 30 - n // 2              # 30..25
        objs = [(OBJ_TYPES[0], 10 + n * 2)]  # RescueAnimals 12..30
        t1 = 500 + n * 100
        huellas = 40 + n * 5
        animals = ANIMALS[(n - 1) % len(ANIMALS)]
    elif n <= 20:
        diff = 1
        name = NAMES_MED[n - 11]
        moves = 24 - (n - 11) // 3       # 24..20
        objs = [
            (OBJ_TYPES[(n) % 4], 20 + n),          # primary
            (OBJ_TYPES[(n + 2) % 4], 12 + n // 2), # secondary
        ]
        t1 = 1500 + (n - 10) * 150
        huellas = 90 + (n - 10) * 8
        animals = ANIMALS[(n - 1) % len(ANIMALS)] + "," + ANIMALS[n % len(ANIMALS)]
    else:
        diff = 2
        name = NAMES_HARD[n - 21]
        moves = 19 - (n - 21) // 4       # 19..16 (min 15 at L30)
        if n == 30:
            moves = 15
        objs = [
            (OBJ_TYPES[n % 4], 35 + n),
            (OBJ_TYPES[(n + 1) % 4], 25 + n // 2),
            (OBJ_TYPES[(n + 3) % 4], 15 + n // 3),
        ]
        t1 = 3000 + (n - 20) * 250
        huellas = 170 + (n - 20) * 12
        animals = ANIMALS[n % len(ANIMALS)]

    return {
        "num": n, "name": name, "diff": diff, "moves": moves, "objs": objs,
        "t1": t1, "huellas": huellas, "animals": animals,
    }


def yaml_for(plan):
    n = plan["num"]
    file_id = 11400000 + n  # unique fileID per asset
    obj_entries = []
    for i, (otype, amount) in enumerate(plan["objs"]):
        piece_filter = 1 + ((i + n) % 6) if otype == "CollectResources" else 0
        obj_entries.append(
            f"    - Type: {otype}\n"
            f"      TargetAmount: {amount}\n"
            f"      PieceFilter: {piece_filter}"
        )
    animals = plan["animals"].split(",")
    animal_lines = "\n".join(f"  - {a.strip()}" for a in animals)
    t1 = plan["t1"]
    t2 = int(t1 * 1.6)
    t3 = int(t1 * 2.4)
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {LEVELDATA_GUID}, type: 3}}
  m_Name: Level_{n:02d}
  m_EditorClassIdentifier: 
  levelNumber: {n}
  levelName: {plan['name']}
  difficulty: {plan['diff']}
  movesLimit: {plan['moves']}
  objectives:
{chr(10).join(obj_entries)}
  starThreshold1: {t1}
  starThreshold2: {t2}
  starThreshold3: {t3}
  animalRewards:
{animal_lines}
  huellasReward: {plan['huellas']}
"""


def meta_for(n):
    return f"""fileFormatVersion: 2
guid: {'abcdef0123456789abcdef0123456789'[:32-n%9]}{n:02d}level{n:02d}00000000000000
ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 2
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    plans = [level_plan(n) for n in range(1, 31)]
    for p in plans:
        n = p["num"]
        path = os.path.join(OUT_DIR, f"Level_{n:02d}.asset")
        with open(path, "w", encoding="utf-8") as f:
            f.write(yaml_for(p))
        # deterministic fake guid meta (Unity regenerates on import)
        meta = (
            "fileFormatVersion: 2\n"
            f"guid: {abs(hash(path)) % (16**32):032x}\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 11400000\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )
        with open(path + ".meta", "w", encoding="utf-8") as f:
            f.write(meta)
    print(f"Wrote {len(plans)} level assets to {OUT_DIR}")
    # sanity: all valid
    for p in plans:
        assert p["moves"] > 0 and len(p["objs"]) > 0, p


if __name__ == "__main__":
    main()
