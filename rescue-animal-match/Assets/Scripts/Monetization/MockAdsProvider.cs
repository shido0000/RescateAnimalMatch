using System;
using System.Collections.Generic;

namespace RescueAnimalMatch.Monetization
{
    /// <summary>
    /// Proveedor de anuncios en memoria para editor, tests y modo offline.
    /// Simula carga, disponibilidad y el callback de recompensa sin SDK real.
    /// </summary>
    public sealed class MockAdsProvider : IAdsProvider
    {
        private readonly HashSet<string> _readyRewarded = new HashSet<string>();
        private readonly HashSet<string> _readyInterstitial = new HashSet<string>();

        /// <summary>Configura si ShowRewarded otorga la recompensa (default true).</summary>
        public bool RewardedShouldEarn = true;

        /// <summary>Si true, los preload fallan (camino AdState.Failed).</summary>
        public bool FailLoading;

        public string AppId { get; private set; }
        public AdState State { get; private set; } = AdState.Uninitialized;
        public bool IsRewardedReady { get; private set; }
        public bool IsInterstitialReady { get; private set; }

        /// <summary>Contador de impresiones para asserts/logs.</summary>
        public int RewardedShownCount { get; private set; }
        public int InterstitialShownCount { get; private set; }

        public void Initialize(string appId)
        {
            AppId = appId;
            State = AdState.Loading;
        }

        public void PreloadRewarded(string adUnitId)
        {
            if (FailLoading) { State = AdState.Failed; IsRewardedReady = false; return; }
            _readyRewarded.Add(adUnitId);
            IsRewardedReady = true;
            State = AdState.Ready;
        }

        public void PreloadInterstitial(string adUnitId)
        {
            if (FailLoading) { State = AdState.Failed; IsInterstitialReady = false; return; }
            _readyInterstitial.Add(adUnitId);
            IsInterstitialReady = true;
            State = AdState.Ready;
        }

        public void ShowRewarded(string adUnitId, Action<RewardResult> onResult)
        {
            if (!IsRewardedReady || !_readyRewarded.Contains(adUnitId))
            {
                onResult?.Invoke(new RewardResult(false, 0, 0, adUnitId));
                return;
            }

            State = AdState.Showing;
            RewardedShownCount++;
            var earned = RewardedShouldEarn;
            onResult?.Invoke(new RewardResult(earned, earned ? 50 : 0, 0, adUnitId));
            State = AdState.Closed;
            // El rewarded se consume: hay que recargar antes del siguiente.
            _readyRewarded.Remove(adUnitId);
            IsRewardedReady = false;
        }

        public void ShowInterstitial(string adUnitId, Action onClose)
        {
            if (!IsInterstitialReady || !_readyInterstitial.Contains(adUnitId))
            {
                onClose?.Invoke();
                return;
            }

            State = AdState.Showing;
            InterstitialShownCount++;
            onClose?.Invoke();
            State = AdState.Closed;
            _readyInterstitial.Remove(adUnitId);
            IsInterstitialReady = false;
        }
    }
}
