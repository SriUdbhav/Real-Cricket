# Technical Architecture Addendum (v1.1) — Approved Revisions

This document records the 10 mandatory architectural revisions applied before Milestone 0 implementation and defines exact Unity project file placement rules.

---

## 1. Approved Architectural Improvements

1. **Dedicated Broadcast & Match Director System**
   - Added `IBroadcastDirector` and strongly-typed `BroadcastCue` contracts in `Cricket.Simulation.Core/PresentationContracts/`.
   - Coordinates camera cuts, replays, crowd reactions, ceremonies, commentary timing, and broadcast overlays exclusively by subscribing to gameplay and lifecycle events on `IMatchEventBus`.
   - Zero reverse dependency: visual directors never mutate match rules or scoring state.

2. **Configurable Cricket Rules via Competition Profiles**
   - All competition regulations are data-driven through `CompetitionRulesProfile` (`BallsPerOver`, `MaxInningsPerTeam`, `MaxOversPerInnings`, `MaxOversPerBowler`, `PowerplayPhases`, `FreeHitOnAllNoBalls`, `BouncersAllowedPerOver`, `MaxFieldersBehindSquareLeg`, `FollowOnLeadThreshold`, `AllowDeclarations`, `AllowSuperOverOnTie`, `ReviewsPerInnings`, `CrossOnCatchNewBatterTakesStrike`, etc.).
   - Standard presets (`CreateStandardT20`, `CreateStandardOdi`, `CreateStandardTest`) are factory configurations, not hardcoded conditionals inside the scoring engine.

3. **Separation of Official Rules Decisions from Approximate Simulation & Visual Effects**
   - **Approximate Simulation Evidence (`DeliveryTelemetry`, `ShotTelemetry`, `FieldingActionEvidence`):** Produced by physics/kinematics layers and passed into `ICricketRulesEngine` as read-only evidence.
   - **Official Rule Decisions (`BallEventRecord`, `DismissalRecord`, `MatchState`):** Produced solely by `CricketRulesEngine` under `CompetitionRulesProfile`.
   - **Visual Effects & Presentation (`BroadcastCue`, animations, VFX, SFX):** Purely downstream observers of official events.

4. **No Unverified Cross-Device Physics Determinism Claims**
   - Floating-point execution across ARM64 Android SoCs (IL2CPP) and x86_64 servers can diverge.
   - Cross-device determinism is treated as an empirical hypothesis to be benchmarked in Milestone 1. Online authoritative multiplayer relies on server-side execution of `Cricket.Simulation.Core` with tolerance-bounded reconciliation rather than assuming bit-identical client/server float execution without verification.

5. **CCD & Empirical Benchmarking Before Physics Step-Rate Commitment**
   - Milestone 1 will implement Swept-Sphere vs. Oriented Bounding Box Continuous Collision Detection (CCD) and benchmark candidate step rates (`120 Hz`, `240 Hz`, `480 Hz`) on representative Android hardware before locking the production sub-step frequency.

6. **Physics-Based DRS Simulation (No Proprietary Technology Claims)**
   - Decision Review System modules are modeled explicitly as internal physics-based trajectory projection (`BallTrackingSimulation`) and contact-impulse edge telemetry (`EdgeDetectionSimulation`), not reproductions of proprietary third-party broadcast tracking systems.

7. **Hardware-Scalable Android Presentation**
   - Crowds, shaders, player mesh LODs, animation update rates, and lighting cascades scale dynamically across Low, Medium, and High Android hardware tiers with thermal-throttling fallbacks.

8. **Live Commentary Decoupled from Gemini API Latency**
   - Real-time ball-by-ball commentary runs 100% locally via event-driven priority queues and local audio/text banks (`< 50 ms` trigger latency).
   - Optional Gemini API features are strictly asynchronous and non-blocking (e.g., post-match reports, career press summaries) with full offline fallbacks.

9. **Offline Single-Player First, Multiplayer-Compatible Pipeline**
   - `Cricket.Simulation.Core` runs completely offline with zero network dependencies.
   - `Cricket.Multiplayer.Shared` wraps gameplay actions in transport-agnostic commands (`IMatchTransport`, `LocalLoopbackTransport`), allowing offline play and future authoritative servers to share one validation and command pipeline.

10. **Licensed or Original Content Governance**
    - Player profiles, teams, competitions, commentary packs, and music carry explicit `ContentLicenseInfo` provenance metadata to guarantee all shipped assets are original or properly licensed.

---

## 2. File Placement Guide (Unity Project Root: `UnityProject/`)

| Path | Purpose | Unity Engine Dependency |
| :--- | :--- | :--- |
| `UnityProject/Packages/manifest.json` | Unity Package Manager dependencies | Unity Project Config |
| `UnityProject/ProjectSettings/ProjectVersion.txt` | Unity LTS editor version pin | Unity Project Config |
| `UnityProject/CricketSim.sln` | Standalone .NET/IDE solution for headless builds & NUnit tests | None |
| `UnityProject/Assets/_Project/Assemblies/Cricket.Simulation.Core/` | Pure C# rules, lifecycle FSM, event bus, data models, presentation contracts | **Forbidden (`noEngineReferences: true`)** |
| `UnityProject/Assets/_Project/Assemblies/Cricket.Multiplayer.Shared/` | Shared network command envelopes, validation, checksums, loopback transport | **Forbidden (`noEngineReferences: true`)** |
| `UnityProject/Assets/_Project/Tests/EditMode/` | Automated NUnit 3 verification suite | Editor/Test Framework only |
