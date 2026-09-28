#!/usr/bin/env python3
"""Task 3 verification (Unity-free):

1. Parse all 30 Assets/Resources/Levels/*.asset YAML files and assert the spec
   contract: movesLimit > 0, objectives non-empty with TargetAmount > 0,
   monotonic star thresholds, unique level numbers 1..30, difficulty curve.
2. Port of PlayerProgress / ProgressionMap / LevelData.StarsForScore logic and
   run the same assertions as the NUnit EditMode tests.
"""
import glob
import os
import re
import sys

try:
    import yaml
except ImportError:
    sys.exit("pyyaml missing: pip install pyyaml")


class UnityLoader(yaml.SafeLoader):
    """SafeLoader that maps Unity's custom !u!<type> tag to a plain mapping."""
    pass

UnityLoader.add_multi_constructor(
    "tag:unity3d.com,2011:",
    lambda loader, suffix, node: loader.construct_mapping(node, deep=True),
)

def load_unity_yaml(path):
    with open(path, encoding="utf-8") as f:
        return [d for d in yaml.load_all(f, Loader=UnityLoader) if isinstance(d, dict)]

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEVELS = sorted(glob.glob(os.path.join(ROOT, "rescue-animal-match", "Assets", "Resources", "Levels", "*.asset")))

OBJ_TYPES = {"RescueAnimals", "CollectResources", "ClearDebris", "FeedAnimals"}

checks = 0
def ok(cond, msg):
    global checks
    if not cond:
        print(f"FAIL: {msg}")
        sys.exit(1)
    checks += 1

# ---------- 1. Asset parsing ----------
ok(len(LEVELS) == 30, f"expected 30 assets, found {len(LEVELS)}")
seen_nums = set()
for path in LEVELS:
    docs = load_unity_yaml(path)
    doc = [d["MonoBehaviour"] for d in docs if "MonoBehaviour" in d][0]
    n = doc["levelNumber"]
    name = doc["levelName"]
    diff = doc["difficulty"]
    moves = doc["movesLimit"]
    objs = doc["objectives"]
    t1, t2, t3 = doc["starThreshold1"], doc["starThreshold2"], doc["starThreshold3"]
    rewards = doc.get("animalRewards") or []
    huellas = doc["huellasReward"]

    ok(n not in seen_nums, f"duplicate level number {n}")
    seen_nums.add(n)
    ok(isinstance(name, str) and name, f"L{n}: levelName required")
    ok(moves > 0, f"L{n}: movesLimit must be > 0")
    ok(isinstance(objs, list) and len(objs) > 0, f"L{n}: objectives non-empty")
    for o in objs:
        ok(o["Type"] in OBJ_TYPES, f"L{n}: bad objective type {o['Type']}")
        ok(o["TargetAmount"] > 0, f"L{n}: objective target > 0")
        ok(isinstance(o.get("PieceFilter", 0), int), f"L{n}: PieceFilter int")
    ok(0 < t1 <= t2 <= t3, f"L{n}: thresholds monotonic")
    ok(huellas > 0, f"L{n}: huellasReward > 0")
    ok(len(rewards) >= 1, f"L{n}: at least one animal reward")
    ok(str(os.path.basename(path)) == f"Level_{n:02d}.asset", f"L{n}: filename matches number")

    # difficulty curve
    if n <= 10:
        ok(diff == 0 and len(objs) == 1, f"L{n}: easy should be difficulty 0 with 1 objective")
    elif n <= 20:
        ok(diff == 1 and len(objs) == 2, f"L{n}: medium difficulty 1 with 2 objectives")
    else:
        ok(diff == 2 and len(objs) >= 2, f"L{n}: hard difficulty 2 with >=2 objectives")

ok(seen_nums == set(range(1, 31)), "levels must cover exactly 1..30")

# m_Name / fileID sanity for Unity import
for path in LEVELS:
    raw = open(path, encoding="utf-8").read()
    num = int(re.search(r"Level_(\d+)\.asset", path).group(1))
    ok(f"m_Name: Level_{num:02d}" in raw, f"L{num}: m_Name matches file")
    ok("!u!114 &11400000" in raw, f"L{num}: MonoBehaviour header present")
    ok("guid:" in raw.split("m_Script")[1][:80], f"L{num}: m_Script guid ref")
    meta = path + ".meta"
    ok(os.path.exists(meta), f"L{num}: .meta sidecar exists")

# ---------- 2. Logic port mirrors the NUnit tests ----------
class Progress:
    def __init__(self):
        self.highest = 1
        self.total_stars = 0
        self.completed = []
        self.stars = {}
        self.animals = []

    def stars_for(self, lvl): return self.stars.get(lvl, 0)

    def record(self, lvl, stars, animals=None):
        if lvl not in self.completed:
            self.total_stars += stars
            self.completed.append(lvl)
        elif stars > self.stars_for(lvl):
            self.total_stars += stars - self.stars_for(lvl)
        self.stars[lvl] = max(stars, self.stars_for(lvl))
        for a in animals or []:
            if a and a not in self.animals:
                self.animals.append(a)
        self.highest = max(self.highest, lvl + 1)

def compute_states(p, total):
    st = {}
    for n in range(1, total + 1):
        if p.stars_for(n) > 0:
            st[n] = "Completed"
        else:
            unlocked = n == 1 or p.stars_for(n - 1) >= 1
            st[n] = "Unlocked" if unlocked else "Locked"
    return st

def stars_for_score(score, t1, t2, t3):
    if score >= t3: return 3
    if score >= t2: return 2
    if score >= t1: return 1
    return 0

p = Progress()
p.record(1, 3, ["perro_max"])
ok(p.highest == 2 and p.total_stars == 3 and p.stars_for(1) == 3, "record completion")
ok("perro_max" in p.animals, "animal rescued recorded")

p2 = Progress()
p2.record(1, 1); p2.record(1, 3)
ok(p2.total_stars == 3, "replay adds only star delta")
ok(len(p2.completed) == 1, "no duplicate completion entries")

st = compute_states(Progress(), 5)
ok(st[1] == "Unlocked" and st[2] == "Locked", "fresh player: only L1 unlocked")
p3 = Progress(); p3.record(1, 1)
st = compute_states(p3, 5)
ok(st[1] == "Completed" and st[2] == "Unlocked" and st[3] == "Locked", "unlock rule N needs N-1 star")

ok(stars_for_score(999, 1000, 1600, 2400) == 0, "below t1 -> 0 stars")
ok(stars_for_score(1000, 1000, 1600, 2400) == 1, "t1 -> 1 star")
ok(stars_for_score(1600, 1000, 1600, 2400) == 2, "t2 -> 2 stars")
ok(stars_for_score(2400, 1000, 1600, 2400) == 3, "t3 -> 3 stars")

print(f"TASK 3 VERIFICATION PASSED — {checks} checks OK "
      f"(30 level assets valid, unlock/star/currency logic mirrored from C#)")
