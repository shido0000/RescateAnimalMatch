using System.Collections.Generic;

namespace RescueAnimalMatch.Board
{
    /// <summary>Orientation of a linear match, used to aim PowerPieces.</summary>
    public enum MatchDirection
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2
    }

    /// <summary>
    /// A set of board cells that form one logical match (>= 3 pieces).
    /// Produced by <see cref="MatchDetector"/> with zero side effects.
    /// </summary>
    public class MatchGroup
    {
        /// <summary>Unique cell coordinates covered by this group.</summary>
        public List<Vector2IntLike> Cells { get; private set; } = new List<Vector2IntLike>();

        /// <summary>The color all matched cells share (None if wild-only).</summary>
        public PieceType Color { get; set; } = PieceType.None;

        /// <summary>Longest straight run found inside the group.</summary>
        public int MaxLineLength { get; set; }

        /// <summary>Orientation of the longest straight run.</summary>
        public MatchDirection Direction { get; set; } = MatchDirection.None;

        /// <summary>True when the group contains both a horizontal and vertical run >= 3 (L/T shape).</summary>
        public bool IsCrossShape { get; set; }

        /// <summary>Swap cell that triggered this group, if known (specials spawn here).</summary>
        public Vector2IntLike OriginCell { get; set; }

        public MatchGroup() { }

        public MatchGroup(IEnumerable<Vector2IntLike> cells, PieceType color)
        {
            foreach (var c in cells)
                if (!Contains(c)) Cells.Add(c);
            Color = color;
        }

        public void Add(Vector2IntLike cell)
        {
            if (!Contains(cell)) Cells.Add(cell);
        }

        public bool Contains(Vector2IntLike cell)
        {
            for (int i = 0; i < Cells.Count; i++)
                if (Cells[i].x == cell.x && Cells[i].y == cell.y) return true;
            return false;
        }

        public int Count => Cells.Count;

        /// <summary>
        /// Special piece derived from this group's geometry:
        /// 5+ in a line -> Wild, L/T -> Bomb, 4 in a line -> Power, else none.
        /// </summary>
        public SpecialType ResolveSpecial()
        {
            if (MaxLineLength >= 5) return SpecialType.Wild;
            if (IsCrossShape) return SpecialType.Bomb;
            if (MaxLineLength == 4) return SpecialType.Power;
            return SpecialType.None;
        }

        /// <summary>Merges another group into this one (used to join H+V runs into L/T shapes).</summary>
        public void MergeWith(MatchGroup other)
        {
            foreach (var c in other.Cells) Add(c);
            if (other.MaxLineLength > MaxLineLength)
            {
                MaxLineLength = other.MaxLineLength;
                Direction = other.Direction;
            }
            if (other.IsCrossShape) IsCrossShape = true;
        }
    }

    /// <summary>
    /// Minimal serializable 2D int coordinate usable from pure C# code and tests
    /// (mirrors UnityEngine.Vector2Int semantics without forcing a Unity ref here).
    /// </summary>
    public struct Vector2IntLike
    {
        public int x;
        public int y;

        public Vector2IntLike(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override string ToString() => $"({x},{y})";

        public override bool Equals(object obj) =>
            obj is Vector2IntLike o && o.x == x && o.y == y;

        public override int GetHashCode() => unchecked(x * 73856093 ^ y * 19349663);

        public static bool operator ==(Vector2IntLike a, Vector2IntLike b) => a.Equals(b);
        public static bool operator !=(Vector2IntLike a, Vector2IntLike b) => !a.Equals(b);
    }
}
