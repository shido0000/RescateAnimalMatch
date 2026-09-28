#!/usr/bin/env python3
"""
Headless verification harness for the Task 2 match-3 engine.

The sandbox has no Unity/dotnet, so this script:
  1. Statically checks every Board .cs file (balanced braces/parens, namespaces,
     expected public API surface, <500 line rule).
  2. Re-implements MatchDetector + gravity/refill/cascade in Python as a faithful
     port and runs the same assertions as BoardManagerTests.cs against it,
     including randomized full-board cascade fuzzing.

Exit code 0 == all checks pass.
"""
import random
import re
import sys
from pathlib import Path

ROOT = Path("/workspace/rescue-animal-match")
BOARD_DIR = ROOT / "Assets" / "Scripts" / "Board"
TESTS = ROOT / "Assets" / "Tests" / "EditMode" / "Board" / "BoardManagerTests.cs"

failures = []
passed = []

def check(name, cond, detail=""):
    (passed if cond else failures).append((name, detail))

# ---------------------------------------------------------------------------
# 1. Static structural checks
# ---------------------------------------------------------------------------
FILES = {
    "Piece.cs": ["enum PieceType", "enum SpecialType", "class Piece : MonoBehaviour",
                 "AnimateSwap(", "AnimateDestroy(", "AnimateFall(",
                 "Paw", "Bone", "Heart", "Fish", "Star", "Leaf"],
    "MatchGroup.cs": ["class MatchGroup", "ResolveSpecial", "MergeWith",
                      "SpecialType.Wild", "SpecialType.Bomb", "SpecialType.Power"],
    "MatchDetector.cs": ["static class MatchDetector", "DetectHorizontal",
                         "DetectVertical", "DetectShapes", "IBoardSource"],
    "BoardManager.cs": ["partial class BoardManager : MonoBehaviour, IBoardSource",
                        "SwapPieces", "FindMatches", "ResolveMatches", "RefillBoard",
                        "ApplyGravity", "AreAdjacent", "ResolveBoard",
                        "BoardWidth = 8", "BoardHeight = 8", "PieceTypeCount = 6"],
    "BoardManager.Refill.cs": ["partial class BoardManager", "ApplyGravity",
                               "RefillBoard", "BlastArea", "ExpandSpecialReactions",
                               "HasValidMove"],
    "BoardInput.cs": ["class BoardInput : MonoBehaviour", "RaycastPiece",
                      "SwipeDirection", "SwapPieces", "OnPointerDown", "OnPointerUp"],
}

for fname, symbols in FILES.items():
    path = BOARD_DIR / fname
    if not path.exists():
        check(f"exists:{fname}", False, "missing file")
        continue
    src = path.read_text()
    check(f"exists:{fname}", True)
    stripped = re.sub(r'"(?:\\.|[^"\\])*"', '""', src)
    stripped = re.sub(r"'(?:\\.|[^'\\])*'", "''", stripped)
    stripped = re.sub(r"//[^\n]*", "", stripped)
    for open_c, close_c in [("{", "}"), ("(", ")"), ("[", "]")]:
        check(f"balance:{fname}:{open_c}",
              stripped.count(open_c) == stripped.count(close_c),
              f"{stripped.count(open_c)} vs {stripped.count(close_c)}")
    for sym in symbols:
        check(f"api:{fname}:{sym}", sym in src)
    lines = src.count("\n")
    check(f"size:{fname}<500lines", lines < 500, f"{lines} lines")

check("tests:BoardManagerTests.cs exists", TESTS.exists())
tsrc = TESTS.read_text() if TESTS.exists() else ""
for sym in ["FindMatches_KnownArrangement_ReturnsExpectedGroups", "ResolveMatches_",
            "Assert.AreEqual(2, matches.Count", "LShape", "FiveInLine", "FourInLine"]:
    check(f"test:{sym}", sym in tsrc)

asmdef_rt = (ROOT / "Assets" / "Scripts" / "RescueAnimalMatch.Runtime.asmdef").exists()
asmdef_te = (ROOT / "Assets" / "Tests" / "EditMode" /
             "RescueAnimalMatch.Tests.EditMode.asmdef").exists()
check("asmdef:runtime", asmdef_rt)
check("asmdef:editmode_tests", asmdef_te)

# ---------------------------------------------------------------------------
# 2. Faithful Python port of the engine + behavioral tests
# ---------------------------------------------------------------------------
W = H = 8
NONE, PAW, BONE, HEART, FISH, STAR, LEAF = 0, 1, 2, 3, 4, 5, 6
S_NONE, S_POWER, S_BOMB, S_WILD = 0, 1, 2, 3


class Cell:
    __slots__ = ("t", "s", "occupied")

    def __init__(self, t, s=S_NONE, occupied=True):
        self.t, self.s, self.occupied = t, s, occupied


def btype(board, x, y):
    if 0 <= x < W and 0 <= y < H and board[x][y].occupied:
        return board[x][y].t
    return NONE


def is_wild(board, x, y):
    return (0 <= x < W and 0 <= y < H and board[x][y].occupied
            and board[x][y].s == S_WILD)


def shares(board, x1, y1, x2, y2):
    t1, t2 = btype(board, x1, y1), btype(board, x2, y2)
    if t1 == NONE or t2 == NONE:
        return False
    if is_wild(board, x1, y1) or is_wild(board, x2, y2):
        return True  # wild bridges any neighbor
    return t1 == t2


class Group:
    def __init__(self):
        self.cells = []
        self.color = NONE
        self.max_line = 0
        self.direction = 0  # 1 horiz, 2 vert
        self.cross = False

    def add(self, cell):
        if cell not in self.cells:
            self.cells.append(cell)

    def merge(self, other):
        for c in other.cells:
            self.add(c)
        if other.max_line > self.max_line:
            self.max_line, self.direction = other.max_line, other.direction
        if other.cross:
            self.cross = True

    def resolve_special(self):
        if self.max_line >= 5:
            return S_WILD
        if self.cross:
            return S_BOMB
        if self.max_line == 4:
            return S_POWER
        return S_NONE


def _runs(board, horizontal):
    groups = []
    outer = range(H) if horizontal else range(W)
    for fixed in outer:
        start = 0
        for pos in range(1, (W if horizontal else H) + 1):
            a1 = (pos - 1, fixed) if horizontal else (fixed, pos - 1)
            a2 = (pos, fixed) if horizontal else (fixed, pos)
            if pos < (W if horizontal else H) and shares(board, *a1, *a2):
                continue
            length = pos - start
            cells = [((sx, fixed) if horizontal else (fixed, sx))
                     for sx in range(start, pos)]
            anchor = any(not is_wild(board, x, y) and btype(board, x, y) != NONE
                         for x, y in cells)
            if length >= 3 and anchor:
                g = Group()
                g.max_line = length
                g.direction = 1 if horizontal else 2
                for x, y in cells:
                    g.add((x, y))
                    if g.color == NONE and not is_wild(board, x, y):
                        g.color = btype(board, x, y)
                groups.append(g)
            start = pos
    return groups


def detect_horizontal(board):
    return _runs(board, horizontal=True)


def detect_vertical(board):
    return _runs(board, horizontal=False)


def longest_run(vals):
    vals = sorted(set(vals))
    if not vals:
        return 0
    best = cur = 1
    for i in range(1, len(vals)):
        cur = cur + 1 if vals[i] == vals[i - 1] + 1 else 1
        best = max(best, cur)
    return best


def detect_shapes(board):
    hs, vs = detect_horizontal(board), detect_vertical(board)
    consumed = set()
    merged = []
    for h in hs:
        for i, v in enumerate(vs):
            if i in consumed:
                continue
            if any(c in v.cells for c in h.cells):
                consumed.add(i)
                h.merge(v)
                h.cross = True
                break
        merged.append(h)
    for i, v in enumerate(vs):
        if i not in consumed:
            merged.append(v)
    for g in merged:  # RecomputeLineMetadata
        rows, cols = {}, {}
        for x, y in g.cells:
            rows.setdefault(y, []).append(x)
            cols.setdefault(x, []).append(y)
        bh = max((longest_run(v) for v in rows.values()), default=0)
        bv = max((longest_run(v) for v in cols.values()), default=0)
        best, d = (bh, 1) if bh >= bv else (bv, 2)
        if best > g.max_line:
            g.max_line, g.direction = best, d
    return merged


def stable_grid():
    return [[Cell(((x * 3 + y * 5) % 6) + 1) for y in range(H)] for x in range(W)]


def empty_grid():
    return [[Cell(NONE, occupied=False) for y in range(H)] for x in range(W)]


def blast_area(board, x, y):
    c = board[x][y]
    cells = []
    if not c.occupied:
        return cells
    if c.s == S_POWER:
        cells = [(i, y) for i in range(W)] + [(x, j) for j in range(H)]
    elif c.s == S_BOMB:
        cells = [(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)
                 if 0 <= x + dx < W and 0 <= y + dy < H]
    elif c.s == S_WILD:
        cells = [(x, y)]
    return cells


def expand_reactions(board, initial):
    result = set(initial)
    queue = list(initial)
    while queue:
        x, y = queue.pop(0)
        c = board[x][y]
        if not c.occupied or c.s == S_NONE:
            continue
        for affected in blast_area(board, x, y):
            if affected not in result:
                result.add(affected)
                queue.append(affected)
    return result


def pick_type(rng, board, x, y):
    for _ in range(20):
        cand = rng.randint(1, 6)
        left = x >= 2 and btype(board, x - 1, y) == cand and btype(board, x - 2, y) == cand
        down = y >= 2 and btype(board, x, y - 1) == cand and btype(board, x, y - 2) == cand
        if not left and not down:
            return cand
    return rng.randint(1, 6)


def apply_gravity(board):
    # Mirror of the C# implementation: iterate bottom->top, compacting pieces
    # into the lowest free slots (identity-preserving, like swapping references).
    for x in range(W):
        write = 0
        for y in range(H):
            if not board[x][y].occupied:
                continue
            if write != y:
                board[x][write] = board[x][y]
                board[x][y] = Cell(NONE, occupied=False)
            write += 1


def refill(board, rng):
    for x in range(W):
        for y in range(H):
            if not board[x][y].occupied:
                board[x][y] = Cell(pick_type(rng, board, x, y))


def resolve_matches(board, groups, origin=None, rng=None):
    rng = rng or random.Random(7)
    if not groups:
        return 0
    clear = set()
    for g in groups:
        clear.update(g.cells)
    clear = expand_reactions(board, clear)
    spawns = {}
    for g in groups:
        sp = g.resolve_special()
        if sp == S_NONE:
            continue
        cell = origin if (origin and origin in g.cells) else g.cells[0]
        spawns.setdefault(cell, sp)
    destroyed = 0
    for key in clear:
        x, y = key
        if board[x][y].occupied:
            board[x][y] = Cell(NONE, occupied=False)
            destroyed += 1
    for (x, y), sp in spawns.items():
        if not board[x][y].occupied:
            color = NONE
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < W and 0 <= ny < H and board[nx][ny].occupied:
                    color = board[nx][ny].t
                    break
            if color == NONE:
                color = rng.randint(1, 6)
            board[x][y] = Cell(color, sp, occupied=True)
    apply_gravity(board)
    refill(board, rng)
    return destroyed


def cascade_until_stable(board, rng=None):
    rng = rng or random.Random(99)
    steps = total = 0
    matches = detect_shapes(board)
    while matches and steps < 50:
        total += resolve_matches(board, matches, rng=rng)
        steps += 1
        matches = detect_shapes(board)
    return steps, total, len(matches)


def swap(board, x1, y1, x2, y2):
    board[x1][y1], board[x2][y2] = board[x2][y2], board[x1][y1]


def adjacent(x1, y1, x2, y2):
    return abs(x1 - x2) + abs(y1 - y2) == 1


def generate_board(seed):
    r = random.Random(seed)
    board = empty_grid()
    for x in range(W):
        for y in range(H):
            board[x][y] = Cell(pick_type(r, board, x, y))
    guard = 0
    while detect_shapes(board) and guard < 100:
        board = empty_grid()
        for x in range(W):
            for y in range(H):
                board[x][y] = Cell(pick_type(r, board, x, y))
        guard += 1
    return board


def has_valid_move(board):
    for x in range(W):
        for y in range(H):
            for nx, ny in ((x + 1, y), (x, y + 1)):
                if nx < W and ny < H:
                    swap(board, x, y, nx, ny)
                    ok = len(detect_shapes(board)) > 0
                    swap(board, x, y, nx, ny)
                    if ok:
                        return True
    return False


# ---- behavioral assertions (mirror of BoardManagerTests.cs) ----
b = empty_grid()
b[0][0] = b[1][0] = b[2][0] = Cell(HEART)
b[5][2] = b[5][3] = b[5][4] = Cell(PAW)
check("py:FindMatches_KnownArrangement==2", len(detect_shapes(b)) == 2,
      str(len(detect_shapes(b))))

b = empty_grid()
for x in range(1, 5):
    b[x][3] = Cell(BONE)
h, v = detect_horizontal(b), detect_vertical(b)
check("py:DetectHorizontal_4run", len(h) == 1 and h[0].max_line == 4
      and h[0].direction == 1 and h[0].color == BONE)
check("py:DetectVertical_none", len(v) == 0)
check("py:PowerFrom4", h[0].resolve_special() == S_POWER)

b = empty_grid()
for x in range(5):
    b[x][4] = Cell(HEART)
g = detect_shapes(b)[0]
check("py:WildFrom5", g.resolve_special() == S_WILD)

b = empty_grid()
b[0][0] = b[1][0] = b[2][0] = Cell(PAW)
b[0][1] = b[0][2] = Cell(PAW)
gs = detect_shapes(b)
check("py:LShape_single_bomb", len(gs) == 1 and gs[0].cross and
      gs[0].resolve_special() == S_BOMB and len(gs[0].cells) == 5,
      f"n={len(gs)}")

b = empty_grid()
for x in range(4):
    b[x][0] = Cell(BONE)
b[1][1] = b[1][2] = Cell(BONE)
gs = detect_shapes(b)
check("py:TShape_bomb", len(gs) == 1 and gs[0].resolve_special() == S_BOMB,
      f"special={gs[0].resolve_special() if gs else '-'}")

b = empty_grid()
b[0][0] = Cell(HEART)
b[1][0] = Cell(STAR, S_WILD)
b[2][0] = Cell(HEART)
h = detect_horizontal(b)
check("py:WildBridgesColors", len(h) == 1 and h[0].color == HEART, f"n={len(h)}")

b = empty_grid()
b[0][0] = b[1][0] = Cell(STAR)
check("py:TwoInRow_notMatch", len(detect_shapes(b)) == 0)

check("py:Adjacency", adjacent(2, 2, 3, 2) and adjacent(2, 2, 2, 3) and
      not adjacent(2, 2, 2, 2) and not adjacent(2, 2, 3, 3) and
      not adjacent(2, 2, 4, 2))

rng = random.Random(12345)
board = stable_grid()
board[2][0] = Cell(HEART)
board[4][0] = Cell(HEART)
board[3][0] = Cell(LEAF)
board[3][1] = Cell(HEART)
swap(board, 3, 1, 3, 0)
matches = detect_shapes(board)
accepted = len(matches) > 0
if not accepted:
    swap(board, 3, 1, 3, 0)
check("py:SwapAccepted_ValidMatch", accepted)
if accepted:
    cleared = resolve_matches(board, matches, origin=(3, 0), rng=rng)
    steps, total, leftover = cascade_until_stable(board, rng)
    full = all(board[x][y].occupied and board[x][y].t != NONE
               for x in range(W) for y in range(H))
    check("py:BoardFullAfterCascade", full)
    check("py:BoardStableAfterCascade", leftover == 0)
    check("py:CascadeCleared>=3", cleared >= 3, str(cleared))

board = empty_grid()
board[0][0] = board[0][1] = board[0][2] = Cell(PAW)
board[0][3] = Cell(BONE)
board[1][0] = Cell(LEAF)
matches = detect_shapes(board)
destroyed = resolve_matches(board, matches, rng=random.Random(3))
check("py:Destroyed3", destroyed == 3, str(destroyed))
check("py:Gravity_BoneAtBottom", board[0][0].occupied and board[0][0].t == BONE,
      f"cell(0,0) t={board[0][0].t}")

gen_ok = moves_ok = True
for seed in range(25):
    bd = generate_board(seed)
    if detect_shapes(bd):
        gen_ok = False
    if not has_valid_move(bd):
        moves_ok = False
check("py:GenerateBoard_MatchFree(25 seeds)", gen_ok)
check("py:GenerateBoard_HasValidMove(25 seeds)", moves_ok)

fuzz_fail = []
for seed in range(30):
    r = random.Random(1000 + seed)
    bd = generate_board(seed)
    found = None
    for x in range(W):
        for y in range(H):
            for nx, ny in ((x + 1, y), (x, y + 1)):
                if nx < W and ny < H:
                    swap(bd, x, y, nx, ny)
                    if detect_shapes(bd):
                        found = (x, y, nx, ny)
                    swap(bd, x, y, nx, ny)
                if found:
                    break
            if found:
                break
        if found:
            break
    if not found:
        continue
    x, y, nx, ny = found
    swap(bd, x, y, nx, ny)
    steps, total, leftover = cascade_until_stable(bd, r)
    full = all(c.occupied and c.t != NONE for col in bd for c in col)
    if leftover != 0 or not full or steps == 0:
        fuzz_fail.append((seed, steps, leftover, full))
check("py:Fuzz_30Swaps_StableFullBoards", not fuzz_fail, str(fuzz_fail[:3]))

bd = empty_grid()
bd[0][0] = bd[1][0] = bd[2][0] = Cell(HEART)
bd[1][1] = Cell(BONE, S_BOMB)   # existing bomb inside blast reach of match? (adjacent cell)
bd[7][7] = Cell(STAR)           # far untouched piece
groups = detect_shapes(bd)
cleared = resolve_matches(bd, groups, rng=random.Random(5))
# The match clears (0..2,0)+(1,1)? No: bomb at (1,1) is NOT part of the match group.
# Chain reaction only triggers when a special itself is inside the cleared set.
check("py:BombNotTriggeredWhenOutsideMatch", cleared == 3, f"cleared={cleared}")

# NOTE: a single-color run cannot contain a different-colored special piece;
# bombs are triggered when they sit inside a same-color match or chain.
bd2 = empty_grid()
for y in range(3):
    bd2[0][y] = Cell(PAW)
bd2[0][1] = Cell(PAW, S_BOMB)   # same-color bomb inside the vertical match
bd2[1][2] = Cell(FISH)          # diagonal neighbour inside the 3x3 blast
bd2[7][7] = Cell(STAR)          # far untouched piece
groups = detect_shapes(bd2)
blast_cells = set(expand_reactions(bd2, {c for g in groups for c in g.cells}))
check("py:BlastIncludesFishNeighbor", (1, 2) in blast_cells)
check("py:BlastExcludesFarCell", (7, 7) not in blast_cells)
cleared2 = resolve_matches(bd2, groups, rng=random.Random(5))
# Blast of bomb@(0,1) covers x0..1,y0..2 -> destroys FISH at (1,2) too.
check("py:BombChainExpandsClear", cleared2 >= 4, f"cleared={cleared2}")
check("py:BlastDestroyedNeighbor", not bd2[1][2].occupied or bd2[1][2].t != FISH,
      "fish should have been blasted away")
# (7,7) is a refill cell after gravity+refill; occupancy is the invariant that matters.
check("py:FarCellRefilled", bd2[7][7].occupied and bd2[7][7].t != NONE,
      f"far t={bd2[7][7].t} occ={bd2[7][7].occupied}")

print(f"PASSED: {len(passed)}")
if failures:
    print(f"FAILED: {len(failures)}")
    for name, detail in failures:
        print(f"  FAIL {name}  {detail}")
    sys.exit(1)
print("ALL CHECKS PASSED")
