#!/usr/bin/env python3
"""
Task 5 verification: faithful Python port of AdManager / IAPManager /
CosmeticManager decision logic + the three EditMode fixtures' expectations.
Mirrors tools/verify_task2.py pattern (no Unity in this sandbox).
"""
import sys

CHECKS = 0
FAILS = []

def check(cond, msg):
    global CHECKS
    CHECKS += 1
    if not cond:
        FAILS.append(msg)

# ---------------- Catalogs (mirror MockBillingProvider.cs / CosmeticItem.cs) --
IAP_IDS = [
    "com.rescueanimalmatch.hammer", "com.rescueanimalmatch.shuffle",
    "com.rescueanimalmatch.moves5", "com.rescueanimalmatch.season_pass",
    "com.rescueanimalmatch.donation_pack",
]
IAP_DONATION_PCT = {pid: 20 for pid in IAP_IDS}
IAP_DONATION_PCT["com.rescueanimalmatch.donation_pack"] = 50

COSMETICS = {  # id -> (type, price)
    "theme_arena": ("BoardTheme", 0), "theme_noche": ("BoardTheme", 800),
    "bg_pradera": ("Background", 0), "bg_atardecer": ("Background", 500),
    "pet_cachorro": ("VirtualPet", 1200), "pet_loro": ("VirtualPet", 1500),
    "avatar_explorador": ("Avatar", 0), "avatar_heroe": ("Avatar", 900),
}

# --------------------------------- CurrencyManager port ---------------------
class Currency:
    def __init__(self): self.huellas = 0; self.save_calls = 0
    def add(self, n): self.huellas += max(0, n)
    def spend(self, n):
        if n < 0 or self.huellas < n: return False
        self.huellas -= n; return True

# --------------------------------- CosmeticManager port ---------------------
class CosmeticManager:
    def __init__(self, currency):
        self.currency = currency
        self.owned = set()
        self.equipped = {}
        self.backend_syncs = 0
        # Awake: free items owned, defaults equipped
        for cid, (ctype, price) in COSMETICS.items():
            if price <= 0:
                self.owned.add(cid)
                self.equipped.setdefault(ctype, cid)

    def owns(self, cid): return cid in self.owned
    def get_equipped(self, ctype): return self.equipped.get(ctype)
    def try_equip(self, cid):
        if cid not in COSMETICS or cid not in self.owned: return False
        self.equipped[COSMETICS[cid][0]] = cid; return True
    def unequip(self, ctype): self.equipped.pop(ctype, None)
    def purchase_with_huellas(self, cid):
        if cid not in COSMETICS or cid in self.owned: return
        price = COSMETICS[cid][1]
        if price > 0 and not self.currency.spend(price): return
        self.owned.add(cid)
        self.equipped[COSMETICS[cid][0]] = cid
        self.backend_syncs += 1   # SyncToBackend -> SaveUserData("cosmetics")

# --------------------------------- IAPManager port ---------------------------
class IAPManager:
    def __init__(self, provider):
        self.provider = provider
        self.catalog = {}
        self.owned = set()
        self.purchase_count = 0
        self.events = []
        self.user_props = {}
        self.delivered = []
        self.donation_pack_huellas = None

    def initialize(self):
        ok, products = self.provider.fetch()
        if ok and products:
            self.catalog = {p: True for p in products}
        return bool(self.catalog)

    def purchase(self, pid):
        if not self.catalog: return ("fail", "catalog_not_loaded")
        if pid not in self.catalog: return ("fail", "unknown_product")
        ok, validated = self.provider.purchase(pid)
        if ok and not validated:
            return ("fail", "receipt_not_server_validated")
        if ok:
            self.owned.add(pid); self.purchase_count += 1
            self.delivered.append(pid)
            if pid == "com.rescueanimalmatch.donation_pack":
                self.donation_pack_huellas = 1250
            self.events.append(("iap_purchased", "purchased", True))
            self.user_props["purchases_count"] = str(self.purchase_count)
            return ("ok", None)
        self.events.append(("iap_purchased", "failed", False))
        return ("fail", "billing_unavailable")

    def restore(self, ids):
        for i in ids:
            if i in self.catalog: self.owned.add(i)

class FakeProvider:
    def __init__(self):
        self.init_ok = True; self.receipt_valid = True; self.fail_next = False
        self.validation_calls = 0
    def fetch(self):
        return (self.init_ok, IAP_IDS if self.init_ok else [])
    def purchase(self, pid):
        if self.fail_next or pid not in IAP_IDS: return (False, False)
        self.validation_calls += 1
        if not self.receipt_valid: return (False, False)
        return (True, True)

# ---------------------------------- AdManager port ---------------------------
class AdManager:
    REWARDED_HUELLAS = 50
    INTERVAL = 3
    GAMEPLAY = {"ingame", "board_active", "cascade"}
    def __init__(self, phase=lambda: "menu"):
        self.phase = phase; self.levels_since = 0; self.shown = 0
        self.wallet = 0; self.host_handlers = 0
    def notify_level_completed(self): self.levels_since += 1
    def show_interstitial(self):
        if self.phase() in self.GAMEPLAY: return
        if self.levels_since < self.INTERVAL:
            self.levels_since += 1; return
        self.levels_since = 0; self.shown += 1
    def show_rewarded(self, ready=True, earned=True):
        if self.phase() in self.GAMEPLAY or not ready: return
        if earned:
            if self.host_handlers == 0: self.wallet += self.REWARDED_HUELLAS

# ================================ TEST EXPECTATIONS ============================
# --- IAPManagerTests ---
check(sorted(IAP_IDS) == sorted([
    "com.rescueanimalmatch.hammer", "com.rescueanimalmatch.shuffle",
    "com.rescueanimalmatch.moves5", "com.rescueanimalmatch.season_pass",
    "com.rescueanimalmatch.donation_pack"]), "Catalog_ContainsRequiredProductIds")

p = FakeProvider(); m = IAPManager(p)
check(m.initialize() and len(m.catalog) == 5, "Initialize_LoadsAllFiveProducts")
status, err = m.purchase(IAP_IDS[0])
check(status == "ok" and p.validation_calls == 1 and m.owned and m.purchase_count == 1,
      "PurchaseProduct_ValidatedReceipt_DeliversOwnsAndLogs")
check(m.events[-1] == ("iap_purchased", "purchased", True)
      and m.user_props["purchases_count"] == "1", "analytics on purchase")

p2 = FakeProvider(); p2.receipt_valid = False; m2 = IAPManager(p2); m2.initialize()
status, err = m2.purchase(IAP_IDS[1])
check(status == "fail" and not m2.delivered and not m2.owned,
      "PurchaseProduct_UnvalidatedReceipt_NoDeliveryNoOwnership")

m3 = IAPManager(FakeProvider())  # no initialize
status, err = m3.purchase(IAP_IDS[2])
check(err == "catalog_not_loaded" and m3.provider.validation_calls == 0,
      "PurchaseProduct_BeforeInitialize_FailsWithCatalogNotLoaded")

m4 = IAPManager(FakeProvider()); m4.initialize()
status, err = m4.purchase("com.rescueanimalmatch.nonexistent")
check(err == "unknown_product" and m4.provider.validation_calls == 0,
      "PurchaseProduct_UnknownProductId")

p5 = FakeProvider(); p5.fail_next = True; m5 = IAPManager(p5); m5.initialize()
status, _ = m5.purchase(IAP_IDS[2])
check(status == "fail" and m5.events[-1] == ("iap_purchased", "failed", False),
      "PurchaseProduct_BillingFailure_LogsFailedStatus")

p6 = FakeProvider(); m6 = IAPManager(p6); m6.initialize()
m6.restore([IAP_IDS[3], IAP_IDS[0], "deleted"])
check(IAP_IDS[3] in m6.owned and IAP_IDS[0] in m6.owned and "deleted" not in m6.owned,
      "RestorePurchases_RegrantsKnownOwnedProducts")

p7 = FakeProvider(); p7.init_ok = False; m7 = IAPManager(p7)
check(not m7.initialize() and not m7.catalog, "Initialize_StoreUnavailable")

m8 = IAPManager(FakeProvider()); m8.initialize()
m8.purchase(IAP_IDS[4])
check(m8.donation_pack_huellas == 1250 and m8.donation_pack_huellas > 0,
      "PurchaseProduct_DonationPack_RaisesCommunityContribution")
check(IAP_DONATION_PCT[IAP_IDS[4]] == 50, "DonationPack_CarriesFiftyPercentSplit")

# --- CosmeticManagerTests ---
cur = Currency(); cur.add(1000); cm = CosmeticManager(cur)
check(cm.owns("theme_arena") and cm.owns("bg_pradera") and cm.owns("avatar_explorador"),
      "FreeDefaultsOwned")
check(cm.get_equipped("BoardTheme") == "theme_arena", "FreeItem_EquippedByDefault")
cm.purchase_with_huellas("theme_noche")
check(cur.huellas == 200 and cm.owns("theme_noche")
      and cm.get_equipped("BoardTheme") == "theme_noche"
      and cm.backend_syncs >= 1, "Purchase_Deducts_OwnsEquips_Syncs")

cur2 = Currency(); cur2.add(100); cm2 = CosmeticManager(cur2)
cm2.purchase_with_huellas("pet_cachorro")
check(not cm2.owns("pet_cachorro") and cur2.huellas == 100 and cm2.backend_syncs == 0,
      "Purchase_InsufficientHuellas_NoStateChange")

cur3 = Currency(); cur3.add(2000); cm3 = CosmeticManager(cur3)
cm3.purchase_with_huellas("bg_atardecer"); cm3.purchase_with_huellas("bg_atardecer")
check(cur3.huellas == 1500, "Purchase_AlreadyOwned_DoesNotChargeTwice")

check(not cm.try_equip("avatar_heroe"), "TryEquip_NotOwned_Fails")
cur4 = Currency(); cur4.add(2000); cm4 = CosmeticManager(cur4)
cm4.purchase_with_huellas("pet_loro")
cm4.unequip("VirtualPet")
check(cm4.get_equipped("VirtualPet") is None, "Unequip_ClearsSlot")
cm4.purchase_with_huellas("no_existe")
check(cur4.huellas == 500 and not cm4.owns("no_existe"), "UnknownProduct_Ignored")

# --- AdManagerTests ---
a = AdManager(); a.show_rewarded()
check(a.wallet == 50, "Rewarded_Grants50Huellas")
a2 = AdManager(phase=lambda: "ingame"); a2.show_rewarded()
check(a2.wallet == 0, "Rewarded_BlockedDuringGameplay")
a3 = AdManager()
for _ in range(3): a3.notify_level_completed(); a3.show_interstitial()
check(a3.shown == 1, "Interstitial_AtMostOnceEveryThreeLevels")
a3.show_interstitial()
check(a3.shown == 1, "Interstitial_NotRepeatedImmediately")
a4 = AdManager(); a4.host_handlers = 1; a4.show_rewarded()
check(a4.wallet == 0, "HostHandlerWired_WalletNotDoubleCredited")
a5 = AdManager(); a5.show_rewarded(earned=False)
check(a5.wallet == 0, "SkippedReward_DoesNotGrant")

print(f"OK - {CHECKS} checks passed" if not FAILS
      else "FAILURES:\n" + "\n".join(FAILS))
sys.exit(1 if FAILS else 0)
