using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RescueAnimalMatch.Board
{
    /// <summary>
    /// Owns the 8x8 piece grid: initialization, swapping, match resolution,
    /// gravity, refill and the cascade loop. Implements IBoardSource so the pure
    /// MatchDetector can read it (and so EditMode tests can drive it directly).
    /// </summary>
    public partial class BoardManager : MonoBehaviour, IBoardSource
    {
        public const int BoardWidth = 8;
        public const int BoardHeight = 8;
        public const int PieceTypeCount = 6; // Paw..Leaf

        [Header("Grid")]
        [SerializeField] private Vector2 cellSize = Vector2.one;
        [SerializeField] private Vector3 boardOrigin = Vector3.zero;
        [SerializeField] private GameObject piecePrefab;
        [SerializeField] private Transform pieceRoot;

        [Header("Tuning")]
        [SerializeField] private int maxCascadeSteps = 50;
        [SerializeField] private int seed = 0; // 0 = time-based

        // x, y lookup. y = 0 is the BOTTOM row (gravity direction is -y).
        private readonly Piece[,] pieces = new Piece[BoardWidth, BoardHeight];

        private System.Random rng;
        private bool isResolving;

        // ----------------------------------------------------------------
        // Events
        // ----------------------------------------------------------------
        public event Action<List<MatchGroup>> OnMatchesFound;
        public event Action<int /*cascadeStep*/, int /*piecesDestroyed*/> OnCascadeStep;
        public event Action OnBoardStable;
        public event Action<PieceType, int, int> OnSpecialCreated;

        public static int Width => BoardWidth;
        public static int Height => BoardHeight;
        int IBoardSource.Width => BoardWidth;
        int IBoardSource.Height => BoardHeight;
        public bool IsResolving => isResolving;
        public Vector2 CellSize => cellSize;

        // ----------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------
        private void Awake()
        {
            InitializeRng();
            if (!IsInitialized) GenerateBoard();
        }

        private void InitializeRng()
        {
            rng = seed != 0 ? new System.Random(seed) : new System.Random(Environment.TickCount);
        }

        /// <summary>Deterministic RNG for tests / replays.</summary>
        public void SetSeed(int newSeed)
        {
            seed = newSeed;
            rng = new System.Random(newSeed == 0 ? Environment.TickCount : newSeed);
        }

        public bool IsInitialized
        {
            get
            {
                for (int x = 0; x < BoardWidth; x++)
                    for (int y = 0; y < BoardHeight; y++)
                        if (pieces[x, y] == null) return false;
                return true;
            }
        }

        /// <summary>Fills the board with random pieces that contain no initial matches.</summary>
        public void GenerateBoard()
        {
            for (int x = 0; x < BoardWidth; x++)
                for (int y = 0; y < BoardHeight; y++)
                    SpawnPiece(x, y, PickNonMatchingType(x, y));

            // Safety: extremely rarely a wild-free generation can still cascade-match.
            int guard = 0;
            while (FindMatches().Count > 0 && guard++ < 100)
                RegenerateAllPieces();
        }

        /// <summary>Test hook: fills the board from an explicit type matrix [x, y].</summary>
        public void SetupBoard(PieceType[,] types)
        {
            if (types == null || types.GetLength(0) != BoardWidth || types.GetLength(1) != BoardHeight)
                throw new ArgumentException($"Expected {BoardWidth}x{BoardHeight} matrix.");

            for (int x = 0; x < BoardWidth; x++)
                for (int y = 0; y < BoardHeight; y++)
                    SpawnPiece(x, y, types[x, y]);
        }

        private void RegenerateAllPieces()
        {
            for (int x = 0; x < BoardWidth; x++)
                for (int y = 0; y < BoardHeight; y++)
                    if (pieces[x, y] != null)
                        DestroyImmediate(pieces[x, y].gameObject);
            for (int x = 0; x < BoardWidth; x++)
                for (int y = 0; y < BoardHeight; y++)
                    SpawnPiece(x, y, PickNonMatchingType(x, y));
        }

        // ----------------------------------------------------------------
        // IBoardSource
        // ----------------------------------------------------------------
        public PieceType GetPieceType(int x, int y)
        {
            if (!InBounds(x, y)) return PieceType.None;
            var p = pieces[x, y];
            return p != null ? p.Type : PieceType.None;
        }

        public bool IsWild(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var p = pieces[x, y];
            return p != null && p.IsSpecial && p.Special == SpecialType.Wild;
        }

        public Piece GetPieceAt(int x, int y)
        {
            if (!InBounds(x, y)) return null;
            return pieces[x, y];
        }

        public static bool InBounds(int x, int y) =>
            x >= 0 && x < BoardWidth && y >= 0 && y < BoardHeight;

        // ----------------------------------------------------------------
        // Swapping
        // ----------------------------------------------------------------

        /// <summary>
        /// Swaps two pieces if they are orthogonally adjacent. Returns false (and
        /// reverts) when the swap produces no match, per classic match-3 rules.
        /// </summary>
        public bool SwapPieces(int x1, int y1, int x2, int y2)
        {
            if (isResolving) return false;
            if (!AreAdjacent(x1, y1, x2, y2)) return false;

            var a = GetPieceAt(x1, y1);
            var b = GetPieceAt(x2, y2);
            if (a == null || b == null) return false;

            ExchangeCells(a, b);

            // Wild specials can be swapped with any neighbor to trigger a color clear.
            bool wildSwap = a.Special == SpecialType.Wild || b.Special == SpecialType.Wild;

            var matches = FindMatches();
            if (matches.Count == 0 && !wildSwap)
            {
                ExchangeCells(a, b); // revert invalid move
                return false;
            }

            if (wildSwap && matches.Count == 0)
                matches = BuildWildClearGroups(a, b);

            AnimateSwapPair(a, b);
            ResolveBoard(matches, origin: new Vector2IntLike(x2, y2));
            return true;
        }

        public static bool AreAdjacent(int x1, int y1, int x2, int y2)
        {
            int dx = Mathf.Abs(x1 - x2);
            int dy = Mathf.Abs(y1 - y2);
            return dx + dy == 1; // orthogonal only, no diagonals
        }

        private void ExchangeCells(Piece a, Piece b)
        {
            int ax = a.GridX, ay = a.GridY;
            pieces[a.GridX, a.GridY] = b;
            pieces[b.GridX, b.GridY] = a;
            a.Initialize(a.Type, b.GridX, b.GridY, a.Special);
            b.Initialize(b.Type, ax, ay, b.Special);
        }

        // ----------------------------------------------------------------
        // Match finding & resolution
        // ----------------------------------------------------------------

        /// <summary>Pure query: current matches without touching the board.</summary>
        public List<MatchGroup> FindMatches() => MatchDetector.DetectShapes(this);

        /// <summary>
        /// Removes matched pieces, creates special pieces where geometry demands it,
        /// applies gravity and refills the top. Runs the full cascade loop synchronously
        /// (animations run in parallel via coroutines when a prefab/scene exists).
        /// </summary>
        public int ResolveMatches(List<MatchGroup> matches)
        {
            int destroyed = 0;
            if (matches == null || matches.Count == 0) return 0;

            var cellsToClear = new HashSet<Vector2IntLike>();
            foreach (var m in matches)
                foreach (var c in m.Cells)
                    cellsToClear.Add(c);

            // Expand clears through chain reactions of pre-existing specials.
            cellsToClear = ExpandSpecialReactions(cellsToClear);

            // Decide where specials spawn BEFORE clearing.
            var specialSpawns = new Dictionary<Vector2IntLike, SpecialType>();
            foreach (var m in matches)
            {
                var sp = m.ResolveSpecial();
                if (sp == SpecialType.None) continue;
                var cell = m.Contains(m.OriginCell) ? m.OriginCell : m.Cells[0];
                if (!specialSpawns.ContainsKey(cell))
                    specialSpawns[cell] = sp;
            }

            foreach (var cell in cellsToClear)
            {
                var p = GetPieceAt(cell.x, cell.y);
                if (p == null) continue;
                destroyed++;
                RemovePiece(p);
            }

            foreach (var kv in specialSpawns)
            {
                if (InBounds(kv.Key.x, kv.Key.y) && GetPieceAt(kv.Key.x, kv.Key.y) == null)
                {
                    var piece = SpawnPiece(kv.Key.x, kv.Key.y, ColorOfFirstNeighbor(kv.Key), kv.Value);
                    OnSpecialCreated?.Invoke(piece.Type, kv.Key.x, kv.Key.y);
                }
            }

            ApplyGravity();
            RefillBoard();
            return destroyed;
        }

        /// <summary>Cascade loop: resolve -> refill -> re-check until stable.</summary>
        public void ResolveBoard(List<MatchGroup> initialMatches, Vector2IntLike origin)
        {
            isResolving = true;
            int step = 0;
            var current = initialMatches;

            while (step < maxCascadeSteps)
            {
                if (current == null || current.Count == 0) break;
                foreach (var m in current) m.OriginCell = origin;
                OnMatchesFound?.Invoke(current);

                int cleared = ResolveMatches(current);
                OnCascadeStep?.Invoke(step, cleared);
                step++;

                current = FindMatches(); // re-check after gravity+refill
            }

            isResolving = false;
            OnBoardStable?.Invoke();
        }

        /// <summary>Convenience entry point used by tests and the input layer.</summary>
        public int ResolveAndCascade()
        {
            int total = 0;
            int guard = 0;
            var matches = FindMatches();
            while (matches.Count > 0 && guard++ < maxCascadeSteps)
            {
                total += ResolveMatches(matches);
                matches = FindMatches();
            }
            return total;
        }

        // ----------------------------------------------------------------
        // Gravity, refill & piece removal (implemented in BoardManager.Refill.cs)
        // ----------------------------------------------------------------

        // ----------------------------------------------------------------
        // Spawning helpers
        // ----------------------------------------------------------------
        private Piece SpawnPiece(int x, int y, PieceType type,
                                 SpecialType special = SpecialType.None)
        {
            GameObject go = null;
            Piece piece = null;

            if (piecePrefab != null)
            {
                var parent = pieceRoot != null ? pieceRoot : transform;
                go = UnityEngine.Object.Instantiate(piecePrefab, parent);
                piece = go.GetComponent<Piece>();
            }

            if (piece == null)
            {
                // Headless / EditMode fallback: plain object without a prefab.
                go = new GameObject($"Piece_{x}_{y}");
                piece = go.AddComponent<Piece>();
            }

            piece.Initialize(type, x, y, special);
            go.transform.position = WorldPositionForCell(x, y);
            go.name = $"Piece_{type}_{x}_{y}";
            pieces[x, y] = piece;
            return piece;
        }

        public Vector3 WorldPositionForCell(int x, int y) =>
            boardOrigin + new Vector3(x * cellSize.x, y * cellSize.y, 0f);

        private PieceType PickNonMatchingType(int x, int y)
        {
            // Avoid creating an instant 3-in-line with already-placed left/down neighbors.
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var candidate = (PieceType)(rng.Next(PieceTypeCount) + 1);
                bool leftConflict = x >= 2 &&
                    GetPieceType(x - 1, y) == candidate && GetPieceType(x - 2, y) == candidate;
                bool downConflict = y >= 2 &&
                    GetPieceType(x, y - 1) == candidate && GetPieceType(x, y - 2) == candidate;
                if (!leftConflict && !downConflict) return candidate;
            }
            return (PieceType)(rng.Next(PieceTypeCount) + 1);
        }

        private PieceType ColorOfFirstNeighbor(Vector2IntLike cell)
        {
            Vector2IntLike[] dirs =
            {
                new Vector2IntLike(cell.x + 1, cell.y), new Vector2IntLike(cell.x - 1, cell.y),
                new Vector2IntLike(cell.x, cell.y + 1), new Vector2IntLike(cell.x, cell.y - 1)
            };
            foreach (var d in dirs)
            {
                var t = GetPieceType(d.x, d.y);
                if (t != PieceType.None) return t;
            }
            return (PieceType)(rng.Next(PieceTypeCount) + 1);
        }

        private List<MatchGroup> BuildWildClearGroups(Piece a, Piece b)
        {
            // Wild swapped with a normal piece: clear everything of that color.
            var color = a.Special == SpecialType.Wild ? b.Type : a.Type;
            var group = new MatchGroup { Color = color, MaxLineLength = 3, OriginCell = new Vector2IntLike(b.GridX, b.GridY) };
            for (int x = 0; x < BoardWidth; x++)
                for (int y = 0; y < BoardHeight; y++)
                    if (GetPieceType(x, y) == color)
                        group.Add(new Vector2IntLike(x, y));
            group.Add(new Vector2IntLike(a.GridX, a.GridY));
            return new List<MatchGroup> { group };
        }
    }
}
