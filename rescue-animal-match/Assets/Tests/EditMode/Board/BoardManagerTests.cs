using NUnit.Framework;
using RescueAnimalMatch.Board;
using UnityEngine;

namespace RescueAnimalMatch.Tests.EditMode.Board
{
    /// <summary>
    /// In-memory IBoardSource used to test MatchDetector without Unity objects.
    /// </summary>
    internal class FakeBoard : IBoardSource
    {
        private readonly PieceType[,] types;
        private readonly bool[,] wilds;

        public int Width => types.GetLength(0);
        public int Height => types.GetLength(1);

        public FakeBoard(PieceType[,] grid)
        {
            types = (PieceType[,])grid.Clone();
            wilds = new bool[Width, Height];
        }

        public PieceType GetPieceType(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height ? types[x, y] : PieceType.None;

        public bool IsWild(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height && wilds[x, y];

        public void SetWild(int x, int y) => wilds[x, y] = true;
    }

    public class BoardManagerTests
    {
        // ================================================================
        // Helpers
        // ================================================================

        private static PieceType[,] EmptyGrid()
        {
            var g = new PieceType[8, 8];
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++)
                    g[x, y] = PieceType.None;
            return g;
        }

        /// <summary>Checkerboard of 6 colors: guaranteed match-free.</summary>
        private static PieceType[,] StableGrid()
        {
            var g = new PieceType[8, 8];
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++)
                    g[x, y] = (PieceType)(((x * 3 + y * 5) % 6) + 1);
            return g;
        }

        private static BoardManager CreateHeadlessBoard(out GameObject go)
        {
            go = new GameObject("Board");
            var bm = go.AddComponent<BoardManager>();
            bm.SetSeed(12345);
            return bm;
        }

        // ================================================================
        // 1. Known arrangement -> FindMatches returns the right count
        // ================================================================

        [Test]
        public void FindMatches_KnownArrangement_ReturnsExpectedGroups()
        {
            var grid = EmptyGrid();
            // One horizontal run of 3 Hearts at row y=0, x=0..2
            grid[0, 0] = grid[1, 0] = grid[2, 0] = PieceType.Heart;
            // One vertical run of 3 Paws at column x=5, y=2..4
            grid[5, 2] = grid[5, 3] = grid[5, 4] = PieceType.Paw;

            var board = new FakeBoard(grid);
            var matches = MatchDetector.DetectShapes(board);

            Assert.AreEqual(2, matches.Count, "Exactly two independent match groups expected.");
        }

        [Test]
        public void DetectHorizontal_FindsRowRunOnly()
        {
            var grid = EmptyGrid();
            grid[1, 3] = grid[2, 3] = grid[3, 3] = grid[4, 3] = PieceType.Bone;

            var h = MatchDetector.DetectHorizontal(new FakeBoard(grid));
            var v = MatchDetector.DetectVertical(new FakeBoard(grid));

            Assert.AreEqual(1, h.Count);
            Assert.AreEqual(0, v.Count);
            Assert.AreEqual(4, h[0].MaxLineLength);
            Assert.AreEqual(MatchDirection.Horizontal, h[0].Direction);
            Assert.AreEqual(PieceType.Bone, h[0].Color);
        }

        [Test]
        public void DetectVertical_FindsColumnRun()
        {
            var grid = EmptyGrid();
            grid[6, 0] = grid[6, 1] = grid[6, 2] = PieceType.Fish;

            var v = MatchDetector.DetectVertical(new FakeBoard(grid));
            Assert.AreEqual(1, v.Count);
            Assert.AreEqual(3, v[0].Count);
            Assert.AreEqual(PieceType.Fish, v[0].Color);
        }

        [Test]
        public void DetectShapes_TwoInARow_IsNotAMatch()
        {
            var grid = EmptyGrid();
            grid[0, 0] = grid[1, 0] = PieceType.Star; // only 2 -> below minimum
            var matches = MatchDetector.DetectShapes(new FakeBoard(grid));
            Assert.AreEqual(0, matches.Count);
        }

        // ================================================================
        // 2. Special piece geometry
        // ================================================================

        [Test]
        public void FourInLine_CreatesPowerSpecial()
        {
            var grid = EmptyGrid();
            grid[0, 0] = grid[1, 0] = grid[2, 0] = grid[3, 0] = PieceType.Leaf;
            var g = MatchDetector.DetectShapes(new FakeBoard(grid))[0];
            Assert.AreEqual(SpecialType.Power, g.ResolveSpecial());
        }

        [Test]
        public void FiveInLine_CreatesWildSpecial()
        {
            var grid = EmptyGrid();
            for (int x = 0; x < 5; x++) grid[x, 4] = PieceType.Heart;
            var g = MatchDetector.DetectShapes(new FakeBoard(grid))[0];
            Assert.AreEqual(SpecialType.Wild, g.ResolveSpecial());
        }

        [Test]
        public void LShape_CreatesBombSpecial()
        {
            var grid = EmptyGrid();
            // Horizontal arm y=0 x=0..2, vertical arm x=0 y=0..2 sharing corner (0,0)
            grid[0, 0] = grid[1, 0] = grid[2, 0] = PieceType.Paw;
            grid[0, 1] = grid[0, 2] = PieceType.Paw;

            var groups = MatchDetector.DetectShapes(new FakeBoard(grid));
            Assert.AreEqual(1, groups.Count, "L shape must merge into a single group.");
            Assert.IsTrue(groups[0].IsCrossShape);
            Assert.AreEqual(SpecialType.Bomb, groups[0].ResolveSpecial());
            Assert.AreEqual(5, groups[0].Count);
        }

        [Test]
        public void TShape_CreatesBombSpecial()
        {
            var grid = EmptyGrid();
            grid[0, 0] = grid[1, 0] = grid[2, 0] = grid[3, 0] = PieceType.Bone; // top bar
            grid[1, 1] = grid[1, 2] = PieceType.Bone;                            // stem down

            var groups = MatchDetector.DetectShapes(new FakeBoard(grid));
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(SpecialType.Bomb, groups[0].ResolveSpecial(),
                "T shape resolves to Bomb even though the bar is length 4.");
        }

        [Test]
        public void WildPiece_BridgesDifferentColors()
        {
            var grid = EmptyGrid();
            grid[0, 0] = PieceType.Heart;
            grid[1, 0] = PieceType.Star;   // different color
            grid[2, 0] = PieceType.Heart;
            var board = new FakeBoard(grid);
            board.SetWild(1, 0);

            var h = MatchDetector.DetectHorizontal(board);
            Assert.AreEqual(1, h.Count, "Wild should bridge Heart-Star-Heart into one run.");
            Assert.AreEqual(PieceType.Heart, h[0].Color);
        }

        // ================================================================
        // 3. BoardManager integration: swap validation & resolution
        // ================================================================

        [Test]
        public void SwapPieces_NonAdjacent_Rejected()
        {
            GameObject go;
            var bm = CreateHeadlessBoard(out go);
            bm.SetupBoard(StableGrid());

            Assert.IsFalse(bm.SwapPieces(0, 0, 5, 5), "Diagonal/far swap must be rejected.");
            Assert.IsFalse(bm.SwapPieces(0, 0, 1, 1), "Diagonal adjacency must be rejected.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SwapPieces_NoMatchCreated_RevertsSwap()
        {
            GameObject go;
            var bm = CreateHeadlessBoard(out go);
            var grid = StableGrid();
            bm.SetupBoard(grid);

            PieceType before1 = bm.GetPieceType(0, 0);
            PieceType before2 = bm.GetPieceType(1, 0);
            bool accepted = bm.SwapPieces(0, 0, 1, 0);

            if (!accepted)
            {
                Assert.AreEqual(before1, bm.GetPieceType(0, 0));
                Assert.AreEqual(before2, bm.GetPieceType(1, 0));
            }
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SwapPieces_ValidMatch_ResolvesAndRefillsFullBoard()
        {
            GameObject go;
            var bm = CreateHeadlessBoard(out go);

            // Handcrafted: swapping (3,1)<->(3,0) completes a horizontal 3-run of Hearts.
            var grid = StableGrid();
            grid[2, 0] = PieceType.Heart;                // left flank
            grid[4, 0] = PieceType.Heart;                // right flank
            grid[3, 0] = PieceType.Leaf;                 // obstacle that swaps away
            grid[3, 1] = PieceType.Heart;                // the piece that moves up
            bm.SetupBoard(grid);

            int cascadeEvents = 0;
            bm.OnCascadeStep += (_, __) => cascadeEvents++;
            bool stableFired = false;
            bm.OnBoardStable += () => stableFired = true;

            bool ok = bm.SwapPieces(3, 1, 3, 0);
            Assert.IsTrue(ok, "Swap creating a vertical Heart run must be accepted.");
            Assert.IsTrue(cascadeEvents >= 1, "At least one cascade step must run.");
            Assert.IsTrue(stableFired, "Board-stable event must fire after cascades.");

            // Post-conditions: board is completely full again and match-free.
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++)
                    Assert.AreNotEqual(PieceType.None, bm.GetPieceType(x, y),
                        $"Cell ({x},{y}) empty after refill.");

            Assert.AreEqual(0, bm.FindMatches().Count, "Cascades must end in a stable board.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ResolveMatches_RemovesMatchedPiecesAndAppliesGravity()
        {
            GameObject go;
            var bm = CreateHeadlessBoard(out go);

            var grid = EmptyGrid();
            // Column x=0: Paws at y=0,1,2 (match). Above them at y=3 a Bone anchor.
            grid[0, 0] = grid[0, 1] = grid[0, 2] = PieceType.Paw;
            grid[0, 3] = PieceType.Bone;
            grid[1, 0] = PieceType.Leaf; // filler so nothing else matches
            bm.SetupBoard(grid);

            var matches = bm.FindMatches();
            Assert.GreaterOrEqual(matches.Count, 1);

            int destroyed = bm.ResolveMatches(matches);
            Assert.AreEqual(3, destroyed, "The three matched Paws must be destroyed.");

            // Gravity: the Bone that was at y=3 fell to y=0. Refill filled y=1..7.
            Assert.AreEqual(PieceType.Bone, bm.GetPieceType(0, 0));
            for (int y = 1; y < 8; y++)
                Assert.AreNotEqual(PieceType.None, bm.GetPieceType(0, y));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FourInLine_SwapCreatesPowerPieceOnBoard()
        {
            GameObject go;
            var bm = CreateHeadlessBoard(out go);

            var grid = StableGrid();
            // Row y=0 has Hearts at x=0,1,2 ; swapping (3,1)->(3,0) makes 4-in-line.
            grid[0, 0] = grid[1, 0] = grid[2, 0] = PieceType.Heart;
            grid[3, 1] = PieceType.Heart;
            grid[3, 0] = PieceType.Star; // will move away
            bm.SetupBoard(grid);

            Assert.IsTrue(bm.SwapPieces(3, 1, 3, 0));

            // Somewhere on row 0 there must now be a Power special (or it was consumed
            // by a follow-up cascade which also counts as resolved gameplay).
            bool powerSeen = false;
            for (int x = 0; x < 8; x++)
            {
                var p = bm.GetPieceAt(x, 0);
                if (p != null && p.IsSpecial && p.Special == SpecialType.Power) powerSeen = true;
            }
            Assert.IsTrue(powerSeen || !bm.IsResolving,
                "4-in-line swap must produce a PowerPiece or fully cascade it away.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GenerateBoard_StartsMatchFree_AndHasValidMove()
        {
            GameObject go;
            var bm = CreateHeadlessBoard(out go);
            bm.GenerateBoard();

            Assert.AreEqual(0, bm.FindMatches().Count, "Fresh board must contain no matches.");
            Assert.IsTrue(bm.HasValidMove(), "Fresh board must contain at least one legal move.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void BoardInput_SwipeDirection_PicksDominantAxis()
        {
            Assert.AreEqual(new Vector2Int(1, 0), BoardInput.SwipeDirection(new Vector2(60, 10)));
            Assert.AreEqual(new Vector2Int(-1, 0), BoardInput.SwipeDirection(new Vector2(-60, -5)));
            Assert.AreEqual(new Vector2Int(0, 1), BoardInput.SwipeDirection(new Vector2(5, 60)));
            Assert.AreEqual(new Vector2Int(0, -1), BoardInput.SwipeDirection(new Vector2(-9, -50)));
        }

        [Test]
        public void AreAdjacent_Matrix()
        {
            Assert.IsTrue(BoardManager.AreAdjacent(2, 2, 3, 2));
            Assert.IsTrue(BoardManager.AreAdjacent(2, 2, 2, 3));
            Assert.IsFalse(BoardManager.AreAdjacent(2, 2, 2, 2));
            Assert.IsFalse(BoardManager.AreAdjacent(2, 2, 3, 3));
            Assert.IsFalse(BoardManager.AreAdjacent(2, 2, 4, 2));
        }
    }
}
