using System.Collections.Generic;
using UnityEngine;

namespace RescueAnimalMatch.Board
{
    /// <summary>
    /// BoardManager responsibilities split out to keep files under 500 lines:
    /// piece removal, special-piece blast expansion, gravity and top refill.
    /// </summary>
    public partial class BoardManager
    {
        // ----------------------------------------------------------------
        // Removal
        // ----------------------------------------------------------------

        /// <summary>Destroys the GameObject (or just detaches it in EditMode) and frees the cell.</summary>
        private void RemovePiece(Piece piece)
        {
            if (piece == null) return;
            int x = piece.GridX, y = piece.GridY;
            if (InBounds(x, y) && pieces[x, y] == piece)
                pieces[x, y] = null;

            piece.MarkDestroyed();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(piece.gameObject);
                return;
            }
#endif
            AnimateDestroyAndRemove(piece);
        }

        private void AnimateDestroyAndRemove(Piece piece)
        {
            var go = piece.gameObject;
            piece.AnimateDestroy(() => { if (go != null) Object.Destroy(go); });
        }

        /// <summary>Fire-and-forget swap animation hook for the presentation layer.</summary>
        private void AnimateSwapPair(Piece a, Piece b)
        {
            if (a != null) a.AnimateSwap(WorldPositionForCell(a.GridX, a.GridY));
            if (b != null) b.AnimateSwap(WorldPositionForCell(b.GridX, b.GridY));
        }

        // ----------------------------------------------------------------
        // Special reactions (Power clears row/col, Bomb clears 3x3)
        // ----------------------------------------------------------------

        /// <summary>
        /// Transitively expands a set of cleared cells: when a cleared cell holds an
        /// existing special, its blast area is added too (chain reactions).
        /// </summary>
        private HashSet<Vector2IntLike> ExpandSpecialReactions(HashSet<Vector2IntLike> initial)
        {
            var result = new HashSet<Vector2IntLike>(initial);
            var queue = new Queue<Vector2IntLike>(initial);

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                var p = GetPieceAt(cell.x, cell.y);
                if (p == null || !p.IsSpecial) continue;

                foreach (var affected in BlastArea(p))
                {
                    if (!result.Contains(affected))
                    {
                        result.Add(affected);
                        queue.Enqueue(affected);
                    }
                }
            }
            return result;
        }

        /// <summary>All cells destroyed when the given special piece is triggered.</summary>
        public IEnumerable<Vector2IntLike> BlastArea(Piece p)
        {
            var cells = new List<Vector2IntLike>();
            if (p == null) return cells;

            switch (p.Special)
            {
                case SpecialType.Power:
                    // PowerPiece aims along the axis of the match that created it;
                    // default orientation clears both cross arms limited to row+column.
                    for (int x = 0; x < BoardWidth; x++) cells.Add(new Vector2IntLike(x, p.GridY));
                    for (int y = 0; y < BoardHeight; y++) cells.Add(new Vector2IntLike(p.GridX, y));
                    break;

                case SpecialType.Bomb:
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int x = p.GridX + dx, y = p.GridY + dy;
                            if (InBounds(x, y)) cells.Add(new Vector2IntLike(x, y));
                        }
                    break;

                case SpecialType.Wild:
                    cells.Add(new Vector2IntLike(p.GridX, p.GridY));
                    break;
            }
            return cells;
        }

        // ----------------------------------------------------------------
        // Gravity & refill
        // ----------------------------------------------------------------

        /// <summary>Drops every floating piece straight down into empty cells below it.</summary>
        public void ApplyGravity()
        {
            for (int x = 0; x < BoardWidth; x++)
            {
                int write = 0; // lowest free slot index
                for (int y = 0; y < BoardHeight; y++)
                {
                    var p = pieces[x, y];
                    if (p == null) continue;

                    if (write != y)
                    {
                        pieces[x, write] = p;
                        pieces[x, y] = null;
                        p.Initialize(p.Type, x, write, p.Special);
                        p.transform.position = WorldPositionForCell(x, write);
                        p.AnimateFall(Vector3.up * ((write - y) * cellSize.y));
                    }
                    write++;
                }
            }
        }

        /// <summary>Spawns fresh random pieces into every remaining empty cell (from the top).</summary>
        public void RefillBoard()
        {
            for (int x = 0; x < BoardWidth; x++)
            {
                for (int y = 0; y < BoardHeight; y++)
                {
                    if (pieces[x, y] != null) continue;
                    var piece = SpawnPiece(x, y, PickNonMatchingType(x, y));
                    // Entrance animation: start the piece visually above the board;
                    // AnimateFall then tweens it down to its final cell position.
                    Vector3 finalPos = WorldPositionForCell(x, y);
                    float dropDistance = (BoardHeight + 1 - y) * cellSize.y;
                    piece.transform.position = finalPos + Vector3.up * dropDistance;
                    piece.AnimateFall(Vector3.down * dropDistance);
                }
            }
        }

        /// <summary>True when at least one legal adjacent swap exists on the board.</summary>
        public bool HasValidMove()
        {
            for (int x = 0; x < BoardWidth; x++)
                for (int y = 0; y < BoardHeight; y++)
                {
                    if (TestSwapProducesMatch(x, y, x + 1, y)) return true;
                    if (TestSwapProducesMatch(x, y, x, y + 1)) return true;
                }
            return false;
        }

        private bool TestSwapProducesMatch(int x1, int y1, int x2, int y2)
        {
            if (!InBounds(x2, y2)) return false;
            var a = GetPieceAt(x1, y1);
            var b = GetPieceAt(x2, y2);
            if (a == null || b == null) return false;

            ExchangeCells(a, b);
            bool hasMatch = FindMatches().Count > 0;
            ExchangeCells(a, b);
            return hasMatch;
        }

        /// <summary>Shuffles all pieces until the board is match-free but has a valid move.</summary>
        public void ShuffleBoard()
        {
            var types = new List<PieceType>(BoardWidth * BoardHeight);
            for (int x = 0; x < BoardWidth; x++)
                for (int y = 0; y < BoardHeight; y++)
                    types.Add(GetPieceType(x, y));

            int guard = 0;
            do
            {
                for (int i = types.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (types[i], types[j]) = (types[j], types[i]);
                }
                int k = 0;
                for (int x = 0; x < BoardWidth; x++)
                    for (int y = 0; y < BoardHeight; y++)
                    {
                        var p = pieces[x, y];
                        if (p != null) p.Initialize(types[k++], x, y, p.Special);
                    }
            }
            while (guard++ < 50 && (FindMatches().Count > 0 || !HasValidMove()));
        }
    }
}
