# Behaviors — Xaml.Behaviors.Avalonia

App-eigene Behaviors für wiederkehrende UI-Patterns.

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `BottomSheetBehavior.cs` | Animiert ein Panel-Element als Bottom-Sheet (Slide-Up/Slide-Down via CSS `translate(0px, 400px)` → `translate(0px, 0px)` mit CubicEaseOut). Nutzt `TransformOperationsTransition` |

---

## Gotcha — translate() braucht px-Einheiten

```axaml
<!-- FALSCH: -->
<Setter Property="RenderTransform" Value="translate(0, 400)" />

<!-- RICHTIG: -->
<Setter Property="RenderTransform" Value="translate(0px, 400px)" />
```

Avalonia 12 wirft eine Exception wenn `translate()` ohne `px`-Einheiten angegeben wird.

## Gotcha — TransformOperationsTransition braucht initialen Wert

`TransformOperationsTransition` für `RenderTransform` braucht IMMER einen initialen
`RenderTransform="scale(1)"` Wert auf dem Control, sonst crasht die `null → scale()`
Transition auf manchen Android-GPU-Treibern.
