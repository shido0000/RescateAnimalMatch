using System.Collections.Generic;

namespace RescueAnimalMatch.Board
{
    /// <summary>
    /// Abstraction over the piece grid so <see cref="MatchDetector"/> stays pure,
    /// side-effect free and testable without instantiating Unity objects.
    /// </summary>
    public interface IBoardSource
    {
        int Width { get; }
        int Height { get; }

        /// <summary>Returns PieceType.None for empty cells. Out-of-range returns None.</summary>
        PieceType GetPieceType(int x, int y);

        /// <summary>True when the cell holds a Wild special (matches any color).</summary>
        bool IsWild(int x, int y);
    }

    /// <summary>
    /// Static match-detection utility. All methods are pure functions of the board state:
    /// they never mutate the board, never destroy pieces, never spawn specials.
    /// </summary>
    public static class MatchDetector
    {
        public const int MinMatchLength = 3;

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        /// <summary>Finds every horizontal run of >= 3 equal-colored cells.</summary>
        public static List<MatchGroup> DetectHorizontal(IBoardSource board)
        {
            var groups = new List<MatchGroup>();
            if (board == null) return groups;

            for (int y = 0; y < board.Height; y++)
            {
                int runStart = 0;
                for (int x = 1; x <= board.Width; x++)
                {
                    bool sameRun = x < board.Width && SharesColor(board, x - 1, x, y, y, axisIsHorizontal: true);
                    if (sameRun) continue;

                    int runLength = x - runStart;
                    if (runLength >= MinMatchLength && RunIsValid(board, runStart, y, runLength, isHorizontal: true))
                        groups.Add(BuildLineGroup(board, runStart, y, runLength, isHorizontal: true));

                    runStart = x;
                }
            }
            return groups;
        }

        /// <summary>Finds every vertical run of >= 3 equal-colored cells.</summary>
        public static List<MatchGroup> DetectVertical(IBoardSource board)
        {
            var groups = new List<MatchGroup>();
            if (board == null) return groups;

            for (int x = 0; x < board.Width; x++)
            {
                int runStart = 0;
                for (int y = 1; y <= board.Height; y++)
                {
                    bool sameRun = y < board.Height && SharesColor(board, x, x, y - 1, y, axisIsHorizontal: false);
                    if (sameRun) continue;

                    int runLength = y - runStart;
                    if (runLength >= MinMatchLength && RunIsValid(board, x, runStart, runLength, isHorizontal: false))
                        groups.Add(BuildLineGroup(board, x, runStart, runLength, isHorizontal: false));

                    runStart = y;
                }
            }
            return groups;
        }

        /// <summary>
        /// Full detection pipeline: horizontal + vertical runs merged into logical groups.
        /// Overlapping H/V runs become L/T cross shapes. Adds MaxLineLength / Direction /
        /// IsCrossShape metadata used to derive special pieces.
        /// </summary>
        public static List<MatchGroup> DetectShapes(IBoardSource board)
        {
            var horizontal = DetectHorizontal(board);
            var vertical = DetectVertical(board);

            var merged = new List<MatchGroup>();
            var consumedV = new HashSet<int>();

            foreach (var h in horizontal)
            {
                for (int i = 0; i < vertical.Count; i++)
                {
                    if (consumedV.Contains(i)) continue;
                    if (SharesCell(h, vertical[i]))
                    {
                        consumedV.Add(i);
                        h.MergeWith(vertical[i]);
                        h.IsCrossShape = true; // joined an H run and a V run -> L or T
                        break;
                    }
                }
                merged.Add(h);
            }

            for (int i = 0; i < vertical.Count; i++)
                if (!consumedV.Contains(i))
                    merged.Add(vertical[i]);

            // Fill line metadata on groups that skipped merging.
            foreach (var g in merged)
                RecomputeLineMetadata(g);

            return merged;
        }

        /// <summary>Convenience: all matches with special resolution already computed.</summary>
        public static List<MatchGroup> DetectAll(IBoardSource board) => DetectShapes(board);

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static bool SharesColor(IBoardSource board, int x1, int x2, int y1, int y2, bool axisIsHorizontal)
        {
            var t1 = board.GetPieceType(x1, y1);
            var t2 = board.GetPieceType(x2, y2);
            if (t1 == PieceType.None || t2 == PieceType.None) return false;

            bool w1 = board.IsWild(x1, y1);
            bool w2 = board.IsWild(x2, y2);
            if (w1 || w2) return true;               // wild bridges any neighbor
            return t1 == t2;
        }

        /// <summary>A run counts only if at least one non-wild anchor color exists.</summary>
        private static bool RunIsValid(IBoardSource board, int start, int fixedAxis, int length, bool isHorizontal)
        {
            for (int i = 0; i < length; i++)
            {
                int x = isHorizontal ? start + i : fixedAxis;
                int y = isHorizontal ? fixedAxis : start + i;
                if (!board.IsWild(x, y) && board.GetPieceType(x, y) != PieceType.None)
                    return true;
            }
            return false; // all-wild run: no color to score
        }

        private static MatchGroup BuildLineGroup(IBoardSource board, int start, int fixedAxis, int length, bool isHorizontal)
        {
            var group = new MatchGroup();
            group.Direction = isHorizontal ? MatchDirection.Horizontal : MatchDirection.Vertical;
            group.MaxLineLength = length;

            PieceType color = PieceType.None;
            for (int i = 0; i < length; i++)
            {
                int x = isHorizontal ? start + i : fixedAxis;
                int y = isHorizontal ? fixedAxis : start + i;
                group.Add(new Vector2IntLike(x, y));
                if (color == PieceType.None && !board.IsWild(x, y))
                    color = board.GetPieceType(x, y);
            }
            group.Color = color;
            return group;
        }

        private static bool SharesCell(MatchGroup a, MatchGroup b)
        {
            foreach (var c in a.Cells)
                if (b.Contains(c)) return true;
            return false;
        }

        /// <summary>Recomputes MaxLineLength/Direction from the actual cell set.</summary>
        private static void RecomputeLineMetadata(MatchGroup group)
        {
            int bestLen = 0;
            var bestDir = MatchDirection.None;

            var byRow = GroupCellsByAxis(group, byColumn: false);
            foreach (var kv in byRow)
            {
                int len = LongestConsecutiveRun(kv.Value);
                if (len > bestLen) { bestLen = len; bestDir = MatchDirection.Horizontal; }
            }

            var byCol = GroupCellsByAxis(group, byColumn: true);
            foreach (var kv in byCol)
            {
                int len = LongestConsecutiveRun(kv.Value);
                if (len > bestLen) { bestLen = len; bestDir = MatchDirection.Vertical; }
            }

            if (bestLen > group.MaxLineLength)
            {
                group.MaxLineLength = bestLen;
                group.Direction = bestDir;
            }
        }

        private static Dictionary<int, List<int>> GroupCellsByAxis(MatchGroup group, bool byColumn)
        {
            var map = new Dictionary<int, List<int>>();
            foreach (var c in group.Cells)
            {
                int key = byColumn ? c.x : c.y;
                int val = byColumn ? c.y : c.x;
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    map[key] = list;
                }
                list.Add(val);
            }
            return map;
        }

        private static int LongestConsecutiveRun(List<int> values)
        {
            values.Sort();
            int best = 1, cur = 1;
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] == values[i - 1]) continue;
                cur = values[i] == values[i - 1] + 1 ? cur + 1 : 1;
                if (cur > best) best = cur;
            }
            return values.Count == 0 ? 0 : best;
        }
    }
}
