using UnityEngine;

namespace RescueAnimalMatch.Board
{
    /// <summary>
    /// The 6 collectible piece types of Rescate Animal Match.
    /// </summary>
    public enum PieceType
    {
        None = 0,
        Paw = 1,
        Bone = 2,
        Heart = 3,
        Fish = 4,
        Star = 5,
        Leaf = 6
    }

    /// <summary>
    /// Special piece variants created from non-standard match shapes.
    /// </summary>
    public enum SpecialType
    {
        None = 0,
        /// <summary>4-in-a-line: clears the whole row or column (PowerPiece).</summary>
        Power = 1,
        /// <summary>L/T shape: clears a 3x3 area (BombPiece).</summary>
        Bomb = 2,
        /// <summary>5-in-a-line: matches any color (WildPiece).</summary>
        Wild = 3
    }

    /// <summary>
    /// MonoBehaviour attached to every piece GameObject on the board.
    /// Pure state (type/grid coords/specials) is safe to use in EditMode tests;
    /// the animation hooks require PlayMode (they tween transforms).
    /// </summary>
    public class Piece : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private PieceType type = PieceType.None;
        [SerializeField] private int gridX = -1;
        [SerializeField] private int gridY = -1;
        [SerializeField] private bool isSpecial = false;
        [SerializeField] private SpecialType specialType = SpecialType.None;

        [Header("Presentation")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float animationDuration = 0.15f;

        public PieceType Type => type;
        public int GridX => gridX;
        public int GridY => gridY;
        public bool IsSpecial => isSpecial;
        public SpecialType Special => specialType;
        public float AnimationDuration => animationDuration;

        /// <summary>Stable key for the BoardManager lookup dictionary.</summary>
        public Vector2Int GridPosition => new Vector2Int(gridX, gridY);

        /// <summary>
        /// Assigns identity data. Called by BoardManager on spawn/refill.
        /// </summary>
        public void Initialize(PieceType pieceType, int x, int y,
                               SpecialType special = SpecialType.None)
        {
            type = pieceType;
            gridX = x;
            gridY = y;
            specialType = special;
            isSpecial = special != SpecialType.None;
        }

        /// <summary>Marks this piece as destroyed (hidden, excluded from matching).</summary>
        public void MarkDestroyed()
        {
            type = PieceType.None;
            isSpecial = false;
            specialType = SpecialType.None;
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
        }

        /// <summary>
        /// True when this piece participates in match detection.
        /// Wild pieces always participate (they match any color).
        /// </summary>
        public bool MatchesColor(PieceType other)
        {
            if (isSpecial && specialType == SpecialType.Wild) return true;
            return type == other;
        }

        // ----------------------------------------------------------------
        // Animation hooks (PlayMode only; no-ops when Unity is unavailable)
        // ----------------------------------------------------------------

        /// <summary>Tweens the piece to a new world position after a swap.</summary>
        public void AnimateSwap(Vector3 target, float duration = -1f)
        {
            if (duration < 0f) duration = animationDuration;
            StopAllCoroutines();
            StartCoroutine(SwapRoutine(target, duration));
        }

        /// <summary>Shrinks and fades the piece before it is removed.</summary>
        public void AnimateDestroy(System.Action onComplete = null)
        {
            StopAllCoroutines();
            StartCoroutine(DestroyRoutine(onComplete));
        }

        /// <summary>Drops the piece down by the given world offset with easing.</summary>
        public void AnimateFall(Vector3 dropOffset, float duration = -1f)
        {
            if (duration < 0f) duration = animationDuration;
            StopAllCoroutines();
            StartCoroutine(FallRoutine(dropOffset, duration));
        }

        private System.Collections.IEnumerator SwapRoutine(Vector3 target, float duration)
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            transform.position = target;
        }

        private System.Collections.IEnumerator DestroyRoutine(System.Action onComplete)
        {
            Vector3 start = transform.localScale;
            float t = 0f;
            while (t < animationDuration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(start, Vector3.zero, t / animationDuration);
                yield return null;
            }
            onComplete?.Invoke();
        }

        private System.Collections.IEnumerator FallRoutine(Vector3 dropOffset, float duration)
        {
            Vector3 start = transform.position;
            Vector3 end = start + dropOffset;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                transform.position = Vector3.Lerp(start, end, k * k); // gravity ease-in
                yield return null;
            }
            transform.position = end;
        }
    }
}
