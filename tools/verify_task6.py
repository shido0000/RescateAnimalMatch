#!/usr/bin/env python3
"""
Task 6 verifier (static, no Firebase CLI/emulator in this sandbox).

1. JSON validity of firebase.json / package.json files / firestore.indexes.json.
2. Firestore rules: required collections present + write restrictions per spec
   (users only own doc; donations/config client-write denied).
3. TypeScript sources export the four required endpoints with correct triggers.
4. node --test suite passes (mock-auth contributeHuellas verification).
5. tsc build artifacts exist (functions/dist/index.js).
6. C# static checks: brace balance, <500 lines, IFirestoreBackend members
   implemented by FirebaseBackend partials.
"""
import json, re, subprocess, sys
from pathlib import Path

ROOT = Path("/workspace/rescue-animal-match")
BE = ROOT / "Backend"
FUNCS = BE / "functions"
ok = fail = 0

def check(name, cond):
    global ok, fail
    if cond:
        ok += 1
    else:
        fail += 1
        print(f"FAIL: {name}")

# 1. JSON files -------------------------------------------------------------
for p in [BE/"firebase.json", BE/"package.json", FUNCS/"package.json",
          FUNCS/"tsconfig.json", BE/"firestore.indexes.json"]:
    try:
        data = json.loads(p.read_text())
        check(f"{p.name} valid JSON", True)
        if p.name == "firebase.json":
            check("firebase.json wires firestore.rules", data["firestore"]["rules"] == "firestore.rules")
            check("firebase.json has functions+hosting+emulators",
                  all(k in data for k in ("functions", "hosting", "emulators")))
    except Exception as e:
        check(f"{p.name} valid JSON ({e})", False)

# 2. Rules ------------------------------------------------------------------
rules = (BE/"firestore.rules").read_text()
check("rules_version declared", rules.startswith("rules_version = '2';"))
for coll in ["config", "shelters", "donations", "users"]:
    check(f"rules match /{coll}", f"/{coll}/" in rules)
# config & donations must deny client writes
blocks = dict(re.findall(r"match\s+/(\w+)/\{[^}]*\}\s*\{(.*?)\}", rules, re.S))
check("config write denied to clients", "allow write: if false" in blocks.get("config", ""))
check("donations write denied to clients", "allow write: if false" in blocks.get("donations", ""))
check("users writable only by owner", "isOwner(userId)" in blocks.get("users", ""))
check("catch-all deny exists", "/{document=**}" in rules and "if false" in rules.split("/{document=**}")[-1])

# 3. TypeScript endpoints ----------------------------------------------------
src = {p.name: p.read_text() for p in (FUNCS/"src").glob("*.ts")}
check("index exports 4 endpoints",
      all(n in src["index.ts"] for n in
          ["contributeHuellas", "onDonationReached", "validatePurchase", "getShelters"]))
check("contribute is callable onCall", "onCall" in src["contribute.ts"])
check("contribute requires auth", "unauthenticated" in src["contribute.ts"])
check("contribute uses transaction", "runTransaction" in src["contribute.ts"])
check("trigger on config/weekly_goal update",
      "onDocumentUpdated('config/weekly_goal'" in src["donations.ts"])
check("donation doc created w/ verified:false",
      "collection('donations').add" in src["donations.ts"] and "verified: false" in src["donations.ts"])
check("FCM notification sent", "sendEachForMulticast" in src["donations.ts"])
check("validatePurchase calls androidpublisher v3",
      "androidpublisher" in src["purchases.ts"] and "purchases.products.get" in src["purchases.ts"])
check("all 5 product ids allow-listed",
      all(pid in src["purchases.ts"] for pid in
          ["hammer","shuffle","moves5","season_pass","donation_pack"]))
check("getShelters filters verified==true",
      "where('verified', '==', true)" in src["shelters.ts"])
check("no hardcoded secrets",
      not re.search(r"AIza[0-9A-Za-z_\-]{20,}|\"private_key\"\s*:", "\n".join(src.values())))

# 4. node --test -------------------------------------------------------------
r = subprocess.run(["node", "--test", str(FUNCS/"tests")], capture_output=True, text=True)
check("node --test passes", r.returncode == 0)
m = re.search(r"# pass (\d+)", r.stdout)
print(f"   node tests passed: {m.group(1) if m else '?'}")

# 5. compiled output ---------------------------------------------------------
check("dist/index.js built", (FUNCS/"dist/index.js").exists())

# 6. C# side -----------------------------------------------------------------
cs_files = sorted((ROOT/"Assets/Scripts/Backend").rglob("*.cs"))
iface = (ROOT/"Assets/Scripts/Backend/IFirestoreBackend.cs").read_text()
members = set(re.findall(r"(?:void|IDisposable)\s+(\w+)\(", iface))
fb_src = ((ROOT/"Assets/Scripts/Backend/Firebase/FirebaseBackend.cs").read_text() +
          (ROOT/"Assets/Scripts/Backend/Firebase/FirebaseBackend.Firestore.cs").read_text())
for mname in members - {"Dispose"}:
    check(f"FirebaseBackend implements {mname}", f"{mname}(" in fb_src)
for p in cs_files:
    s = p.read_text()
    check(f"{p.name} <500 lines", len(s.splitlines()) < 500)
    stripped = re.sub(r'"(?:\\.|[^"\\])*"|//[^\n]*|/\*.*?\*/', '', s, flags=re.S)
    check(f"{p.name} braces balanced", stripped.count("{") == stripped.count("}"))
check("Firebase code guarded by USE_FIREBASE",
      all("#if USE_FIREBASE" in p.read_text() for p in (ROOT/"Assets/Scripts/Backend/Firebase").glob("*.cs")))
check("MiniJson available unguarded (used by mock+real)",
      "#if" not in (ROOT/"Assets/Scripts/Backend/MiniJson.cs").read_text().split("\n")[0])

print(f"\nRESULT: {ok} OK / {fail} FAIL")
sys.exit(1 if fail else 0)
