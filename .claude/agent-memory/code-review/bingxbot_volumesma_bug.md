---
name: BingXBot Volume-SMA Bug
description: CalculateSma berechnet Close-Preis-SMA, wird aber in 4 Strategien als Volume-Durchschnitt verwendet - Volume-Filter komplett wirkungslos
type: project
---

IndicatorHelper.CalculateSma() benutzt ToQuotes().GetSma() was den SMA auf dem Close-Preis berechnet (Skender.Stock.Indicators Default). 4 von 6 Strategien vergleichen das Ergebnis mit candles[^1].Volume - Aepfel mit Birnen.

**Why:** Skender.Stock.Indicators berechnet SMA immer auf dem Close-Preis des Quote-Objekts. Es gibt keine Moeglichkeit, ein anderes Feld zu waehlen. Fuer Volume-SMA muss eine eigene Methode erstellt werden die Volume als Close-Feld setzt.

**How to apply:** Eigene CalculateVolumeSma-Methode im IndicatorHelper. Betrifft: TrendFollowStrategy (Zeile 69+127+166), EmaCrossStrategy (48+73), RsiStrategy (52+65), BollingerStrategy (45+90). GridStrategy und MacdStrategy sind nicht betroffen (kein Volume-Check).
