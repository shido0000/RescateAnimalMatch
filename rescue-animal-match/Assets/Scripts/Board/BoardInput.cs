using UnityEngine;
using UnityEngine.EventSystems;

namespace RescueAnimalMatch.Board
{
    /// <summary>
    /// Translates touch/mouse gestures into BoardManager.SwapPieces calls.
    /// - Tap + tap on an adjacent cell = swap.
    /// - Press + drag (swipe) in one of 4 directions = swap with the neighbor.
    /// Only valid moves reach the board; invalid swipes simply cancel selection.
    /// </summary>
    public class BoardInput : MonoBehaviour
    {
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private float swipeThresholdPx = 40f;
        [SerializeField] private LayerMask pieceLayerMask = ~0;

        private Piece selectedPiece;
        private Vector3 pressPosition;
        private bool pressCaptured;

        public Piece SelectedPiece => selectedPiece;
        public float SwipeThresholdPx => swipeThresholdPx;

        private void Awake()
        {
            if (boardManager == null)
                boardManager = GetComponentInParent<BoardManager>();
            if (sceneCamera == null)
                sceneCamera = Camera.main;
        }

        private void Update()
        {
            if (boardManager == null || boardManager.IsResolving) return;

#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(0)) OnPointerDown(Input.mousePosition);
            if (Input.GetMouseButtonUp(0)) OnPointerUp(Input.mousePosition);
#else
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began) OnPointerDown(touch.position);
                else if (touch.phase == TouchPhase.Ended) OnPointerUp(touch.position);
            }
#endif
        }

        // ----------------------------------------------------------------
        // Gesture handling
        // ----------------------------------------------------------------

        public void OnPointerDown(Vector2 screenPos)
        {
            if (IsUiHovered(screenPos)) return;

            pressPosition = screenPos;
            pressCaptured = true;
            selectedPiece = RaycastPiece(screenPos);
        }

        public void OnPointerUp(Vector2 screenPos)
        {
            if (!pressCaptured) return;
            pressCaptured = false;

            Vector2 delta = screenPos - pressPosition;

            if (delta.magnitude >= swipeThresholdPx)
            {
                HandleSwipe(delta);
                selectedPiece = null;
                return;
            }

            // Tap-tap fallback: select, then swap with adjacent tapped piece.
            var tapped = RaycastPiece(screenPos);
            if (tapped == null) { selectedPiece = null; return; }

            if (selectedPiece == null)
            {
                selectedPiece = tapped;
            }
            else
            {
                TrySwap(selectedPiece, tapped);
                selectedPiece = null;
            }
        }

        private void HandleSwipe(Vector2 delta)
        {
            if (selectedPiece == null) return;

            Vector2Int dir = SwipeDirection(delta);
            int tx = selectedPiece.GridX + dir.x;
            int ty = selectedPiece.GridY + dir.y;

            if (!BoardManager.InBounds(tx, ty)) return;
            if (!BoardManager.AreAdjacent(selectedPiece.GridX, selectedPiece.GridY, tx, ty)) return;

            boardManager.SwapPieces(selectedPiece.GridX, selectedPiece.GridY, tx, ty);
        }

        /// <summary>
        /// Dominant-axis direction of a swipe: exactly one of (1,0)(-1,0)(0,1)(0,-1).
        /// Public static so unit tests can pin the geometry without touching Input.
        /// </summary>
        public static Vector2Int SwipeDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                return new Vector2Int(delta.x > 0 ? 1 : -1, 0);
            return new Vector2Int(0, delta.y > 0 ? 1 : -1);
        }

        private bool TrySwap(Piece a, Piece b)
        {
            if (a == b) return;
            if (!BoardManager.AreAdjacent(a.GridX, a.GridY, b.GridX, b.GridY))
            {
                // Not adjacent: re-select instead of failing silently.
                selectedPiece = b;
                return;
            }
            boardManager.SwapPieces(a.GridX, a.GridY, b.GridX, b.GridY);
        }

        // ----------------------------------------------------------------
        // Picking
        // ----------------------------------------------------------------

        /// <summary>Raycasts against piece colliders; falls back to grid math from world pos.</summary>
        public Piece RaycastPiece(Vector2 screenPos)
        {
            if (sceneCamera == null || boardManager == null) return null;

            Ray ray = sceneCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, pieceLayerMask))
            {
                var p = hit.collider.GetComponentInParent<Piece>();
                if (p != null) return p;
            }

            // Fallback: convert hit-plane world position to grid coordinates.
            Vector3 world = GetWorldPointOnBoardPlane(screenPos);
            int gx = Mathf.RoundToInt((world.x - BoardWorldOrigin().x) / boardManager.CellSize.x);
            int gy = Mathf.RoundToInt((world.y - BoardWorldOrigin().y) / boardManager.CellSize.y);
            return boardManager.GetPieceAt(gx, gy);
        }

        private Vector3 GetWorldPointOnBoardPlane(Vector2 screenPos)
        {
            Plane plane = new Plane(Vector3.forward, boardManager.WorldPositionForCell(0, 0));
            Ray ray = sceneCamera.ScreenPointToRay(screenPos);
            float enter;
            if (plane.Raycast(ray, out enter))
                return ray.GetPoint(enter);
            return Vector3.zero;
        }

        private Vector3 BoardWorldOrigin() =>
            boardManager != null ? boardManager.WorldPositionForCell(0, 0) : Vector3.zero;

        private static bool IsUiHovered(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            var pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);
            foreach (var r in results)
                if (r.gameObject.layer == LayerMask.NameToLayer("UI")) return true;
            return false;
        }

        // Test/automation hooks
        public void SetBoardManager(BoardManager bm) { boardManager = bm; }
        public void SetCamera(Camera cam) { sceneCamera = cam; }
    }
}
