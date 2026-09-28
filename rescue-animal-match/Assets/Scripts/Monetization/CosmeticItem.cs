using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RescueAnimalMatch.Monetization
{
    /// <summary>Tipo de cosmético según la tienda virtual.</summary>
    public enum CosmeticType { BoardTheme, Background, VirtualPet, Avatar }

    /// <summary>
    /// ScriptableObject de un cosmético (Task 5). Los assets viven en
    /// Resources/Cosmetics/; si no hay assets cargados se usa el catálogo
    /// por defecto (<see cref="DefaultCatalog"/>), que además alimenta los
    /// tests EditMode sin dependencias de assets.
    /// </summary>
    [CreateAssetMenu(fileName = "Cosmetic", menuName = "Rescue Animal Match/Cosmetic Item")]
    public class CosmeticItem : ScriptableObject
    {
        [Header("Identidad")]
        public string Id;               // ej. "theme_noche"
        public string LocalizedKey;     // clave en Resources/Localization/es.json
        public CosmeticType Type;
        public Sprite Preview;

        [Header("Adquisición")]
        public int PriceHuellas;        // 0 = gratis / desbloqueado por progreso
        public string UnlockedByLevel;  // opcional: nivel requerido

        [Header("Visual")]
        public Color[] PaletteOverride; // temas de tablero: 6 colores (uno por PieceType)
        public string PrefabResourcePath;

        public bool IsFree => PriceHuellas <= 0;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name;
            if (Type == CosmeticType.BoardTheme &&
                (PaletteOverride == null || PaletteOverride.Length != 6))
            {
                Debug.LogWarning($"[CosmeticItem] '{Id}' es tema de tablero y debe tener 6 colores.");
            }
        }

        /// <summary>Fábrica usada por el catálogo por defecto y por tests.</summary>
        public static CosmeticItem Create(string id, string locKey, CosmeticType type,
                                          int priceHuellas, Color[] palette = null)
        {
            var item = CreateInstance<CosmeticItem>();
            item.name = id;
            item.Id = id;
            item.LocalizedKey = locKey;
            item.Type = type;
            item.PriceHuellas = priceHuellas;
            item.PaletteOverride = palette;
            return item;
        }
    }

    /// <summary>Catálogo por defecto (fallback cuando no hay assets en Resources).</summary>
    public static class DefaultCosmeticCatalog
    {
        private static readonly Color[] ThemePaw =
        {
            new Color(0.95f, 0.75f, 0.45f), new Color(0.85f, 0.85f, 0.90f),
            new Color(0.95f, 0.55f, 0.65f), new Color(0.55f, 0.75f, 0.95f),
            new Color(0.98f, 0.90f, 0.50f), new Color(0.60f, 0.85f, 0.60f),
        };

        private static readonly Color[] ThemeNight =
        {
            new Color(0.45f, 0.40f, 0.65f), new Color(0.35f, 0.45f, 0.60f),
            new Color(0.70f, 0.40f, 0.60f), new Color(0.30f, 0.55f, 0.70f),
            new Color(0.75f, 0.65f, 0.40f), new Color(0.35f, 0.60f, 0.50f),
        };

        public static readonly CosmeticItem PawTheme =
            CosmeticItem.Create("theme_arena", "cosmetic_theme_arena", CosmeticType.BoardTheme, 0, ThemePaw);
        public static readonly CosmeticItem NightTheme =
            CosmeticItem.Create("theme_noche", "cosmetic_theme_night", CosmeticType.BoardTheme, 800, ThemeNight);
        public static readonly CosmeticItem MeadowBg =
            CosmeticItem.Create("bg_pradera", "cosmetic_bg_meadow", CosmeticType.Background, 0);
        public static readonly CosmeticItem SunsetBg =
            CosmeticItem.Create("bg_atardecer", "cosmetic_bg_sunset", CosmeticType.Background, 500);
        public static readonly CosmeticItem PuppyPet =
            CosmeticItem.Create("pet_cachorro", "cosmetic_pet_puppy", CosmeticType.VirtualPet, 1200);
        public static readonly CosmeticItem ParrotPet =
            CosmeticItem.Create("pet_loro", "cosmetic_pet_parrot", CosmeticType.VirtualPet, 1500);
        public static readonly CosmeticItem ExplorerAvatar =
            CosmeticItem.Create("avatar_explorador", "cosmetic_avatar_explorer", CosmeticType.Avatar, 0);
        public static readonly CosmeticItem HeroAvatar =
            CosmeticItem.Create("avatar_heroe", "cosmetic_avatar_hero", CosmeticType.Avatar, 900);

        public static readonly CosmeticItem[] All =
        {
            PawTheme, NightTheme, MeadowBg, SunsetBg,
            PuppyPet, ParrotPet, ExplorerAvatar, HeroAvatar
        };
    }

    /// <summary>
    /// Acceso al catálogo: prefiere los assets de Resources/Cosmetics y cae al
    /// catálogo por defecto. Mantiene identidad estable por Id para persistencia.
    /// </summary>
    public static class CosmeticCatalog
    {
        public const string ResourceFolder = "Cosmetics";

        private static List<CosmeticItem> _cached;

        public static IReadOnlyList<CosmeticItem> Items
        {
            get
            {
                if (_cached != null) return _cached;
                var assets = UnityEngine.Resources.LoadAll<CosmeticItem>(ResourceFolder);
                _cached = assets != null && assets.Length > 0
                    ? assets.Where(a => a != null && !string.IsNullOrEmpty(a.Id)).ToList()
                    : DefaultCosmeticCatalog.All.ToList();
                return _cached;
            }
        }

        public static bool TryGet(string id, out CosmeticItem item)
        {
            item = Items.FirstOrDefault(i => i.Id == id);
            return item != null;
        }

        public static IEnumerable<CosmeticItem> OfType(CosmeticType type) =>
            Items.Where(i => i.Type == type);

        /// <summary>Tests: fuerza el catálogo por defecto sin tocar Resources.</summary>
        public static void ResetCacheForTests() => _cached = null;
    }
}
