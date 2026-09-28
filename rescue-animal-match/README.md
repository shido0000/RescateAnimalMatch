# 🐾 Rescate Animal Match

Match-3 puzzle game for Android (Unity 2022 LTS) where community progress unlocks **real-world
donations to animal shelters**, funded by rewarded ads and in-app purchases.

> El juego debe ser entretenido, ético, escalable y rentable, con impacto social medible
> en bienestar animal y adopción responsable.

## Feature list
- 8x8 match-3 board, 6 piece types (Paw, Bone, Heart, Fish, Star, Leaf)
- Special pieces: PowerPiece (4-in-line), BombPiece (L/T), WildPiece (5-in-line)
- 30 levels with objectives (RescueAnimals, CollectResources, ClearDebris, FeedAnimals)
- "Huellas" currency + weekly community goal → real donations (verified shelters)
- Adoption screen fed by Firestore (`shelters` where `verified == true`)
- Monetization: Google AdMob (rewarded / interstitial) + Google Play Billing (IAP)
- Firebase backend: Auth, Firestore, Cloud Functions, FCM, Analytics, Crashlytics
- Next.js Admin Panel (shelters, donations, events, content, metrics)
- Accessibility: colorblind palette, text-size scaling, full localization (JSON)

## Repository layout
```
rescue-animal-match/
├── Assets/Scripts/{Core,Board,Progression,Monetization,Donations,Backend,UI}
├── Assets/{Prefabs,Scenes,Sprites,Audio,Resources/{Levels,Cosmetics,Localization}}
├── Packages/manifest.json          # UPM + Firebase + AdMob + Purchasing
├── ProjectSettings/                # Unity 2022 LTS config (IL2CPP, ARM64, API 26+)
├── Backend/                        # Firebase: functions (TS), firestore.rules, firebase.json
├── AdminPanel/                     # Next.js admin app
└── Documentation/                  # GDD, API reference, impact report, legal docs
```

## Requirements
| Tool | Version | Notes |
|------|---------|-------|
| Unity | 2022.3 LTS | IL2CPP, ARM64, Min SDK Android 8.0 (API 26) |
| Node.js | ≥ 18 | Cloud Functions + Admin Panel |
| Firebase CLI | ≥ 12 | Emulators for backend tests |
| Android SDK | API 26 → latest stable | Build target |

## Getting started
1. Open `rescue-animal-match/` with Unity Hub (2022.3 LTS). Package name: `com.rescueanimalmatch.game`.
2. Drop your `google-services.json` into `Assets/StreamingAssets/` (never commit it).
3. Configure secrets through `ProjectSettings` + environment variables — see
   `Documentation/API_REFERENCE.md`. **No hardcoded keys.**
4. Backend: `cd Backend/functions && npm i && npm run build`, then
   `firebase emulators:start --project rescue-animal-match`.
5. Admin panel: `cd AdminPanel && npm i && npm run dev`.

## Build
```bash
# Editor import check (must finish with zero errors)
Unity -batchmode -quit -projectPath ./rescue-animal-match -logFile build.log

# Release AAB (Android 8.0+, ARM64 only)
Unity -batchmode -quit -projectPath ./rescue-animal-match \
      -buildTarget Android -buildAAB ./Build/Release/RescueAnimalMatch.aab
```

## Tests
```bash
# EditMode unit tests (board engine, progression, donations, monetization mocks)
Unity -batchmode -quit -projectPath ./rescue-animal-match \
      -runTests -testPlatform EditMode -testResults Results/editmode.xml
```
Coverage target: **> 80 %** on core board logic (`Assets/Scripts/Board`).

## Documentation
- `Documentation/GAME_DESIGN.md`
- `Documentation/API_REFERENCE.md`
- `Documentation/IMPACT_REPORT_TEMPLATE.md`
- Legal: `PRIVACY_POLICY.md`, `TERMS_OF_SERVICE.md`, `DONATION_TRANSPARENCY.md`

## Roadmap (task order)
1. ✅ Project structure + README
2. ✅ Task 1 – Unity project configuration (ProjectSettings + manifest)
3. ✅ Task 2 – Core match-3 engine + tests
4. ✅ Task 3 – Levels & progression (30 level assets in Resources/Levels)
5. ⬜ Task 4 – Huellas currency & donation system
6. ⬜ Task 5 – Monetization (AdMob, IAP, cosmetics)
7. ⬜ Task 6 – Firebase backend (Cloud Functions TS, rules)
8. ⬜ Task 7 – Next.js admin panel
9. ⬜ Task 8 – UI & scenes (+ localization/accessibility)
10. ⬜ Task 9 – Analytics & Crashlytics
11. ⬜ Task 10 – Legal & compliance docs

## License / compliance notes
Donation transparency, GDPR/CCPA data handling and COPPA age gate are described in
`Documentation/`. All revenue-share percentages committed to shelters are audited quarterly
(see `DONATION_TRANSPARENCY.md`).
