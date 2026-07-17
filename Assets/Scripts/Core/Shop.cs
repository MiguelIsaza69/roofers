namespace RoofingSimulator.Core
{
    /// <summary>One purchasable shop entry (Fase E). charges > 0 marks a consumable pack.</summary>
    public class ShopItem
    {
        public string id;
        public string title;
        public string desc;
        public int price;
        public int charges; // 0 = permanent gear; >0 = consumable charges per purchase
    }

    /// <summary>
    /// The roofer's shop catalog (Fase E, Etapa E1): permanent gear that HELPS without
    /// automating — every item softens a danger the player still has to respect. Bought
    /// from the contract board with career money (no debt: cash in hand only); ownership
    /// persists on the Career save. Effects are wired by JobSceneController.ApplyOwnedGear.
    /// </summary>
    public static class Shop
    {
        public const string Boots = "boots";          // anti-slip soles
        public const string Thermal = "thermal";      // thermal clothing (cold days)
        public const string Canteen = "canteen";      // water canteen (heat waves)
        public const string Harness = "harness";      // reinforced harness (falls)
        public const string NailGun = "nailgun";      // calibrated nail gun (precision)

        // Consumables (Etapa E2) — spent in-level with [Q], cycled with [X].
        public const string Salt = "salt";            // de-icing salt: melts snow in an area
        public const string Coffee = "coffee";        // thermos: stamina top-up + regen boost
        public const string Rope = "rope";            // emergency rope: absorbs one hard fall

        public static readonly ShopItem[] Items =
        {
            new ShopItem
            {
                id = Canteen, title = "Cantimplora", price = 250,
                desc = "El sol te cocina a la mitad de velocidad en olas de calor (la sombra sigue siendo tu amiga)."
            },
            new ShopItem
            {
                id = Thermal, title = "Ropa térmica", price = 300,
                desc = "El frío cala a la mitad en días helados (moverte sigue calentando más)."
            },
            new ShopItem
            {
                id = Boots, title = "Botas de agarre", price = 350,
                desc = "Resbalas un 45% menos en techo mojado o nevado (las espumas siguen siendo el agarre total)."
            },
            new ShopItem
            {
                id = Harness, title = "Arnés reforzado", price = 400,
                desc = "Las caídas duras lesionan un 40% menos (caerse sigue doliendo — y asustando)."
            },
            new ShopItem
            {
                id = NailGun, title = "Clavadora calibrada", price = 500,
                desc = "Centro más generoso al clavar: los clavos algo descentrados pierden menos calidad."
            },
        };

        /// <summary>Consumable packs (Etapa E2): charges stack and persist until spent.</summary>
        public static readonly ShopItem[] Packs =
        {
            new ShopItem
            {
                id = Salt, title = "Sal de deshielo", price = 120, charges = 3,
                desc = "[Q] mirando el techo nevado: derrite la nieve alrededor del punto (más rápido que palear)."
            },
            new ShopItem
            {
                id = Coffee, title = "Termo de café", price = 90, charges = 2,
                desc = "[Q]: estamina al instante, cura un poco la lesión y regenera ×1.5 durante 75 s."
            },
            new ShopItem
            {
                id = Rope, title = "Cuerda de emergencia", price = 150, charges = 1,
                desc = "[Q] para armarla: absorbe POR COMPLETO la próxima caída dura (lesión y aturdimiento)."
            },
        };
    }
}
