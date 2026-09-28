using System;
using System.Collections.Generic;
using System.Linq;

namespace RescueAnimalMatch.Monetization
{
    /// <summary>
    /// Catálogo canónico de productos IAP (los IDs coinciden con Play Console).
    /// </summary>
    public static class IapCatalog
    {
        public const string Hammer = "com.rescueanimalmatch.hammer";
        public const string Shuffle = "com.rescueanimalmatch.shuffle";
        public const string Moves5 = "com.rescueanimalmatch.moves5";
        public const string SeasonPass = "com.rescueanimalmatch.season_pass";
        public const string DonationPack = "com.rescueanimalmatch.donation_pack";

        public static readonly ProductInfo[] Products =
        {
            new ProductInfo
            {
                ProductId = Hammer, Title = "Martillo rescatista",
                Description = "Elimina una pieza bloqueante del tablero.",
                LocalizedPrice = "USD 1.99", IsoCurrencyCode = "USD", MicroAmount = 1990000,
                Kind = ProductKind.Consumable, DonationPercent = 20
            },
            new ProductInfo
            {
                ProductId = Shuffle, Title = "Cambio de piezas",
                Description = "Reorganiza todo el tablero cuando no hay movimientos.",
                LocalizedPrice = "USD 1.99", IsoCurrencyCode = "USD", MicroAmount = 1990000,
                Kind = ProductKind.Consumable, DonationPercent = 20
            },
            new ProductInfo
            {
                ProductId = Moves5, Title = "+5 movimientos",
                Description = "Gana 5 movimientos extra en el nivel actual.",
                LocalizedPrice = "USD 0.99", IsoCurrencyCode = "USD", MicroAmount = 990000,
                Kind = ProductKind.Consumable, DonationPercent = 20
            },
            new ProductInfo
            {
                ProductId = SeasonPass, Title = "Pase de temporada",
                Description = "Recompensas premium durante la temporada vigente.",
                LocalizedPrice = "USD 4.99", IsoCurrencyCode = "USD", MicroAmount = 4990000,
                Kind = ProductKind.NonConsumable, DonationPercent = 20
            },
            new ProductInfo
            {
                ProductId = DonationPack, Title = "Pack donación",
                Description = "El 50% de este pack va directo al refugio de la semana.",
                LocalizedPrice = "USD 9.99", IsoCurrencyCode = "USD", MicroAmount = 9990000,
                Kind = ProductKind.NonConsumable, DonationPercent = 50
            },
        };

        public static IEnumerable<ProductInfo> All => Products;

        public static bool TryGet(string productId, out ProductInfo info)
        {
            info = Products.FirstOrDefault(p => p.ProductId == productId);
            return info != null;
        }

        public static IReadOnlyList<string> Ids =>
            Products.Select(p => p.ProductId).ToList();
    }

    /// <summary>
    /// Billing provider en memoria. Simula fetch, compra exitosa/fracasada y
    /// restore, incluyendo el paso de validación server-side (mock del Cloud
    /// Function validatePurchase de la Task 6).
    /// </summary>
    public sealed class MockBillingProvider : IBillingProvider
    {
        private readonly HashSet<string> _owned = new HashSet<string>();
        private readonly Dictionary<string, int> _purchaseCounts = new Dictionary<string, int>();

        /// <summary>Si true, FetchProducts devuelve error (tienda caída).</summary>
        public bool FailFetch;

        /// <summary>Si true, la validación server-side rechaza el recibo.</summary>
        public bool FailServerValidation;

        /// <summary>IDs que fallan al comprar (p. ej. user cancelled).</summary>
        public readonly HashSet<string> CancelledProducts = new HashSet<string>();

        public IReadOnlyDictionary<string, int> PurchaseCounts => _purchaseCounts;

        public void FetchProducts(Action<bool, IReadOnlyList<ProductInfo>> onResult)
        {
            if (FailFetch)
            {
                onResult?.Invoke(false, Array.Empty<ProductInfo>());
                return;
            }
            // La tienda "resuelve" precios locales sobre el catálogo base.
            var resolved = IapCatalog.Products
                .Select(p => new ProductInfo
                {
                    ProductId = p.ProductId, Title = p.Title, Description = p.Description,
                    LocalizedPrice = p.LocalizedPrice, IsoCurrencyCode = p.IsoCurrencyCode,
                    MicroAmount = p.MicroAmount, Kind = p.Kind, DonationPercent = p.DonationPercent
                })
                .ToList();
            onResult?.Invoke(true, resolved);
        }

        public void Purchase(string productId, Action<PurchaseResult> onResult)
        {
            if (!IapCatalog.TryGet(productId, out _))
            {
                onResult?.Invoke(PurchaseResult.Fail(productId, "unknown_product"));
                return;
            }

            if (CancelledProducts.Contains(productId))
            {
                onResult?.Invoke(PurchaseResult.Fail(productId, "user_cancelled"));
                return;
            }

            var receipt = $"{{\"orderId\":\"mock-{Guid.NewGuid():N}\",\"productId\":\"{productId}\"}}";

            if (FailServerValidation)
            {
                // El recibo se obtuvo pero el servidor lo rechazó: no se entrega.
                onResult?.Invoke(PurchaseResult.Fail(productId, "receipt_validation_failed"));
                return;
            }

            MarkOwned(productId);
            onResult?.Invoke(PurchaseResult.Ok(productId, receipt, validated: true));
        }

        public void Restore(Action<bool, IReadOnlyList<string>> onResult)
        {
            onResult?.Invoke(true, _owned.ToList());
        }

        private void MarkOwned(string productId)
        {
            _owned.Add(productId);
            _purchaseCounts.TryGetValue(productId, out var c);
            _purchaseCounts[productId] = c + 1;
        }

        /// <summary>Puede pre-sembrar compras para pruebas de restore.</summary>
        public void SeedOwnership(IEnumerable<string> productIds)
        {
            foreach (var id in productIds) MarkOwned(id);
        }
    }
}
