using System;
using System.Collections.Generic;
using UnityEngine;
using RescueAnimalMatch.Core;

namespace RescueAnimalMatch.Monetization
{
    /// <summary>
    /// Gestor de compras dentro de la app (Task 5). Inicializa Google Play
    /// Billing vía Unity IAP (define USE_IAP) o un provider mock para editor
    /// y tests. Regla clave: ningún producto se entrega sin validación de
    /// recibo server-side (Cloud Function validatePurchase, Task 6).
    /// </summary>
    [DisallowMultipleComponent]
    public class IAPManager : MonoBehaviour
    {
        [Tooltip("Si true usa el provider mock (editor/tests/offline).")]
        [SerializeField] private bool useMockProvider = true;

        private IBillingProvider _provider;
        private IAnalyticsLogger _analytics;

        private bool _catalogReady;
        private bool _purchasingInProgress;
        private readonly Dictionary<string, ProductInfo> _catalog =
            new Dictionary<string, ProductInfo>();
        private readonly HashSet<string> _ownedProducts = new HashSet<string>();
        private int _purchaseCount;

        public bool CatalogReady => _catalogReady;
        public IReadOnlyDictionary<string, ProductInfo> Catalog => _catalog;
        public bool Owns(string productId) => _ownedProducts.Contains(productId);
        public int PurchaseCount => _purchaseCount;

        private void Awake()
        {
            if (_provider == null) Configure(ResolveProvider(), new MockAnalyticsLogger());
        }

        /// <summary>Inyección de dependencias usada por bootstrap y tests.</summary>
        public void Configure(IBillingProvider provider, IAnalyticsLogger analytics)
        {
            _provider = provider ?? ResolveProvider();
            _analytics = analytics ?? new MockAnalyticsLogger();
        }

        private IBillingProvider ResolveProvider()
        {
#if USE_IAP && !UNITY_EDITOR
            if (!useMockProvider) return UnityBillingProvider.CreateAndConnect(this);
#endif
            return new MockBillingProvider();
        }

        /// <summary>Carga el catálogo desde la tienda. Callback(bool ok).</summary>
        public void Initialize(Action<bool> onCatalogLoaded = null)
        {
            _provider.FetchProducts((ok, products) =>
            {
                if (!ok || products == null || products.Count == 0)
                {
                    Debug.LogWarning("[IAPManager] Catálogo no disponible.");
                    onCatalogLoaded?.Invoke(false);
                    return;
                }

                _catalog.Clear();
                foreach (var p in products)
                {
                    if (p != null && p.IsValid) _catalog[p.ProductId] = p;
                }

                _catalogReady = _catalog.Count > 0;
                onCatalogLoaded?.Invoke(_catalogReady);
            });
        }

        /// <summary>Compra con validación server-side obligatoria antes de entregar.</summary>
        public void PurchaseProduct(string productId, Action<PurchaseResult> onResult = null)
        {
            if (!_catalogReady)
            {
                onResult?.Invoke(PurchaseResult.Fail(productId, "catalog_not_loaded"));
                return;
            }
            if (_purchasingInProgress)
            {
                onResult?.Invoke(PurchaseResult.Fail(productId, "purchase_in_progress"));
                return;
            }
            if (!_catalog.ContainsKey(productId))
            {
                onResult?.Invoke(PurchaseResult.Fail(productId, "unknown_product"));
                return;
            }

            _purchasingInProgress = true;
            _provider.Purchase(productId, result =>
            {
                _purchasingInProgress = false;

                // Defensa en profundidad: aunque el provider diga Success,
                // exigimos ServerValidated (validatePurchase) antes de entregar.
                if (result.Success && !result.ServerValidated)
                {
                    result = PurchaseResult.Fail(productId, "receipt_not_server_validated");
                }

                if (result.Success)
                {
                    _ownedProducts.Add(productId);
                    _purchaseCount++;
                    DeliverProduct(productId, result.ReceiptJson);
                    LogPurchase(productId, true, null);
                }
                else
                {
                    LogPurchase(productId, false, result.Error);
                }

                onResult?.Invoke(result);
            });
        }

        /// <summary>Restore purchases de Play (no consumibles + season pass).</summary>
        public void RestorePurchases(Action<bool, IReadOnlyList<string>> onResult = null)
        {
            _provider.Restore((ok, ids) =>
            {
                if (ok && ids != null)
                {
                    foreach (var id in ids)
                        if (_catalog.ContainsKey(id)) _ownedProducts.Add(id);
                }
                onResult?.Invoke(ok, ids);
            });
        }

        /// <summary>Efecto del producto al entregarse (fulfillment).</summary>
        public event Action<string> OnProductDelivered;
        /// <summary>El donation_pack debe contribuir al objetivo comunitario.</summary>
        public event Action<int> OnDonationPackPurchased; // huellas equivalentes (50% valor)

        private void DeliverProduct(string productId, string receipt)
        {
            try
            {
                OnProductDelivered?.Invoke(productId);

                if (productId == IapCatalog.DonationPack)
                {
                    // 50% del precio ($9.99 → ~1250 Huellas de valor de donación).
                    OnDonationPackPurchased?.Invoke(1250);
                }
            }
            catch (Exception e)
            {
                CrashReport.NonFatal(e, "Fulfillment failed for " + productId);
            }
        }

        private void LogPurchase(string productId, bool success, string error)
        {
            var p = new Dictionary<string, object>
            {
                { "product_id", productId },
                { "status", success ? "purchased" : "failed" },
                { "server_validated", success },
            };
            if (!string.IsNullOrEmpty(error)) p["error"] = error;
            _analytics?.LogEvent(AnalyticsEvents.IapPurchased, p);
            _analytics?.SetUserProperty(
                AnalyticsUserProperties.PurchasesCount, _purchaseCount.ToString());
        }

        // --- Visibilidad para tests ---
        internal void SetProviderForTests(IBillingProvider provider) => _provider = provider;
        internal void SetAnalyticsForTests(IAnalyticsLogger logger) => _analytics = logger;
    }
}
