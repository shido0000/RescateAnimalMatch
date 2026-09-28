using System;
using System.Collections.Generic;
using RescueAnimalMatch.Core;

namespace RescueAnimalMatch.Tests.EditMode.Monetization
{
    /// <summary>Logger de analytics falso que registra todo lo emitido.</summary>
    internal sealed class FakeAnalytics : IAnalyticsLogger
    {
        public sealed class LoggedEvent
        {
            public string Name;
            public IReadOnlyDictionary<string, object> Parameters;
        }

        public readonly List<LoggedEvent> Events = new List<LoggedEvent>();
        public readonly Dictionary<string, string> UserProperties =
            new Dictionary<string, string>();

        public void LogEvent(string name, IDictionary<string, object> parameters = null)
        {
            Events.Add(new LoggedEvent
            {
                Name = name,
                Parameters = parameters == null
                    ? new Dictionary<string, object>()
                    : new Dictionary<string, object>(parameters),
            });
        }

        public void SetUserProperty(string name, string value)
        {
            UserProperties[name] = value;
        }

        public bool HasEvent(string name) => Events.Exists(e => e.Name == name);
    }

    /// <summary>
    /// Billing provider falso con control total de resultados. Simula el flujo
    /// completo: compra -> validación server-side (validatePurchase) -> entrega.
    /// </summary>
    internal sealed class FakeBillingProvider : Monetization.IBillingProvider
    {
        public bool InitSucceeds = true;
        public bool ReceiptValid = true;
        public bool FailNextPurchase;
        public int ValidationCallCount;
        public string LastValidatedReceipt;
        public List<string> OwnedOnRestore = new List<string>();

        private readonly List<Monetization.ProductInfo> _catalog =
            new List<Monetization.ProductInfo>();

        public void SeedCatalog()
        {
            _catalog.Clear();
            foreach (var def in Monetization.IapCatalog.Products)
            {
                _catalog.Add(new Monetization.ProductInfo
                {
                    ProductId = def.ProductId,
                    Title = def.Title,
                    Description = def.Description,
                    LocalizedPrice = def.LocalizedPrice,
                    IsoCurrencyCode = def.IsoCurrencyCode,
                    MicroAmount = def.MicroAmount,
                    Kind = def.Kind,
                    DonationPercent = def.DonationPercent,
                });
            }
        }

        public void FetchProducts(Action<bool, IReadOnlyList<Monetization.ProductInfo>> onResult)
        {
            if (!InitSucceeds) { onResult?.Invoke(false, null); return; }
            onResult?.Invoke(true, _catalog);
        }

        public void Purchase(string productId, Action<Monetization.PurchaseResult> onResult)
        {
            if (FailNextPurchase || !_catalog.Exists(p => p.ProductId == productId))
            {
                onResult?.Invoke(Monetization.PurchaseResult.Fail(productId, "billing_unavailable"));
                return;
            }

            string receipt = "receipt-" + productId;
            ValidationCallCount++;                 // llamada al Cloud Function
            LastValidatedReceipt = receipt;
            if (!ReceiptValid)
            {
                onResult?.Invoke(Monetization.PurchaseResult.Fail(
                    productId, "receipt_validation_failed"));
                return;
            }
            onResult?.Invoke(Monetization.PurchaseResult.Ok(productId, receipt, validated: true));
        }

        public void Restore(Action<bool, IReadOnlyList<string>> onResult)
        {
            onResult?.Invoke(true, OwnedOnRestore);
        }
    }
}
