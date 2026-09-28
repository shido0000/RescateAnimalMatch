#!/usr/bin/env python3
"""Static + logic verifier for Task 4 (Firebase/Donations/Adoption).

1. Validates Resources/Mocks/mock_firestore.json against the Firestore schema.
2. Ports CommunityProgress/MockFirestoreBackend semantics to Python and asserts
   ContributeHuellas(1000) updates progress correctly and donation triggers fire
   exactly once per cycle.
3. Static checks: braces balanced, <500 lines, member cross-references.
Run inside Unity additionally with: dotnet test (EditMode tests included).
"""
import json, re, sys
from pathlib import Path

ROOT = Path("/workspace/rescue-animal-match")
FAIL = []

def check(cond, msg):
    print(("OK   " if cond else "FAIL ") + msg)
    if not cond: FAIL.append(msg)

data = json.loads((ROOT/"Assets/Resources/Mocks/mock_firestore.json").read_text())
g = data["weekly_goal"]
check(set(g) >= {"targetHuellas","currentHuellas","shelterId","shelterName","rewardDescription"},
      "weekly_goal has required fields")
check(all(set(s) >= {"name","location","contact","needs","animals","verified"} for s in data["shelters"]),
      "shelters match schema {name, location, contact, needs[], animals[], verified}")
check(any(not s["verified"] for s in data["shelters"]), "seed contains an unverified shelter for filtering")

class Goal:
    def __init__(s,t,c,sid,sname): s.target,s.current,s.sid,s.sname=t,c,sid,sname
    @property
    def reached(s): return s.target>0 and s.current>=s.target
    @property
    def progress(s): return max(0.,min(1.,s.current/s.target)) if s.target>0 else 0.
    def same(o,s): return s is not None and (o.current,o.target,o.sid)==(s.current,s.target,s.sid)

class MockBackend:
    def __init__(s,seed):
        gg=seed["weekly_goal"]
        s.goal=Goal(gg["targetHuellas"],gg["currentHuellas"],gg["shelterId"],gg["shelterName"])
        s.open=not s.goal.reached; s.donations=[]; s.subs=[]
    def increment(s,a):
        if a<=0: return None
        s.goal.current+=a
        if s.open and s.goal.reached:
            s.open=False; s.donations.append({"amount":s.goal.target,"type":"weekly_goal","verified":False})
        for cb in s.subs: cb()
        return Goal(s.goal.target,s.goal.current,s.goal.sid,s.goal.sname)
    def subscribe(s,cb): s.subs.append(cb)

class Progress:
    def __init__(s,b):
        s.b=b; gg=b.goal
        s.snapshot=Goal(gg.target,gg.current,gg.sid,gg.sname)
        s.cycle_fired=s.snapshot.reached; s.reaches=[]
        b.subscribe(s.apply)
    def apply(s):
        gg=s.b.goal; incoming=Goal(gg.target,gg.current,gg.sid,gg.sname)
        just=incoming.reached and not s.cycle_fired
        if just: s.cycle_fired=True
        s.snapshot=incoming
        if just: s.reaches.append(incoming)
    def contribute(s,a):
        res=s.b.increment(a)
        if res is not None and not s.snapshot.same(res): s.apply()
        return res

p=Progress(MockBackend(data)); snap=p.contribute(1000)
check(snap.current==g["currentHuellas"]+1000, f"ContributeHuellas(1000): {g['currentHuellas']} -> {snap.current}")
check(abs(snap.progress-(g['currentHuellas']+1000)/g['targetHuellas'])<1e-6, "progress bar ratio updated")

small={"weekly_goal":{"targetHuellas":5000,"currentHuellas":1000,"shelterId":"sh_1","shelterName":"T","rewardDescription":"x"},"shelters":[],"donations":[]}
p2=Progress(MockBackend(small)); p2.contribute(4000)
check(len(p2.reaches)==1 and p2.snapshot.reached, "goal reached fires once")
p2.contribute(500); p2.b.increment(100)
check(len(p2.reaches)==1, "over-contribution/realtime pushes do not re-trigger")
check(len(p2.b.donations)==1 and p2.b.donations[0]["amount"]==5000 and not p2.b.donations[0]["verified"],
      "donation doc created once with verified=false")

CS_FILES=[
 "Assets/Scripts/Backend/IFirestoreBackend.cs",
 "Assets/Scripts/Backend/WeeklyGoalSnapshot.cs",
 "Assets/Scripts/Backend/Mocks/MockFirestoreJson.cs",
 "Assets/Scripts/Backend/Mocks/MockFirestoreBackend.cs",
 "Assets/Scripts/Donations/ShelterData.cs",
 "Assets/Scripts/Donations/DonationRecord.cs",
 "Assets/Scripts/Donations/CurrencyManager.cs",
 "Assets/Scripts/Donations/CommunityProgress.cs",
 "Assets/Scripts/Donations/DonationTracker.cs",
 "Assets/Scripts/UI/AdoptionScreen.cs",
 "Assets/Tests/EditMode/Donations/CommunityProgressTests.cs",
 "Assets/Tests/EditMode/Donations/CurrencyManagerTests.cs",
 "Assets/Tests/EditMode/Donations/AdoptionScreenTests.cs",
]
allsrc=""
for rel in CS_FILES:
    t=(ROOT/rel).read_text(); allsrc+=t
    check(t.count("{")==t.count("}"), f"braces balanced: {rel}")
    check(len(t.splitlines())<500, f"<500 lines: {rel}")
for m in ["IncrementWeeklyGoal","GetVerifiedShelters","SubscribeWeeklyGoal","ContributeHuellas",
          "OnWeeklyGoalReached","BuildContactUri","FilterAnimals","ContributeToCommunity",
          "_donationCycleFired","SameState","HuellasKey"]:
    check(re.search(r"\b"+m+r"\b",allsrc) is not None, f"member present: {m}")
n_tests=allsrc.count("[Test]")
check(n_tests>=15, f"{n_tests} EditMode [Test] methods present")

sys.exit(1 if FAIL else 0)
