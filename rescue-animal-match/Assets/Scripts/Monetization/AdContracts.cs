using System;

namespace RescueAnimalMatch.Monetization
{
    /// <summary>Tipo de anuncio según la política del juego.</summary>
    public enum AdFormat { Rewarded, Interstitial }

    /// <summary>Estado del SDK de anuncios (adapters en memoria para tests).</summary>
    public enum AdState { Uninitialized, Loading, Ready, Showing, Closed, Failed }

    /// <summary>Resultado de una visualización recompensada.</summary>
    public sealed class RewardResult
    {
        public bool Earned;
        public int HuellasAmount;
        public int ExtraMoves;
        public string AdUnitId;

        public RewardResult(bool earned, int huellas, int moves, string adUnitId)
        {
            Earned = earned;
            HuellasAmount = ClampNonNegative(huellas);
            ExtraMoves = ClampNonNegative(moves);
            AdUnitId = adUnitId;
        }

        private static int ClampNonNegative(int v) => v < 0 ? 0 : v;
    }

    /// <summary>
    /// Abstracción del SDK de anuncios. La implementación real usa Google Mobile
    /// Ads (USE_ADMOB); <see cref="MockAdsProvider"/> sirve para editor y tests.
    /// </summary>
    public interface IAdsProvider
    {
        void Initialize(string appId);
        void PreloadRewarded(string adUnitId);
        void PreloadInterstitial(string adUnitId);
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }
        AdState State { get; }

        /// <summary>Muestra un rewarded; onResult(earned) se llama al cerrar.</summary>
        void ShowRewarded(string adUnitId, Action<RewardResult> onResult);

        /// <summary>Muestra un interstitial; onClose se llama siempre al cerrar.</summary>
        void ShowInterstitial(string adUnitId, Action onClose);
    }
}
