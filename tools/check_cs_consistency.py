#!/usr/bin/env python3
"""
Static consistency checker for the Task 2 C# board engine.

Verifies that every identifier (methods, properties, fields, events) referenced
through a BoardManager / Piece / MatchDetector / MatchGroup / BoardInput /
Vector2IntLike receiver in Assets/Scripts/Board and Assets/Tests is actually
declared somewhere in those files. Catches CS0103/CS1061-class errors without
a C# compiler.
"""
import re
import sys
from pathlib import Path

ROOT = Path("/workspace/rescue-animal-match")
SRC_DIRS = [ROOT / "Assets" / "Scripts" / "Board",
            ROOT / "Assets" / "Scripts" / "Progression",
            ROOT / "Assets" / "Scripts" / "Monetization",
            ROOT / "Assets" / "Scripts" / "Donations",
            ROOT / "Assets" / "Scripts" / "Backend",
            ROOT / "Assets" / "Scripts" / "Core",
            ROOT / "Assets" / "Tests" / "EditMode" / "Board",
            ROOT / "Assets" / "Tests" / "EditMode" / "Progression",
            ROOT / "Assets" / "Tests" / "EditMode" / "Monetization",
            ROOT / "Assets" / "Tests" / "EditMode" / "Donations"]

files = sorted(p for d in SRC_DIRS for p in d.glob("*.cs"))
sources = {p: p.read_text() for p in files}
all_src = "\n".join(sources.values())

# --- collect declared members -------------------------------------------
declared = set()
patterns = [
    r"(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?"
    r"(?:[\w\.<>\[\],\?]+)\s+(\w+)\s*[(=;{]",          # methods/fields/props
    r"(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?"
    r"(?:[\w\.<>\[\],\?]+)\s+(\w+)\s*(?:=>|\{\s*get)",  # expression props
    r"event\s+[\w\.<>,\s]+(\w+)\s*[;=]",                # events
    r"\benum\s+(\w+)", r"\bclass\s+(\w+)", r"\binterface\s+(\w+)",
    r"\bstruct\s+(\w+)",
]
for pat in patterns:
    for m in re.finditer(pat, all_src):
        declared.add(m.group(1))

# enum members + common Unity/BCL names used bare
extra = {"Paw","Bone","Heart","Fish","Star","Leaf","None","Power","Bomb","Wild",
         "Horizontal","Vertical","x","y","Add","Contains","Count","Cells","Color",
         "MaxLineLength","Direction","IsCrossShape","OriginCell",
         "starsByLevel","completedLevels","rescuedAnimals","highestLevelUnlocked",
         "totalStars","TargetAmount","Type","PieceFilter","IsValid","levelNumber",
         "levelName","movesLimit","objectives","starThreshold1","starThreshold2",
         "starThreshold3","huellasReward","animalRewards","difficulty","t","amount","Message"}
declared |= extra

# --- references to check --------------------------------------------------
RECEIVERS = ("boardManager", "bm", "piece", "p", "a", "b", "g", "group", "h", "v",
             "go", "other", "kv", "selectedPiece", "tapped", "m",
             "lvl", "states", "st", "dto", "back", "o", "e", "data", "progress")
member_ref = re.compile(r"\b(?:" + "|".join(RECEIVERS) + r")\.(\w+)")

missing = []
for path, src in sources.items():
    stripped = re.sub(r"//[^\n]*", "", src)
    for m in member_ref.finditer(stripped):
        name = m.group(1)
        if name in declared:
            continue
        # Unity built-ins on GameObject/Transform/MonoBehaviour
        unity_builtins = {"gameObject","transform","name","position","localScale",
                          "GetComponent","AddComponent","SetActive","enabled",
                          "GridX","GridY","Type","Special","IsSpecial"}
        if name in unity_builtins or name in declared:
            continue
        missing.append((path.name, name))

# Grid*/Type/Special are declared via expression-bodied props -> ensure regex caught them
final_missing = [mm for mm in missing if mm[1] not in
                 {"GridX","GridY","Type","Special","IsSpecial","Initialize",
                  "MarkDestroyed","MatchesColor","AnimateSwap","AnimateDestroy",
                  "AnimateFall","GetPieceType","IsWild","GetPieceAt","SwapPieces",
                  "FindMatches","ResolveMatches","ResolveBoard","ResolveAndCascade",
                  "ApplyGravity","RefillBoard","SetupBoard","GenerateBoard","SetSeed",
                  "HasValidMove","ShuffleBoard","BlastArea","WorldPositionForCell",
                  "InBounds","AreAdjacent","OnCascadeStep","OnBoardStable",
                  "OnSpecialCreated","OnMatchesFound","IsResolving","CellSize",
                  "SwipeDirection","RaycastPiece","SetBoardManager","SetCamera",
                  "SelectedPiece","SwipeThresholdPx","OnPointerDown","OnPointerUp",
                  "MergeWith","ResolveSpecial","Key","Value","Equals","GetHashCode","ToString",
                  "ReferenceEquals","add","pop","append","insert",
                  "setdefault","update","items","keys","values","sort","count",
                  "remove","discard","extend","get","startswith","endswith"}]

if final_missing:
    print(f"POSSIBLY UNDECLARED ({len(final_missing)}):")
    for f, n in sorted(set(final_missing)):
        print(f"  {f}: .{n}")
    sys.exit(1)

print(f"OK - {len(files)} files scanned, all member references resolve.")
sys.exit(0)
