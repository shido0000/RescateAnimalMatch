using System;
using System.Collections.Generic;

namespace RescueAnimalMatch.Monetization
{
    /// <summary>Categoría de producto IAP del juego.</summary>
    public enum ProductKind { Consumable, NonConsumable, Subscription }

    /// <summary>Metadatos inmutables de un producto (catálogo local).</summary>
    public sealed class ProductInfo
    {
        public string ProductId;
        public string Title;
        public string Description;
        public string LocalizedPrice;   // "USD 2.99" — lo resuelve la tienda
        public string IsoCurrencyCode;
        public decimal MicroAmount;     // nano/micro-unidades según tienda
        public ProductKind Kind;

        /// <summary>% de ingresos netos que va a refugios (transparencia Task 10).</summary>
        public int DonationPercent;

        public bool IsValid =>
            !string.IsNullOrEmpty(ProductId) &&
            !string.IsNullOrEmpty(Title);
    }

    /// <summary>Resultado de una compra tras validación de recibo.</summary>
    public sealed class PurchaseResult
    {
        public bool Success;
        public string ProductId;
        public string ReceiptJson;
        public string Error;
        /// <summary>Recibo validado server-side por el Cloud Function validatePurchase.</summary>
        public bool ServerValidated;

        public static PurchaseResult Ok(string productId, string receipt, bool validated) =>
            new PurchaseResult { Success = true, ProductId = productId, ReceiptJson = receipt, ServerValidated = validated };

        public static PurchaseResult Fail(string productId, string error) =>
            new PurchaseResult { Success = false, ProductId = productId, Error = error };
    }

    /// <summary>
    /// Contrato de billing. La implementación real usa Unity IStoreListener +
    /// Google Play (USE_IAP); MockBillingProvider cubre editor y tests.
    /// </summary>
    public interface IBillingProvider
    {
        /// <summary>Carga el catálogo desde la tienda (callback con productos).</summary>
        void FetchProducts(Action<bool, IReadOnlyList<ProductInfo>> onResult);

        /// <summary>Inicia compra; callback con resultado ya validado en servidor.</summary>
        void Purchase(string productId, Action<PurchaseResult> onResult);

        /// <summary>Reintegro de compras no consumibles/restore.</summary>
        void Restore(Action<bool, IReadOnlyList<string>> onResult);
    }

    /// <summary>Callback de entrega de producto consumido (post-fulfillment).</summary>
    public delegate void ProductFulfilled(string productId);
}
