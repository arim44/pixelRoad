# Contest Compliance Review

## Current status

The official contest name, rules, submission guide, and judging-environment requirements are not present in this repository. Compliance therefore cannot be confirmed yet. Obtain the official rules or an authoritative URL/PDF before selecting a production map provider or making the live map an internet-only feature.

This document is a review gate, not a statement that the project is already compliant.

## Map implementation decision

- **2026-08-08 change: the static PNG fallback was removed and the live vector map is now the only map surface.** `mapImageResourcePath` was deleted from `map_config.json`, and `PixelRoadApp` no longer loads a map texture. This was an explicit product decision and it **reopens** the offline-demo gate recorded below.
  - When the live map is unavailable, the app starts, shows an on-screen reason, logs an error, and keeps the codex and unlock logic usable, but no map and no spot markers are drawn.
  - The release gates were intentionally left untouched: a non-development build still needs both `allowLiveVectorMapInRelease: true` and the `PIXELROAD_LIVE_VECTOR_MAP` scripting symbol, so an offline-review APK now has no map at all.
  - Before submission, either approve a live provider for release use or re-introduce an offline map source.
- A provider-swappable, Shortbread-compatible live MVT path is implemented for technical validation. The current development configuration points to OSM Shortbread and is used to test viewport tile selection, vector rendering, caching, and optional pixel output. It is not the approved contest submission provider.
- The submission provider, endpoint, service plan, and permitted cache policy remain undecided until the official contest rules and the provider terms have been reviewed together.
- The MVT/PBF decoder, mesh builder, viewport selector, and cache are project source code and add no new runtime package. The data provider still must pass the gates below before it is enabled in a submission release.
- `allowLiveVectorMapInRelease` is `false`. Editor and development builds may validate live tiles, while a non-development build compiles out the requester unless `PIXELROAD_LIVE_VECTOR_MAP` is explicitly defined. Both gates require approval before a live submission.
- ~~The app must retain a static PNG fallback so that it can still demonstrate its core flow when the judging environment is offline, the provider is unavailable, or contest rules prohibit an external service.~~ **Superseded on 2026-08-08 and unresolved.** The fallback no longer exists, so an offline judging environment currently sees no map. Re-adding an offline map source or approving a live provider for release remains an open submission blocker.

## Mandatory review gates

All applicable gates must have written evidence and an owner before the final submission build is produced.

### 1. Official contest rules

- Record the exact contest name, organizer, rule version, publication URL, and retrieval date.
- Confirm participant and team eligibility, permitted pre-existing work, ownership terms, and judging criteria.
- Confirm whether an internet-dependent demo, paid service, third-party backend, or account login is allowed.
- Record every required deliverable, format, deadline, size limit, and supported device/OS requirement.

Status: **Blocked — official source not present.**

### 2. External API and internet access

- Confirm that the chosen provider permits mobile-app use, contest demonstrations, distribution of the submitted binary, and the intended memory/disk caching behavior.
- Confirm request quotas, rate limits, attribution requirements, retention limits, geographic coverage, and expected service availability.
- Use HTTPS and avoid embedding an unrestricted secret in the client. Document any app/package restrictions, proxy, or key-rotation process.
- Fetch only viewport-required data, cancel stale requests, cap retries, and provide clear loading/error states.
- Test the complete judging flow with no network and with provider errors. The static PNG fallback was removed on 2026-08-08, so this test currently only verifies that the app degrades cleanly (notice shown, codex usable, no crash) rather than that a map is still shown.
- Do not use `tile.openstreetmap.org` or Overpass as a drag-driven production tile backend.

Status: **Pending — submission provider is not selected.**

### 3. Open-source software and third-party assets

- Inventory every submitted package, font, image, dataset, shader, and future vector-tile decoder/renderer.
- Verify that each license permits the planned source submission, binary distribution, modification, and contest use.
- Include required license texts, copyright notices, attribution, and source-offer obligations in the repository and submission package as applicable.
- Remove development-only packages and tools from the submission when they are not required to build or run the entry.
- Update `docs/THIRD_PARTY_LICENSES.md` and `docs/DATA_SOURCES.md` whenever a provider, dataset, or library is selected.

Status: **Pending final dependency and asset audit.**

### 4. AI-assisted development

- Confirm whether code, art, writing, or design created with generative AI or coding agents is allowed.
- Confirm whether prompts, tool names, generated portions, human review, or modification history must be disclosed.
- Keep a development record sufficient to produce the required disclosure without claiming that unreviewed generated work is original human work.
- Audit development-only AI integrations before submission and exclude them from the runtime/submission when they are not required.

Status: **Blocked — the official AI-use policy is unknown.**

### 5. Location information and privacy

- Confirm whether the contest or target distribution channel imposes a privacy notice, consent, retention, or deletion requirement.
- Request only the minimum foreground location permissions needed for the demonstrated feature. Background location must not be added without a separately reviewed requirement and user benefit.
- Explain why location is used, support permission denial, and avoid storing precise location longer than necessary.
- Treat tile requests as potentially revealing the viewed area, especially while the map follows GPS. Document what is sent to the provider and reflect it in the privacy notice.
- Verify that analytics or diagnostics do not transmit precise location unintentionally.

Status: **Pending privacy and final-permission review.**

### 6. Submission artifact

- Confirm APK/AAB/source/video/presentation requirements, maximum sizes, signing rules, target API, supported architectures, and device orientation.
- Replace placeholder product/company/package metadata and use an intentional release version.
- Build and test a release artifact from a clean checkout, including a fresh dependency restore.
- Exclude debug symbols, editor caches, secrets, provider credentials, backup folders, and files marked `DoNotShip`.
- Test installation, first launch, permission denial, offline fallback, slow network, rapid drag/zoom, and repeated smooth/pixel-mode switching on the target device.
- Record the final artifact checksum and the exact source revision used to build it.

Status: **Pending official submission guide, final signing/version decision, and target-device validation.**

### 7. OSM and provider attribution

- Show `© OpenStreetMap contributors` in a visible, accessible place on the map experience.
- Add every attribution string required by the selected tile provider, renderer, style, and derived dataset.
- Keep the attribution readable in both smooth and pixel modes and while the static fallback is active.
- Record the data source, provider, endpoint or dataset version, access/generation date, style, coverage, and cache policy in `docs/DATA_SOURCES.md`.
- Record the corresponding license and notice text in `docs/THIRD_PARTY_LICENSES.md`.

Status: **Partially implemented — the OSM attribution is visible outside the pixelated map layer and links to the OSM copyright page; final-provider notices remain pending.**

## Technical verification record (2026-08-07)

- Unity EditMode: 25/25 offline tests passed for WebMercator coordinates, visible-tile selection, cache integrity/expiry/LRU, HTTPS/template policy, MVT decoding, label filtering, and mesh validity.
- Opt-in live PlayMode: 1/1 passed against one current Shortbread viewport. It rendered a tile, switched a 640×480 smooth RenderTexture to a 160×120 point-sampled pixel RenderTexture, and kept attribution outside the pixel layer.
- Android IL2CPP development build: succeeded for OpenGLES3 and Vulkan. APK size is 51,423,036 bytes, SHA-256 is `F57E2437B6B5F000D563006C48240FEF5AEF3421354EB1D55BFAB45360250FCA`, package id is `com.pixelroad.app`, min SDK is 25, and target SDK is 36.
- The development APK declares `INTERNET`, `ACCESS_FINE_LOCATION`, and `ACCESS_COARSE_LOCATION`; no background-location permission is present.
- Android IL2CPP offline-review build: succeeded with the live requester compiled out. APK size is 40,263,395 bytes and SHA-256 is `8E0ED96AF63ECBC4AE16D6C161E052DB9B92107D138012BCB42CE0DA05E7D4EA`.
- The merged offline-review manifest was inspected with Android build tools: package id is `com.pixelroad.app`, min SDK is 25, target SDK is 36, and `INTERNET` is absent. It retains foreground `ACCESS_FINE_LOCATION` and `ACCESS_COARSE_LOCATION` plus Android's app-scoped dynamic-receiver permission.
- The connected Android device was not authorized for ADB, so installation, frame-time, memory, permission-denial, and physical-device visual checks remain pending.
- `com.coplaydev.unity-mcp` is still a development package referenced from a Git `main` branch. Pin or remove it for the final source/submission after reviewing the contest's AI-tool and dependency rules.

These results establish technical viability only. They do not resolve the blocked official-rule, final-provider, privacy-notice, or submission-artifact approvals above.

## Approval record

Before submission, replace each blocked or pending status above with the evidence reviewed, reviewer, decision date, and any required implementation action. A successful technical test with OSM Shortbread does not by itself approve that source or any provider for the contest submission.
