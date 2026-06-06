# Services/Anker — Echte Anker-Cloud-Live-Watt-Anbindung

Liest die Live-Solar-Eingangsleistung der Anker-Powerstation (C2000 Gen 2 = **A1783**) über die
**inoffizielle** Anker-Power-Cloud + MQTT. Reverse-Engineering-Quelle: `github.com/thomluther/anker-solix-api`
(Python). Plattformneutral (MQTTnet + `System.Security.Cryptography`) → läuft auf Android **und** Desktop.
Service-Conventions → [../CLAUDE.md](../CLAUDE.md).

---

## Bausteine

| Datei | Zweck |
|-------|-------|
| `AnkerCrypto` | Login-Krypto: ECDH (NIST P-256) gegen fest hinterlegten Server-Public-Key → 32-Byte-Raw-Secret (`DeriveRawSecretAgreement`) → AES-256-CBC (Key = Secret, IV = Secret[0..16], PKCS7, Base64). Plus `GToken` (MD5-Hex des user_id) und `TimezoneHeader`. |
| `AnkerHexFrame` | Proprietärer Binär-Frame (base64 in der MQTT-Message, **nicht** verschlüsselt): `TryDecode` (TLV-Parser) + `A1783DcInputWatts`/`A1783BatterySoc` + `BuildRealtimeTrigger` (msgtype 0057 inkl. XOR-Checksumme). |
| `AnkerCloudClient` | HTTPS: `passport/login` → `get_relate_and_bind_devices` → `get_user_mqtt_info`. Regionaler Host (EU/COM nach Ländercode). |
| `AnkerMonitorService` | `IAnkerMonitorService`-Impl: orchestriert Login → MQTT (mTLS) → Subscribe → Trigger → Decode. Demo-Fallback (`MockAnkerMonitorService`) ohne Zugangsdaten. |
| `AnkerCredentialStore` | Liest/schreibt E-Mail/Passwort/Land im `IPreferencesService` (einziger Ort der Pref-Keys). |

`AnkerCloudModels.cs` (in `Models/`): `AnkerCredentials`, `AnkerSession`, `AnkerDevice`, `AnkerMqttInfo`.

---

## Ablauf (AnkerMonitorService.ConnectAsync)

1. Zugangsdaten aus `AnkerCredentialStore`. **Keine** → Demo-Modus (`IsSimulated = true`, Watt aus Sonnenstand).
2. `AnkerCloudClient.LoginAsync` (ECDH/AES-Passwort) → `GetDevicesAsync` → `GetMqttInfoAsync`.
3. Client-Zertifikat aus PEM (`X509Certificate2.CreateFromPem` + **Pkcs12-Roundtrip** → privater Schlüssel beim TLS-Handshake nutzbar).
4. MQTTnet v5: `WithTcpServer(endpoint, 8883)` + `WithTlsOptions` (Client-Cert + Server-Validierung gegen Amazon-Root-CA), **kein** WebSocket/SigV4.
5. Pro Gerät `dt/{app_name}/{pn}/{sn}/#` abonnieren, Realtime-Trigger an `cmd/{app_name}/{pn}/{sn}/req` (alle 30 s).
6. `OnMqttMessage`: äußeres JSON → `payload`-String-JSON → `data` (base64) → `AnkerHexFrame.TryDecode` → A1783 `a6/04` = DC-Eingang (W) → `SampleReceived`.

---

## Gotchas / Fallstricke

- **Google-OAuth-Account hat KEIN API-Passwort.** Die Cloud kennt nur E-Mail+Passwort (kein OAuth-Pfad).
  Lösung des Nutzers: in der Anker-App per „Passwort vergessen" mit der Gmail-Adresse ein Passwort setzen
  (oder dediziertes Zweitkonto + System teilen). Die UI weist über `AnkerHintGoogle` darauf hin.
- **`a6` ist ein TLV-Tag, kein fester Byte-Offset.** Beim A1783 ist Sub-Offset `04` = `dc_input_power_total`
  (int16 signed LE, **Solar + 12V-Auto-Laden kombiniert** — kein reines PV-Feld). Bedeutung von `a6` ist
  **modellspezifisch** → nur für `A1783`/`A1785` (PPS) anwenden, sonst falsche Werte.
- **mTLS, nicht SigV4.** Anker frontet AWS-IoT-Core per eigener DNS; Authentifizierung über Client-Zertifikat
  auf Port 8883. Kein AWSSDK, kein SigV4-URL-Signing nötig.
- **Nur 1 aktives Token/Account** (vor Anker-App 3.10): paralleler App-Login kickt das API-Token. Ggf. Zweitkonto.
- **Kein Refresh-Token** — bei Ablauf (<60 s Restlaufzeit) vollständiges Re-Login.
- **Realtime-Trigger ist Best-Effort.** Schlägt der 0057-Frame fehl, kommen weiterhin (langsamere) 0900-Status.
- **Release-Härtung (offen):** `JsonSerializer.Serialize(new {…})` im Trigger nutzt Reflection — bei aggressivem
  Trimming/Native-AOT prüfen (Debug-Deploy unkritisch; aktuell keine IL-Warnung).
- **Inoffiziell** — Anker kann den Zugang jederzeit ändern/kappen; firmwareabhängig.

---

## Verifikation

Krypto-Struktur + Frame-Encode/Decode sind unit-getestet
([tests/SunSeeker.Tests/AnkerCryptoTests.cs](../../../../../../tests/SunSeeker.Tests/AnkerCryptoTests.cs),
`AnkerHexFrameTests.cs`). Der HTTP-/MQTT-Live-Pfad wird am Gerät verifiziert (Account + C2000 nötig).
