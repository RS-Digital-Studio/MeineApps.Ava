# SETUP.md — HandwerkerImperium-Unity First-Time-Setup

> **Schritt-für-Schritt-Anleitung für die erstmalige Einrichtung.**
> Geschätzte Dauer: 4-6 Stunden (Unity + Firebase + Tools + KI-Pipeline).
> Voraussetzung: Windows 10/11, RTX 3090+ (für KI-Pipeline), 64 GB RAM empfohlen.

---

## Inhaltsverzeichnis

1. [Pre-Setup-Checkliste](#1-pre-setup-checkliste)
2. [Unity & Tools installieren](#2-unity--tools-installieren)
3. [Repository klonen & vorbereiten](#3-repository-klonen--vorbereiten)
4. [Unity-Projekt anlegen](#4-unity-projekt-anlegen)
5. [Packages installieren](#5-packages-installieren)
6. [Firebase einrichten](#6-firebase-einrichten)
7. [Play Store + AdMob Konten](#7-play-store--admob-konten)
8. [KI-Asset-Pipeline einrichten](#8-ki-asset-pipeline-einrichten)
9. [ElevenLabs Standard-Voice Setup](#9-elevenlabs-standard-voice-setup)
10. [First-Boot-Test](#10-first-boot-test)
11. [Build Android Dev](#11-build-android-dev)
12. [Troubleshooting](#12-troubleshooting)
13. [Setup-Checkliste (alle Schritte)](#13-setup-checkliste-alle-schritte)

---

## 1. Pre-Setup-Checkliste

### 1.1 Hardware

| Komponente | Mindest | Empfohlen |
|-----------|---------|-----------|
| **GPU** | RTX 3090 (24 GB VRAM) | RTX 4090 / 5090 (24-32 GB) |
| **CPU** | 8 Cores | 16 Cores |
| **RAM** | 32 GB | 64 GB |
| **SSD** | 1 TB NVMe (freier Platz: 500 GB) | 2 TB NVMe |
| **Internet** | 50 Mbps Download (für Modell-Downloads) | 100 Mbps+ |

### 1.2 Bestehende Accounts (vorausgesetzt)

- Google-Konto (für Play Store + Firebase + AdMob) — gleicher Account wie die Avalonia-Apps
- Apple-Konto (Phase 2, optional)
- GitHub-Konto (für CI/CD später)
- **Adobe Creative Cloud** (Substance 3D Sampler + Painter) — Subscription kaufen falls nicht vorhanden
- **ElevenLabs Pro** — Subscription kaufen
- **Rodin Gen-2.5** (Hyper3D, optional für Hero-Assets) — Free-Tier reicht erstmal

### 1.3 Vorbereitete Dateien

- Keystore: `F:\Meine_Apps_Ava\Releases\meineapps.keystore` (gleicher wie alle Avalonia-Apps, Alias `meineapps`)
- Style-Reference-Bilder (15-20 Stück für LoRA-Training) — werden in Phase 1 Woche 8 generiert
- **Standard-Voice statt eigener Aufnahme** — vorgefertigte ElevenLabs-Library-Voice (siehe § 9)

### 1.4 Empfohlene zusätzliche Software

- **Git** (mit Git LFS für große ScriptableObjects/Assets)
- **GitHub Desktop** oder **GitKraken** (visual Git)
- **VS Code** (für Cloud-Functions in TypeScript)
- **Notepad++** oder **Sublime Text** (für JSON/USS/UXML)
- **Audacity** (Audio-Schnitt/-Normalisierung der generierten Voice-Lines)
- **Discord** (für Beta-Tester-Community später)

---

## 2. Unity & Tools installieren

### 2.1 Unity Hub & Editor

```powershell
# 1. Unity Hub herunterladen und installieren
# https://unity.com/download

# 2. In Unity Hub:
# - Installs → Install Editor → Unity 6000.4.8f1 (LTS)
# - Modules wählen:
#   ☑ Android Build Support
#     ☑ OpenJDK
#     ☑ Android SDK & NDK Tools
#   ☑ Documentation
#   ☐ iOS Build Support (Phase 2)
#   ☐ WebGL Build Support
#   ☑ Windows Build Support (IL2CPP) [für Desktop-Testing]
```

**Wichtig:** Die Unity-Version **6000.4.8f1** muss exakt diese sein (gleiche wie ArcaneKingdom — Engine-Patches geteilt).

### 2.2 Visual Studio 2022 Community + Workloads

```
Visual Studio Installer:
  ☑ .NET desktop development
  ☑ Game development with Unity
  ☑ Desktop development with C++ (für IL2CPP)
  ☑ MSBuild Build Tools
```

### 2.3 JetBrains Rider (Alternative IDE)

Falls Rider bevorzugt:
- Lizenz: Educational (kostenlos) oder Personal (€)
- Better Unity-Support als VS in vielen Bereichen

### 2.4 Git LFS

```powershell
# Git LFS installieren
git lfs install

# Im Repo-Root (Workspace bereits vorhanden)
cd F:\Meine_Apps_Ava
# .gitattributes konfigurieren falls nötig
```

### 2.5 Python 3.12 (für KI-Pipeline)

```powershell
# Python 3.12.x installieren (für ComfyUI)
# https://www.python.org/downloads/

# Verify:
python --version
# Sollte 3.12.x ausgeben
```

### 2.6 CUDA Toolkit 12.4

```powershell
# CUDA Toolkit 12.4 von NVIDIA
# https://developer.nvidia.com/cuda-12-4-0-download-archive
```

### 2.7 Blender 4.3+

```powershell
# Blender Free
# https://www.blender.org/download/
```

### 2.8 Adobe Creative Cloud

- Substance 3D Sampler 4.4 (für Image-to-Material, Decals)
- Substance 3D Painter (für Hand-Polish)

### 2.9 Cascadeur (optional, für Animation-Polish)

- **Free-Indie** unter $100k Revenue
- https://cascadeur.com/

---

## 3. Repository klonen & vorbereiten

### 3.1 Repository verifizieren

Der Workspace liegt bereits unter `F:\Meine_Apps_Ava\`, das Unity-Projekt unter
`src/Apps/HandwerkerImperium.Unity/`.

```powershell
# Verifizieren:
ls F:\Meine_Apps_Ava\src\Apps\HandwerkerImperium.Unity\

# Sollte zeigen:
# - README.md, PLAN.md, DESIGN.md, CLAUDE.md, ARCHITECTURE.md
# - ROADMAP.md, ASSETS_AI.md, SETUP.md (diese Datei)
# - PLAN_ABGLEICH_ORIGINAL.md, ORIGINAL_WERTE.md
```

### 3.2 Branch wechseln

Unity-Arbeit läuft auf einem eigenen Branch parallel zur produktiven Avalonia-Version (siehe
CLAUDE.md § 18.1):

```powershell
git checkout -b unity-main
git push -u origin unity-main
```

### 3.3 .gitignore + .gitattributes überprüfen

Stelle sicher, dass folgende Unity-Pfade gitignored sind:
```
src/Apps/HandwerkerImperium.Unity/Unity/Library/
src/Apps/HandwerkerImperium.Unity/Unity/Temp/
src/Apps/HandwerkerImperium.Unity/Unity/Logs/
src/Apps/HandwerkerImperium.Unity/Unity/UserSettings/
src/Apps/HandwerkerImperium.Unity/Unity/MemoryCaptures/
*.tmp
*.user
*.userprefs
```

Git LFS für große Asset-Dateien:
```
*.fbx filter=lfs diff=lfs merge=lfs -text
*.glb filter=lfs diff=lfs merge=lfs -text
*.png filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
*.unity3d filter=lfs diff=lfs merge=lfs -text
*.bundle filter=lfs diff=lfs merge=lfs -text
```

---

## 4. Unity-Projekt anlegen

### 4.1 Neues Projekt erstellen

1. Unity Hub öffnen
2. **New Project**
3. **Template:** Universal 3D (URP) — wichtig für 2D + 3D
4. **Project Name:** `HandwerkerImperium`
5. **Location:** `F:\Meine_Apps_Ava\src\Apps\HandwerkerImperium.Unity\Unity\`
6. **Create**

Unity öffnet → erste Initialisierung dauert ~5-10 min.

### 4.2 Project Settings konfigurieren

**Edit → Project Settings:**

#### Player

- **Company Name:** Robert Schneider
- **Product Name:** HandwerkerImperium
- **Bundle Identifier (Android):** `com.meineapps.handwerkerimperium2.beta` (für Beta)
- **Version:** 0.1.0
- **Bundle Version Code:** 1
- **Scripting Backend:** **IL2CPP** (für Release-Builds)
- **API Compatibility Level:** .NET Standard 2.1
- **Target Architectures:** **ARM64** only
- **Target SDK Version:** Automatic (highest)
- **Minimum API Level:** 24 (Android 7+)
- **Internet Access:** Required
- **Write Permission:** External (SDCard)

#### Quality

- 3 Quality-Levels anlegen: Low / Medium / High
- Default für Mobile: Medium

#### Graphics

- **Scriptable Render Pipeline Asset:** URP-HighFidelity (default) oder eigene anlegen

### 4.3 Build Settings

**File → Build Settings:**

- **Platform:** Android (Switch Platform)
- **Texture Compression:** ASTC (Android-Default)
- **Build App Bundle (Google Play):** ☑ (AAB statt APK)
- **Scenes In Build:** (kommt in Woche 1)

### 4.4 Erste Asset-Ordner-Struktur

Lege folgende Ordner unter `Assets/` an:

```
Assets/
└── _Project/                    ← Unser Code (Underscore = sortiert oben)
    ├── Scripts/
    │   ├── Bootstrap/
    │   ├── Core/
    │   ├── Domain/
    │   ├── Game/
    │   ├── UI/
    │   ├── Editor/
    │   └── Tests/
    ├── ScriptableObjects/
    ├── Scenes/
    ├── Prefabs/
    ├── Art/
    ├── Audio/
    ├── Addressables/
    └── Resources/  (NUR Bootstrap!)
```

---

## 5. Packages installieren

> **Single-Source-of-Truth für Versionen:** [CLAUDE.md § 2 (Tech-Stack)](CLAUDE.md). Die hier
> genannten Versionen müssen mit CLAUDE.md § 2 übereinstimmen — bei Abweichung gilt CLAUDE.md.

### 5.1 Über Package Manager UI

**Window → Package Manager:**

- Universal RP (built-in, 17.0.4)
- Input System 1.19.0 (Package Registry)
- Addressables 2.9.1
- Localization 1.5.11
- TextMeshPro (in Unity 6 Teil von uGUI `com.unity.ugui` — KEIN eigenes Paket; Essentials via `Window → TextMeshPro → Import TMP Essential Resources`)
- Timeline 1.8.12
- Cinemachine 3.x (`com.unity.cinemachine`, Unity-6-Default; API-inkompatibel zu 2.10)
- Mobile Notifications 2.4.3
- Test Framework 1.5.1

### 5.2 Über manifest.json (Custom Packages)

Editiere `Unity/Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10",
    "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.16.9",
    "com.unity.nuget.newtonsoft-json": "3.2.2",
    "com.unity.render-pipelines.universal": "17.0.4",
    "com.unity.inputsystem": "1.19.0",
    "com.unity.addressables": "2.9.1",
    "com.unity.localization": "1.5.11",
    "com.unity.timeline": "1.8.12",
    "com.unity.mobile.notifications": "2.4.3",
    "com.unity.test-framework": "1.5.1",
    "com.unity.cinemachine": "3.1.0"
  }
}
```

> **Kein `com.unity.textmeshpro`-Pin:** In Unity 6 ist TextMesh Pro Teil von uGUI
> (`com.unity.ugui`, built-in) — das frühere Standalone-Paket `com.unity.textmeshpro` 3.0.6 ist
> nicht mehr installierbar. TMP-Essentials werden einmalig per
> `Window → TextMeshPro → Import TMP Essential Resources` importiert (§ 5.1), nicht via Manifest.
> Cinemachine ist auf 3.x (`com.unity.cinemachine`) gepinnt — Unity 6 liefert Cinemachine 3.x
> standardmäßig; die alte 2.10er-Linie ist API-inkompatibel und hier nicht zu verwenden.

### 5.3 DOTween (über Unity Asset Store)

- Window → Asset Store → DOTween Pro (~17€) oder DOTween Free
- Import nach `Assets/ThirdParty/DOTween/`

### 5.4 Firebase Unity SDK

```
1. Download Firebase Unity SDK von https://firebase.google.com/download/unity
2. Importiere folgende .unitypackages:
   - FirebaseApp.unitypackage
   - FirebaseAuth.unitypackage
   - FirebaseDatabase.unitypackage
   - FirebaseFunctions.unitypackage
   - FirebaseMessaging.unitypackage
   - FirebaseRemoteConfig.unitypackage
   - FirebaseAnalytics.unitypackage
   - FirebaseCrashlytics.unitypackage
```

### 5.5 Google Mobile Ads Unity Plugin

- Download von https://github.com/googleads/googleads-mobile-unity/releases
- Import als .unitypackage

### 5.6 Google Play Billing Plugin

- Über Asset Store: "Google Play Billing Library Unity Plugin"

---

## 6. Firebase einrichten

### 6.1 Firebase-Projekt erstellen

1. https://console.firebase.google.com/
2. **Add Project**
3. **Project Name:** `handwerkerimperium2-beta`
4. **Google Analytics:** Enable (mit eigenem Account)
5. **Create**

### 6.2 Android-App registrieren

1. **Project Overview → Add App → Android**
2. **Android Package Name:** `com.meineapps.handwerkerimperium2.beta`
3. **App Nickname:** `HandwerkerImperium-Unity-Beta`
4. **SHA-1:** Aus Keystore extrahieren:
   ```powershell
   keytool -list -v -keystore F:\Meine_Apps_Ava\Releases\meineapps.keystore -alias meineapps
   # Password: MeineApps2025
   ```
5. **Register App**
6. **Download `google-services.json`** → ablegen unter `Unity/Assets/StreamingAssets/` (Unity-Firebase-Plugin liest es von dort)

### 6.3 Firebase-Produkte aktivieren

In Firebase Console:

- **Authentication:** Enable Anonymous + Google Sign-In
- **Realtime Database:** Create Database (Region: europe-west1)
- **Cloud Functions:** Enable (Blaze-Plan erforderlich)
- **Cloud Messaging:** Enable
- **Remote Config:** Enable
- **Analytics:** Enable (war im Step 6.1 schon enabled)
- **Crashlytics:** Enable

### 6.4 Realtime Database Rules

```json
{
  "rules": {
    ".read": false,
    ".write": false,
    "auth_to_player": {
      "$uid": {
        ".read": "auth.uid === $uid",
        ".write": "auth.uid === $uid"
      }
    },
    "cloud_saves": {
      "$playerId": {
        ".read": "auth != null && root.child('auth_to_player').child(auth.uid).val() === $playerId",
        ".write": "auth != null && root.child('auth_to_player').child(auth.uid).val() === $playerId"
      }
    },
    "guilds": {
      ".indexOn": ["tag", "lastActive"],
      "$guildId": {
        ".read": "auth != null",
        ".write": "auth != null"
      }
    }
  }
}
```

Vollständige Rules: aus der produktiven Avalonia-Version portieren — Quelle ist die Workspace-Root-Datei
`F:\Meine_Apps_Ava\database.rules.json` (gilt aktuell nur für HandwerkerImperium). Ziel im Unity-Projekt:
`Server/DatabaseRules/database.rules.json` (siehe CLAUDE.md § 13.3).

### 6.5 Cloud Functions Setup

```powershell
# Firebase CLI installieren
npm install -g firebase-tools

# Login
firebase login

# Im Projekt-Root des Server-Folders:
cd src/Apps/HandwerkerImperium.Unity/Server/CloudFunctions
firebase init functions
# - Use existing project: handwerkerimperium2-beta
# - Language: TypeScript
# - ESLint: Yes
# - Install dependencies: Yes
```

---

## 7. Play Store + AdMob Konten

### 7.1 Google Play Console

1. https://play.google.com/console/
2. **Create App**
   - **App Name:** HandwerkerImperium Beta (Unity)
   - **Default Language:** Deutsch
   - **App or Game:** Game
   - **Free or Paid:** Free
3. **Setup Required Items:**
   - Privacy Policy URL
   - App Access (Closed Testing)
   - Ads (Yes — AdMob)
   - Target Audience (13+)
   - News App (No)
   - Data Safety
   - Content Rating

### 7.2 AdMob Setup

1. https://admob.google.com/
2. **Add App → Already published: No → Manually add**
3. **App Name:** HandwerkerImperium Beta (Unity)
4. **Platform:** Android
5. **App Store:** Google Play
6. **Ad Units anlegen (13 Rewarded-Placements, siehe DESIGN.md § 29.3):**
   - golden_screws
   - shop_reward
   - score_double
   - market_refresh
   - workshop_speedup
   - workshop_unlock
   - worker_hire_bonus
   - research_speedup
   - daily_challenge_retry
   - achievement_boost
   - offline_double
   - rush_boost
   - lucky_spin

**App-ID** und alle Ad-Unit-IDs in `BalancingConfig.asset` → `AdMobConfig` Section eintragen.

### 7.3 Google Play Billing Setup

> **Inhalte/Preise nicht hier pflegen** — maßgeblich ist [DESIGN.md § 29 (Monetarisierung)](DESIGN.md):
> § 29.1 Imperium-Pass (Premium), § 29.2 IAP-Bundles, § 29.5 Daily-Bundle. Hier nur die Play-Console-Anlage.

1. Play Console → App → Monetization → Products
2. **Premium (Non-Consumable):**
   - Imperium-Pass, 4,99 € Lifetime (Effekte → DESIGN.md § 29.1)
3. **Whale-Bundles (Consumable, 3 Stück — Inhalte → DESIGN.md § 29.2):**
   - Mid (9,99 €), Big (19,99 €), Mega (49,99 €)
4. **Battle-Pass-Saison (Consumable):**
   - `battle_pass_season` — eigener SKU pro Saison (einziger im Original-Code fest belegter SKU,
     `BattlePassService.UpgradeToPremiumAsync`; DESIGN.md § 21.8)
5. **Daily-Bundle (Consumable, RemoteConfig-getrieben):**
   - 7 Slot-SKUs, befüllt über `monetization.daily_bundle_skus` (DESIGN.md § 29.5) — KEIN Hardcoded-Default
6. **Subscriptions:** keine

**Hinweis zu SKU-IDs:** Außer `battle_pass_season` sind die konkreten Produkt-IDs für Imperium-Pass und
Bundles im Original-Avalonia-Code nicht fest verdrahtet — finale IDs beim Anlegen in der Play Console
festlegen und mit der Avalonia-Version abgleichen, nicht erfinden.

---

## 8. KI-Asset-Pipeline einrichten

> **Vollständige Spec:** [ASSETS_AI.md](ASSETS_AI.md). Hier nur Quickstart.

### 8.1 ComfyUI installieren

```powershell
# In F:\ (oder anderer großer SSD)
cd F:\
git clone https://github.com/comfyanonymous/ComfyUI
cd ComfyUI

# Python venv erstellen
python -m venv venv
.\venv\Scripts\Activate.ps1

# Dependencies
pip install -r requirements.txt
pip install torch==2.5.1 --index-url https://download.pytorch.org/whl/cu124

# Erstmal starten
python main.py
# → öffnet ComfyUI Web-UI unter http://127.0.0.1:8188
```

### 8.2 ComfyUI-3D-Pack installieren

```powershell
cd F:\ComfyUI\custom_nodes\
git clone https://github.com/MrForExample/ComfyUI-3D-Pack
cd ComfyUI-3D-Pack
python install.py
```

### 8.3 Modelle herunterladen

Lege folgende Modelle in `F:\ComfyUI\models\` ab:

| Modell | Größe | Ablage |
|--------|-------|--------|
| **TRELLIS 2 (image-large)** | ~5 GB | `models/TRELLIS/` |
| **SPAR3D** | ~2 GB | `models/SPAR3D/` |
| **Stable Fast 3D** | ~1.5 GB | `models/SF3D/` |
| **TripoSG** | ~3 GB | `models/TripoSG/` |
| **InstantMesh** | ~1 GB | `models/InstantMesh/` |
| **SDXL 1.0 base + refiner** | ~13 GB | `models/checkpoints/` |
| **Flux.1-dev** (optional, intern) | ~24 GB | `models/checkpoints/` |

**Quellen:** Siehe ASSETS_AI.md § 3 (URLs verifiziert Mai 2026).

### 8.4 Workflow-Ablage anlegen

```powershell
mkdir F:\AI\ComfyUI_workflows\handwerkerimperium_unity
mkdir F:\AI\ComfyUI_workflows\handwerkerimperium_unity\00_style_reference
mkdir F:\AI\ComfyUI_workflows\handwerkerimperium_unity\01_concept_2d
mkdir F:\AI\ComfyUI_workflows\handwerkerimperium_unity\02_image_to_3d
mkdir F:\AI\ComfyUI_workflows\handwerkerimperium_unity\03_texture_decals
mkdir F:\AI\ComfyUI_workflows\handwerkerimperium_unity\04_audio

mkdir F:\AI\3d_output\handwerkerimperium_unity
mkdir F:\AI\audio_output\handwerkerimperium_unity
mkdir F:\AI\Licenses\handwerkerimperium_unity
mkdir F:\AI\Blender\scripts
```

### 8.5 Blender-Skripte vorbereiten

Lege Skripte aus ASSETS_AI.md § 7 in `F:\AI\Blender\scripts\`:
- `hwi_unity_batch_cleanup.py`
- `hwi_unity_workshop_modular.py`

(Die Skripte werden in Woche 4 implementiert; erstmal nur Ordner anlegen.)

### 8.6 Kohya_ss für Style-LoRA-Training

```powershell
cd F:\AI\
git clone https://github.com/bmaltais/kohya_ss
cd kohya_ss
.\setup.bat
```

Style-LoRA-Training-Setup in Phase 1 Woche 7.

---

## 9. ElevenLabs Standard-Voice Setup

> **Strategie-Update Mai 2026:** Statt Voice-Cloning mit eigener Aufnahme nutzen wir eine **vorgefertigte ElevenLabs-Standard-Voice** aus der Library. Vorteile: schneller, einfacher, keine rechtlichen Risiken, gleiche Stimme in allen 6 Sprachen via Multilingual v2.

### 9.1 ElevenLabs Account

1. https://elevenlabs.io/
2. **Pro Subscription** (~22 €/Monat)
3. **API Key** generieren in Profile → API Keys
4. **API Key sicher ablegen:** `setx ELEVENLABS_API_KEY "your_key"` (Windows-User-Env-Var)
5. **Lizenz-PDF** speichern unter `F:\AI\Licenses\handwerkerimperium_unity\2026-XX-XX_elevenlabs_pro_terms.pdf`

### 9.2 Voice aus Library auswählen

1. ElevenLabs Dashboard → **Voice Library**
2. **Filter:** "Male", "Middle-aged" oder "Older", "Warm", "Multilingual"
3. **Top-Kandidaten anhören** (3-5 Stück) — Beispiele:
   - "Adam" (warm, älter, multilingual-fähig)
   - "Bill" (freundlich-mittelalt)
   - "Daniel" (deutsch-gut)
4. **Test pro Sprache:** Jede Sprache mit einem Beispielsatz aus DESIGN.md durchhören
5. **Beste Voice fixieren:** Voice-ID notieren in `F:\AI\ComfyUI_workflows\handwerkerimperium_unity\04_audio\voice_config.json`:
   ```json
   {
     "voice_id": "selected_voice_id_here",
     "voice_name": "Meister Hans (basiert auf ElevenLabs Voice 'XYZ')",
     "model_id": "eleven_multilingual_v2",
     "voice_settings": {
       "stability": 0.5,
       "similarity_boost": 0.75,
       "style": 0.3
     }
   }
   ```

### 9.3 API-Test mit gewählter Voice

```python
# Test-Skript: F:\AI\ComfyUI_workflows\handwerkerimperium_unity\04_audio\test_voice.py
import requests
import os
import json
from pathlib import Path

# Config laden
config_path = Path("F:/AI/ComfyUI_workflows/handwerkerimperium_unity/04_audio/voice_config.json")
config = json.loads(config_path.read_text())

API_KEY = os.environ["ELEVENLABS_API_KEY"]
VOICE_ID = config["voice_id"]

# Test-Sätze pro Sprache
test_lines = {
    "de": "Hallo, ich bin Meister Hans. Willkommen in meiner Werkstatt!",
    "en": "Hello, I'm Master Hans. Welcome to my workshop!",
    "es": "¡Hola, soy Maestro Hans. ¡Bienvenido a mi taller!",
    "fr": "Bonjour, je suis Maître Hans. Bienvenue dans mon atelier !",
    "it": "Ciao, sono Maestro Hans. Benvenuto nella mia officina!",
    "pt": "Olá, eu sou Mestre Hans. Bem-vindo à minha oficina!"
}

output_dir = Path("F:/AI/audio_output/handwerkerimperium_unity/voice_meister_hans/_pilot/")
output_dir.mkdir(parents=True, exist_ok=True)

for lang, text in test_lines.items():
    response = requests.post(
        f"https://api.elevenlabs.io/v1/text-to-speech/{VOICE_ID}",
        headers={"xi-api-key": API_KEY, "Content-Type": "application/json"},
        json={
            "text": text,
            "model_id": config["model_id"],
            "voice_settings": config["voice_settings"]
        }
    )

    out_path = output_dir / f"meister_hans_pilot_{lang}.mp3"
    out_path.write_bytes(response.content)
    print(f"Generated: {out_path.name}")

print("Pilot-Voice-Generation complete!")
```

### 9.4 Pilot-Hörtest

1. Skript ausführen: `python test_voice.py`
2. Alle 6 MP3-Dateien anhören (DE/EN/ES/FR/IT/PT)
3. **Akzeptanz-Kriterien:**
   - Verständlich
   - Konsistent (gleiche Stimme, gleicher Charakter)
   - Klingt nach "warm, freundlicher Meister"
4. **Falls eine Sprache schlecht klingt:** Voice-Settings anpassen (stability/style/similarity_boost) oder andere Voice wählen
5. **Bei OK:** Voice-ID final fixieren in voice_config.json

### 9.5 Batch-Generation-Pipeline

Phase 1 Woche 35-36: Vollständige Batch-Generation aller 1500 Voice-Lines.

**Quelle:** Unity Localization String-Tables → CSV-Export für Voice-Generation.

**Skript:** `F:\AI\ComfyUI_workflows\handwerkerimperium_unity\04_audio\generate_voice_batch.py` (wird in Phase 1 Woche 35 geschrieben).

**Rate-Limits beachten:** ElevenLabs Pro hat 500.000 chars/month — 1500 Lines × ~50 chars = 75k chars → eine Pro-Sub-Monat reicht für komplette Generation.

---

## 10. First-Boot-Test

### 10.1 Unity öffnen + Initial Build

1. Unity Hub → HandwerkerImperium-Projekt öffnen
2. Edit → Project Settings → Quality → Mobile-Default = Medium
3. File → Save Project

### 10.2 Sample Boot-Scene

```
1. New Scene erstellen: Boot.unity unter Assets/_Project/Scenes/
2. Im Scene-Hierarchy: GameObject "RootLifetimeScope" anlegen
3. (RootLifetimeScope-Komponente kommt in Woche 5 mit VContainer-Setup)
```

### 10.3 Build Smoke-Test

```
File → Build Settings
- Add Open Scene (Boot.unity)
- Build → Output: Releases/HWI-Unity-smoke-test.apk
- Sollte ein leerer APK sein, der startet und ein leeres Fenster zeigt
```

---

## 11. Build Android Dev

### 11.1 Keystore konfigurieren

```
Project Settings → Player → Android → Publishing Settings:
☑ Custom Keystore
Keystore Path: ..\..\..\..\Releases\meineapps.keystore
Keystore Pass: MeineApps2025
Key Alias: meineapps
Key Pass: MeineApps2025
```

### 11.2 IL2CPP konfigurieren

```
Player → Other Settings → Configuration:
- Scripting Backend: IL2CPP
- Api Compatibility Level: .NET Standard 2.1
- IL2CPP Code Generation: Faster (smaller) builds
- Target Architectures: ARM64 only
- Strip Engine Code: ☑
```

### 11.3 Erste Build

```
File → Build Settings → Build
Output: Releases/HandwerkerImperium-Unity-v0.1.0.aab
```

Erste IL2CPP-Build dauert **~15-30 min** (danach inkrementell viel schneller).

### 11.4 Auf Gerät testen

```powershell
# Per ADB
adb install Releases/HandwerkerImperium-Unity-v0.1.0.aab
# Bei AAB: erst zu APK konvertieren via bundletool
```

Oder direkt über Unity → Build And Run.

---

## 12. Troubleshooting

### 12.1 Häufige Probleme

| Problem | Lösung |
|---------|--------|
| **Unity Build dauert ewig** | Cache Library/ behalten, nur Re-Import bei Bedarf |
| **VContainer-AOT-Fehler** | `link.xml` korrekt setzen, `[Preserve]`-Attribute |
| **Firebase Build-Fehler** | google-services.json korrekt in `Assets/StreamingAssets/` |
| **IL2CPP-Strip entfernt Code** | `[Preserve]`-Attribute oder `link.xml` ergänzen |
| **TextMeshPro CJK fehlt** | TMP Essential Resources importieren, Dynamic Font einrichten |
| **Addressables-Fehler** | Build Catalog explizit (`Addressables → Build → New Build`) |
| **ComfyUI Modell lädt nicht** | Verify CUDA 12.4 + PyTorch 2.5.1 + RAM-Check (16+ GB VRAM für TRELLIS 2) |
| **TRELLIS 2 OOM** | Kleinere Bilder (1024² statt 2048²), Memory-Fragmentierung-Cleanup |
| **ElevenLabs Voice klingt schlecht** | Voice-Settings anpassen (stability/style/similarity_boost) oder andere Library-Voice wählen (§ 9.4) |
| **Firebase RTDB schreibt nicht** | Rules + indexOn korrekt setzen, im Console-Logs prüfen |
| **Mixamo Auto-Rig fail bei stylized Worker** | Proportionen näher am Standard halten, AccuRIG 2 als Fallback |

### 12.2 Performance-Optimierungen (falls FPS < 60)

- Quality Setting auf **Low**: Texture-Resolution 0.5x, kein Post-FX
- Mesh-Compression auf **High**
- Particle-Pool reduzieren (60% statt 100%)
- LOD-Groups korrekt setup
- Addressables-Memory: max 50 Assets gleichzeitig

### 12.3 Logging & Diagnose

- **Unity Editor:** Console-Window
- **Android:** `adb logcat -s Unity`
- **Firebase:** Firebase Console → Crashlytics + Analytics
- **Performance:** Unity Profiler (Window → Analysis → Profiler)

---

## 13. Setup-Checkliste (alle Schritte)

### 13.1 Hardware & Software (Tag 1)

- [ ] Unity Hub installiert
- [ ] Unity 6000.4.8f1 LTS installiert (mit Android Build Support)
- [ ] Visual Studio 2022 oder JetBrains Rider
- [ ] Python 3.12 installiert
- [ ] CUDA Toolkit 12.4 installiert
- [ ] Blender 4.3+ installiert
- [ ] Adobe Creative Cloud (Substance 3D Sampler + Painter) — optional erstmal
- [ ] Git LFS installiert

### 13.2 Repository (Tag 1)

- [ ] Repository unter `F:\Meine_Apps_Ava\` verfügbar
- [ ] Branch `unity-main` erstellt
- [ ] .gitignore + .gitattributes für Unity konfiguriert

### 13.3 Unity-Projekt (Tag 1)

- [ ] Projekt unter `src/Apps/HandwerkerImperium.Unity/Unity/` angelegt
- [ ] Project Settings konfiguriert (Player, Quality, Graphics, Build)
- [ ] Asset-Ordner-Struktur angelegt
- [ ] Packages installiert (siehe § 5)
- [ ] Keystore-Pfad konfiguriert (Player → Publishing)

### 13.4 Firebase (Tag 2)

- [ ] Firebase-Projekt `handwerkerimperium2-beta` erstellt
- [ ] Android-App `com.meineapps.handwerkerimperium2.beta` registriert
- [ ] google-services.json in StreamingAssets/
- [ ] Authentication (Anonymous + Google) aktiviert
- [ ] Realtime Database erstellt (europe-west1)
- [ ] Database Rules deployed (aus Avalonia portiert)
- [ ] Cloud Functions Setup (`firebase init functions`)
- [ ] Crashlytics + Analytics + Remote Config aktiviert

### 13.5 Play Store + AdMob (Tag 2)

- [ ] Play Console App erstellt (Beta)
- [ ] AdMob App + 13 Rewarded-Ad-Units angelegt (DESIGN.md § 29.3)
- [ ] In-App Products angelegt: Imperium-Pass + 3 Bundles + `battle_pass_season` + Daily-Bundle-Slots (DESIGN.md § 29)

### 13.6 KI-Pipeline (Tag 3-4)

- [ ] ComfyUI in `F:\ComfyUI\` installiert
- [ ] ComfyUI-3D-Pack installiert
- [ ] TRELLIS 2, SPAR3D, SF3D, TripoSG, InstantMesh Modelle heruntergeladen
- [ ] SDXL 1.0 base + refiner heruntergeladen
- [ ] Workflow-Ablage `F:\AI\ComfyUI_workflows\handwerkerimperium_unity\` angelegt
- [ ] Blender-Template + Scripts vorbereitet
- [ ] Kohya_ss installiert (für späteres LoRA-Training)
- [ ] Erste Test-Generation läuft (z.B. SDXL → Image)

### 13.7 ElevenLabs (Tag 4)

- [ ] ElevenLabs Pro Subscription aktiv
- [ ] API Key generiert + als Env-Var gesetzt (`ELEVENLABS_API_KEY`)
- [ ] Voice aus Library ausgewählt (Voice-ID + Settings in `voice_config.json`)
- [ ] Pilot-Test mit 6 Sprachen erfolgreich (Hörtest bestanden)
- [ ] Pro-Sub-Lizenz-PDF archiviert

### 13.8 First Build (Tag 5)

- [ ] Erste Boot-Scene (leer) erstellt
- [ ] IL2CPP-Build erfolgreich
- [ ] APK auf Android-Gerät getestet
- [ ] Crashlytics zeigt ersten Connection-Event

### 13.9 Doku-Anchor (Tag 5)

- [ ] Alle Doku-Dateien gelesen:
  - [ ] README.md
  - [ ] PLAN.md
  - [ ] DESIGN.md (korrigiertes GDD — verbindliche Werte)
  - [ ] CLAUDE.md
  - [ ] ARCHITECTURE.md
  - [ ] ROADMAP.md
  - [ ] ASSETS_AI.md
  - [ ] ORIGINAL_WERTE.md (echte Werte aus dem Avalonia-Code)
  - [ ] PLAN_ABGLEICH_ORIGINAL.md (Abgleich Plan ↔ Original)
- [ ] Designentscheidungen verstanden
- [ ] **Grundsatz verinnerlicht:** Die Unity-Version ist GENAU DASSELBE SPIEL wie das produktive
      Avalonia-Original (gleiche Mechaniken, Formeln, Balancing) — nur in 3D besser präsentiert.
      Jede mechanische/Balancing-Abweichung ist ein Fehler.

### 13.10 Bereitstellung für Phase 1 (Woche 1 starten)

- [ ] 7 Assembly-Definitions können angelegt werden
- [ ] VContainer-Setup-Plan klar
- [ ] First-Time-Setup-Wizard-Editor-Tool kann geschrieben werden

---

## 14. Anhang: Wichtige URLs

### Tools (Verified Mai 2026)

- **Unity:** https://unity.com/download
- **Visual Studio:** https://visualstudio.microsoft.com/
- **Rider:** https://www.jetbrains.com/rider/
- **Python:** https://www.python.org/downloads/
- **CUDA Toolkit 12.4:** https://developer.nvidia.com/cuda-12-4-0-download-archive
- **Blender:** https://www.blender.org/
- **ComfyUI:** https://github.com/comfyanonymous/ComfyUI
- **ComfyUI-3D-Pack:** https://github.com/MrForExample/ComfyUI-3D-Pack
- **TRELLIS 2:** https://github.com/microsoft/TRELLIS.2
- **SPAR3D:** https://github.com/Stability-AI/stable-point-aware-3d
- **TripoSG:** https://github.com/VAST-AI-Research/TripoSG
- **Kohya_ss:** https://github.com/bmaltais/kohya_ss
- **Mixamo:** https://www.mixamo.com/
- **Cascadeur:** https://cascadeur.com/
- **Adobe Substance:** https://www.adobe.com/products/substance3d.html
- **ElevenLabs:** https://elevenlabs.io/

### Services

- **Firebase Console:** https://console.firebase.google.com/
- **Google Play Console:** https://play.google.com/console/
- **AdMob:** https://admob.google.com/
- **Stable Audio 3:** https://stableaudio.com/
- **Rodin (Hyper3D):** https://hyper3d.io/
- **Meshy:** https://www.meshy.ai/

### Doku

- **Unity Manual:** https://docs.unity3d.com/Manual/index.html
- **Unity API:** https://docs.unity3d.com/ScriptReference/
- **VContainer:** https://vcontainer.hadashikick.jp/
- **UniTask:** https://github.com/Cysharp/UniTask
- **Firebase Unity:** https://firebase.google.com/docs/unity/setup

---

## 15. Nächste Schritte nach Setup

1. Setup abgeschlossen
2. [ROADMAP.md](ROADMAP.md) → Phase 1, Woche 1 starten
3. Parallel: KI-Pilot-Assets gemäß [ASSETS_AI.md § 15 (Pilot-Plan)](ASSETS_AI.md) starten
4. Erste Style-Reference-Bilder generieren (Woche 7-8)

**Setup abgeschlossen — bereit zur Implementation.**
