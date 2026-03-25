# GardenControl - Hardware & Setup

## Systemübersicht

```
[Solarpanel 50W 12V] → [Laderegler 10A] → [12V AGM-Akku 12Ah]
                                                │
                                        [Buck-Converter 5V 5A]
                                                │
                                        [Raspberry Pi 5 + 7" Display]
                                                ├── I2C → [ADS1115 ADC]
                                                │              ├── A0 → Sensor 1 (10m Kabel)
                                                │              ├── A1 → Sensor 2 (10m Kabel)
                                                │              ├── A2 → Sensor 3 (10m Kabel)
                                                │              └── A3 → (frei, Erweiterung)
                                                │
                                                └── GPIO → [4-Kanal 5V Relais (ALAMSCN)]
                                                              ├── Kanal 1 → 12V Magnetventil Beet 1
                                                              ├── Kanal 2 → 12V Magnetventil Beet 2
                                                              ├── Kanal 3 → 12V Magnetventil Beet 3
                                                              └── Kanal 4 → 12V Tauchpumpe
```

**Ein Raspberry Pi 5 = Server + Controller + Display-App**
- ASP.NET Core Server (API + SignalR)
- Avalonia Desktop-App auf dem 7"-Touchscreen (Fullscreen/Kiosk)
- GPIO-Steuerung (Relais)
- I2C Sensor-Auslesen (ADS1115)
- Handy-App verbindet sich über WLAN

---

## Vorhandene Hardware

| Artikel | Quelle |
|---------|--------|
| Raspberry Pi 5 (8GB) | [Amazon](https://www.amazon.de/dp/B0CRPF47RG) |
| 7" Display | [Amazon](https://www.amazon.de/dp/B0B9M5SCG4) |
| 4-Kanal 5V Relais (ALAMSCN) | Im Kit enthalten |
| 4x Bodenfeuchtesensoren (resistiv) | Im Kit enthalten (Upgrade auf kapazitiv empfohlen) |
| 4x Mini-Pumpen | Im Kit enthalten (zu schwach für 10m, nur für Indoor-Test) |
| 3m PVC-Schlauch + Dupont-Drähte | Im Kit enthalten |

---

## Einkaufsliste (was noch fehlt)

### Elektronik

| Artikel | Link | ca. Preis |
|---------|------|-----------|
| ADS1115 16-Bit ADC Modul (I2C) | [AZDelivery ADS1115](https://www.amazon.de/AZDelivery-ADS1115-Kan%C3%A4le-Arduino-Raspberry/dp/B07PXFD3BH) | ~7 EUR |
| DC-DC Buck Converter 12V→5V 5A | [Greluma 12V→5V USB-C](https://www.amazon.de/Greluma-Wasserdichter-Konverter-Step-Down-Modul-Netzteil-kompatibel/dp/B0BX3MQ18D) | ~8 EUR |

### Stromversorgung (Solar)

| Artikel | Link | ca. Preis |
|---------|------|-----------|
| Solarpanel 50W 12V monokristallin | [Collect Light 50W](https://www.amazon.de/Collect-Light-Solarpanel-monokristallin-Photovoltaik/dp/B0CGJ1W9W1) | ~30 EUR |
| Solar-Laderegler PWM 10A 12V/24V | [OSXCAUES 10A PWM](https://www.amazon.de/OSXCAUES-Automatische-Identifizierung-Solarverbinder-Schraubenschl%C3%BCssel/dp/B09W9FQ8HX) | ~12 EUR |
| 12V AGM-Akku 12Ah wartungsfrei | [Blei-Gel-Akku 12V 12Ah](https://www.amazon.de/Blei-Gel-Akku-Akkubatterie-Versorgungsbatterie-Ersatzakku-Wartungsfrei/dp/B0CWHBZWKN) | ~22 EUR |

### Pumpe + Ventile

| Artikel | Link | ca. Preis |
|---------|------|-----------|
| 12V DC Tauchpumpe (5m Förderhöhe, 600+ L/h) | [Aideepen 12V 600L/h](https://www.amazon.de/Aideepen-Tauchpumpe-Wasserpumpe-Gartenbew%C3%A4sserung-Campinggarten/dp/B09TB9HGBR) | ~12 EUR |
| 3x 12V Magnetventil 1/2" NC Messing | [EXLECO 12V NC G1/2"](https://www.amazon.de/EXLECO-Elektromagnetventil-Wassereinlass-Magnetventil-Geschlossenes/dp/B085BYC9X2) (3x bestellen) | ~24 EUR |

### Kabel + Schlauch

| Artikel | Link | ca. Preis |
|---------|------|-----------|
| 15m CAT7 Outdoor-Kabel (Sensorkabel) | [CSL 15m CAT7 Outdoor](https://www.amazon.de/CSL-Netzwerkkabel-Patchkabel-Abriebfest-%C3%B6lbest%C3%A4ndig/dp/B08JQFJYZH) | ~15 EUR |
| 15m Tropfschlauch 1/2" | [Bradas Tropfschlauch 15m](https://www.amazon.de/Tropfschlauch-Aqua-Drop-15m-Perlschlauch-Gartenschlauch/dp/B00GSPSYUO) | ~15 EUR |

### Gehäuse + Schutz

| Artikel | Link | ca. Preis |
|---------|------|-----------|
| IP65 Gehäuse 230x150x85mm | [Projektschutzbox IP65](https://www.amazon.de/Geh%C3%A4use-Projektkasten-Elektronisches-Anschlussdosen-Projektschutzbox-DIY-Anschlussdose/dp/B098L8G9PK) | ~10 EUR |
| Kabelverschraubungen Set IP68 M12-M25 | [ARLI 40er Set](https://www.amazon.de/Kabelverschraubungen-Kabelverschraubung-Verschraubung-wasserdichte-ARLI/dp/B06XWY6BR8) | ~10 EUR |

### Empfohlenes Upgrade

| Artikel | Link | ca. Preis |
|---------|------|-----------|
| 4x Kapazitive Bodenfeuchtesensoren (korrosionsfrei) | [APKLVSR 5er-Pack](https://www.amazon.de/APKLVSR-Bodenfeuchtesensor-Kapazitive-Hygrometer-Feuchtigkeitssensor/dp/B0CQNF7S7L) | ~10 EUR |

### Gesamtkosten (zusätzlich zur vorhandenen Hardware)

**ca. 175 EUR**

---

## Verkabelung

### GPIO-Belegung (Raspberry Pi 5, BCM-Nummern)

| GPIO | Pin# | Funktion |
|------|------|----------|
| GPIO 2 | Pin 3 | I2C SDA → ADS1115 SDA |
| GPIO 3 | Pin 5 | I2C SCL → ADS1115 SCL |
| GPIO 17 | Pin 11 | Relais Kanal 1 → Ventil Beet 1 |
| GPIO 27 | Pin 13 | Relais Kanal 2 → Ventil Beet 2 |
| GPIO 22 | Pin 15 | Relais Kanal 3 → Ventil Beet 3 |
| GPIO 23 | Pin 16 | Relais Kanal 4 → Pumpe |
| 3.3V | Pin 1 | VCC für ADS1115 + Sensoren |
| 5V | Pin 2 | VCC für Relais-Modul |
| GND | Pin 6 | Gemeinsame Masse |

### Sensorkabel (CAT7, 10m zu den Beeten)

CAT7 hat 8 Drähte (4 Adernpaare). Belegung:

```
Paar 1 (orange): VCC 3.3V (gemeinsam für alle Sensoren)
Paar 2 (grün):   GND (gemeinsam)
Paar 3 (blau):   Sensor 1 Signal → ADS1115 A0
                  Sensor 2 Signal → ADS1115 A1
Paar 4 (braun):  Sensor 3 Signal → ADS1115 A2
                  Reserve (Sensor 4 oder Temperatur)
```

### 12V Aktoren (vom Akku über Relais)

```
12V Akku (+) ── Sicherung 10A ── Relais COM
                                    ├── NO 1 → Ventil 1 (+)
                                    ├── NO 2 → Ventil 2 (+)
                                    ├── NO 3 → Ventil 3 (+)
                                    └── NO 4 → Pumpe (+)

12V Akku (-) ── Ventil 1/2/3 (-), Pumpe (-)
```

---

## Solar-Berechnung

| Parameter | Wert |
|-----------|------|
| Pi 5 Verbrauch (mit Display) | ~7-10W |
| Tagesverbrauch | ~200 Wh (10W × 20h, Display-Standby nachts) |
| Solarpanel 50W × 4h Sonne | ~200 Wh/Tag |
| Akku 12V × 12Ah | 144 Wh → ~14h Backup ohne Sonne |
| Sicherheitsfaktor | Reicht für 1+ Tage Bewölkung |

**Tipp**: Display per Software nach 5 Minuten Inaktivität abschalten → Verbrauch sinkt auf ~4W → Akku hält >1.5 Tage ohne Sonne.

---

## Aufbau

1. **Gehäuse vorbereiten**: Kabelverschraubungen einbauen, Pi + Relais + ADS1115 montieren
2. **Solarpanel aufstellen**: Südausrichtung, 30-45° Neigung, mit Akku + Laderegler verbinden
3. **Pi anschließen**: Buck-Converter 12V→5V an Akku, Pi über USB-C versorgen
4. **Sensorkabel verlegen**: CAT7 zu den Beeten (10m), Sensoren anschließen
5. **Wassersystem**: Pumpe ins Reservoir, Schlauch zu den Ventilen, Tropfschlauch zu den Beeten
6. **Software flashen**: `install-pi5.sh` ausführen (siehe SETUP.md)
7. **Kalibrierung**: Über die App Trocken-/Nass-Werte setzen
