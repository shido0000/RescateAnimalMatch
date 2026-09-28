using System;
using System.Collections.Generic;
using UnityEngine;
using RescueAnimalMatch.Core;

namespace RescueAnimalMatch.Monetization
{
    /// <summary>
    /// Gestor de anuncios (Task 5). Reglas de política del juego:
    ///  - Los rewarded son SIEMPRE opcionales y otorgan 50 Huellas o +5 movimientos.
    ///  - Los interstitial solo se muestran tras completar nivel, máximo 1 cada 3 niveles.
    ///  - Nunca se muestra un anuncio durante gameplay activo (se verifica con GamePhase).
    ///  - Cada impresión se loguea en Firebase Analytics (ad_watched / ad_impression).
    /// IDs de ad unit de prueba de Google (placeholders sin secretos reales).
    /// </summary>
    [DisallowMultipleComponent]
    public class AdManager : MonoBehaviour
    {
        // ---- Test ad unit IDs de Google (placeholders oficiales) ----
        public const string TestAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string TestRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        public const string TestInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";

        public const int RewardedHuellas = 50;
        public const int RewardedExtraMoves = 5;
        public const int InterstitialLevelInterval = 3;

        private static readonly HashSet<string> GameplayActivePhases =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ingame", "board_active", "cascade" };

        [Tooltip("Inyectable para tests/offline; si es null se usa MockAdsProvider.")]
        [SerializeField] private bool useMockProvider = true;

        private IAdsProvider _provider;
        private IAnalyticsLogger _analytics;
        private Func<string> _gamePhaseGetter;
        private int _levelsSinceLastInterstitial;
        private bool _showingAd;

        public IAdsProvider Provider => _provider;
        public bool IsRewardedReady => _provider != null && _provider.IsRewardedReady;
        public bool IsShowingAd => _showingAd;

        private void Awake()
        {
            if (_provider == null) Configure(ResolveProvider(), new MockAnalyticsLogger());
        }

        /// <summary>Sobrescribe el proveedor (inyección desde bootstrap/tests).</summary>
        public void Configure(IAdsProvider provider, IAnalyticsLogger analytics)
        {
            _provider = provider ?? ResolveProvider();
            _analytics = analytics ?? new MockAnalyticsLogger();
        }

        private IAdsProvider ResolveProvider()
        {
#if USE_ADMOB && !UNITY_EDITOR
            if (!useMockProvider) return new GoogleMobileAdsProvider(TestAppId);
#endif
            return new MockAdsProvider();
        }

        /// <summary>
        /// Bootstrap: inicializa el SDK y precarga ambos formatos. Debe llamarse
        /// desde SplashScreen, nunca desde la escena de juego.
        /// </summary>
        public void Initialize(Func<string> gamePhaseGetter = null)
        {
            _gamePhaseGetter = gamePhaseGetter;
            _provider.Initialize(TestAppId);
            _provider.PreloadRewarded(TestRewardedUnitId);
            _provider.PreloadInterstitial(TestInterstitialUnitId);
            LogImpression("init", AdFormat.Rewarded, false);
        }

        /// <summary>Recompensa opcional: onReward(true, 50 huellas) o (true, +5 movs).</summary>
        public void ShowRewardedAd(Action<bool, RewardResult> onReward)
        {
            if (IsGameplayActive())
            {
                RejectDuringGameplay("rewarded");
                onReward?.Invoke(false, new RewardResult(false, 0, 0, TestRewardedUnitId));
                return;
            }

            if (!_provider.IsRewardedReady)
            {
                _provider.PreloadRewarded(TestRewardedUnitId);
                onReward?.Invoke(false, new RewardResult(false, 0, 0, TestRewardedUnitId));
                return;
            }

            _showingAd = true;
            _provider.ShowRewarded(TestRewardedUnitId, result =>
            {
                _showingAd = false;
                LogImpression(result.Earned ? "reward_earned" : "skipped", AdFormat.Rewarded, result.Earned);
                if (result.Earned) GrantReward(result);
                // Recarga inmediata para la próxima solicitud.
                _provider.PreloadRewarded(TestRewardedUnitId);
                onReward?.Invoke(result.Earned, result);
            });
        }

        /// <summary>Variante de la spec: callback simple al otorgar la recompensa.</summary>
        public void ShowRewardedAd(Action<RewardResult> onReward)
        {
            ShowRewardedAd((earned, result) => { if (earned) onReward?.Invoke(result); });
        }

        /// <summary>
        /// Interstitial post-nivel. Se ignora silenciosamente si hay gameplay
        /// activo o si aún no se cumplieron 3 niveles desde el último.
        /// </summary>
        public void ShowInterstitial()
        {
            if (IsGameplayActive())
            {
                RejectDuringGameplay("interstitial");
                return;
            }

            if (_levelsSinceLastInterstitial < InterstitialLevelInterval)
            {
                _levelsSinceLastInterstitial++;
                return; // todavía no toca
            }

            _levelsSinceLastInterstitial = 0;
            if (!_provider.IsInterstitialReady)
            {
                _provider.PreloadInterstitial(TestInterstitialUnitId);
                return;
            }

            _showingAd = true;
            _provider.ShowInterstitial(TestInterstitialUnitId, () =>
            {
                _showingAd = false;
                LogImpression("shown", AdFormat.Interstitial, false);
                _provider.PreloadInterstitial(TestInterstitialUnitId);
            });
        }

        /// <summary>Llamado por LevelManager al completar nivel (contador de frecuencia).</summary>
        public void NotifyLevelCompleted()
        {
            _levelsSinceLastInterstitial++;
        }

        /// <summary>Hooks de recompensa que la escena de juego puede registrar.</summary>
        public event Action<int> OnHuellasRewardGranted;
        public event Action<int> OnExtraMovesRewardGranted;

        private void GrantReward(RewardResult result)
        {
            if (result.HuellasAmount > 0)
            {
                // Default: acreditar al wallet persistente (PlayerPrefs). Si el host
                // suscribió OnHuellasRewardGranted (p. ej. CurrencyManager de escena),
                // ese handler es responsable y se omite el default para no duplicar.
                if (OnHuellasRewardGranted == null)
                    Progression.CurrencyWallet.AddToPersistentWallet(result.HuellasAmount);
                OnHuellasRewardGranted?.Invoke(result.HuellasAmount);
            }
            else if (result.ExtraMoves > 0)
            {
                OnExtraMovesRewardGranted?.Invoke(result.ExtraMoves);
            }
        }

        private bool IsGameplayActive()
        {
            var phase = _gamePhaseGetter != null ? _gamePhaseGetter() : null;
            return !string.IsNullOrEmpty(phase) && GameplayActivePhases.Contains(phase);
        }

        private void RejectDuringGameplay(string kind)
        {
            Debug.LogWarning($"[AdManager] Blocked {kind} ad during active gameplay (policy).");
            LogImpression("blocked_gameplay_" + kind, AdFormat.Rewarded, false);
        }

        private void LogImpression(string action, AdFormat format, bool earned)
        {
            var p = new Dictionary<string, object>
            {
                { "ad_format", format.ToString().ToLowerInvariant() },
                { "action", action },
                { "earned_reward", earned },
                { "test_units", true }
            };
            _analytics?.LogEvent(AnalyticsEvents.AdWatched, p);
        }

        // --- Visibilidad para tests ---
        internal void SetProviderForTests(IAdsProvider provider) => _provider = provider;
        internal void SetAnalyticsForTests(IAnalyticsLogger logger) => _analytics = logger;
        internal int LevelsSinceInterstitial => _levelsSinceLastInterstitial;
    }
}
