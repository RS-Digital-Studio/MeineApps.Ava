using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Styling;

namespace HandwerkerImperium.Behaviors;

/// <summary>
/// Attached Behavior fuer Bottom-Sheet Slide-Up/Down Animation.
/// Nutzt TransformOperations mit translate (px-Einheiten!) fuer echtes Sliding.
///
/// EMPFEHLUNG: Fuer die beste Erfahrung CSS-Styles in der View verwenden:
///   Style Selector="Border.BottomSheet" → RenderTransform="translate(0px, 800px)"
///   Style Selector="Border.BottomSheet.Open" → RenderTransform="translate(0px, 0px)"
///   mit TransformOperationsTransition (300ms, CubicEaseOut)
///
/// Dieses Behavior ist fuer Faelle wo CSS-Styles nicht praktikabel sind.
/// </summary>
public static class BottomSheetBehavior
{
    /// <summary>
    /// Steuert ob das Bottom-Sheet sichtbar ist (slide up/down).
    /// </summary>
    public static readonly AttachedProperty<bool> IsOpenProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsOpen", typeof(BottomSheetBehavior), false);

    /// <summary>
    /// Slide-Distanz in Pixel (Standard: 800px).
    /// </summary>
    public static readonly AttachedProperty<double> SlideDistanceProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("SlideDistance", typeof(BottomSheetBehavior), 800.0);

    // Pro-Element CancellationToken — wenn der User waehrend einer laufenden
    // Animation neu toggled (Open→Close→Open in unter 300ms), wird die vorherige Animation
    // abgebrochen. Ohne Cancellation konnte ein laufender "Close"-Task das Element auf
    // IsVisible=false setzen, obwohl der User es gerade wieder geoeffnet hatte.
    private static readonly AttachedProperty<CancellationTokenSource?> AnimationCtsProperty =
        AvaloniaProperty.RegisterAttached<Control, CancellationTokenSource?>("AnimationCts", typeof(BottomSheetBehavior));

    public static bool GetIsOpen(Control element) => element.GetValue(IsOpenProperty);
    public static void SetIsOpen(Control element, bool value) => element.SetValue(IsOpenProperty, value);

    public static double GetSlideDistance(Control element) => element.GetValue(SlideDistanceProperty);
    public static void SetSlideDistance(Control element, double value) => element.SetValue(SlideDistanceProperty, value);

    static BottomSheetBehavior()
    {
        IsOpenProperty.Changed.AddClassHandler<Control>(OnIsOpenChanged);
    }

    private static async void OnIsOpenChanged(Control element, AvaloniaPropertyChangedEventArgs e)
        => await HandwerkerImperium.Helpers.AsyncExtensions.RunHandlerSafely(async () =>
        {
            // Vorherige Animation abbrechen — letzter Toggle gewinnt.
            var oldCts = element.GetValue(AnimationCtsProperty);
            oldCts?.Cancel();
            oldCts?.Dispose();

            var cts = new CancellationTokenSource();
            element.SetValue(AnimationCtsProperty, cts);
            var ct = cts.Token;

            var isOpen = (bool)e.NewValue!;
            var distance = GetSlideDistance(element);

            try
            {
                if (isOpen)
                {
                    // Startposition: Unterhalb des sichtbaren Bereichs (OHNE Transition)
                    element.Transitions = null;
                    element.RenderTransform = TransformOperations.Parse($"translate(0px, {distance}px)");
                    element.Opacity = 0;
                    element.IsVisible = true;

                    // Frame abwarten, damit Startposition gerendert wird
                    await Task.Delay(16, ct);

                    // Transition aktivieren
                    element.Transitions =
                    [
                        new TransformOperationsTransition
                        {
                            Property = Visual.RenderTransformProperty,
                            Duration = TimeSpan.FromMilliseconds(300),
                            Easing = new CubicEaseOut()
                        },

                        new DoubleTransition
                        {
                            Property = Visual.OpacityProperty,
                            Duration = TimeSpan.FromMilliseconds(200)
                        }
                    ];

                    // Zielposition → Transition animiert automatisch
                    element.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
                    element.Opacity = 1;
                }
                else
                {
                    // Transition sicherstellen fuer Slide-Down
                    element.Transitions ??=
                    [
                        new TransformOperationsTransition
                        {
                            Property = Visual.RenderTransformProperty,
                            Duration = TimeSpan.FromMilliseconds(250),
                            Easing = new CubicEaseIn()
                        },

                        new DoubleTransition
                        {
                            Property = Visual.OpacityProperty,
                            Duration = TimeSpan.FromMilliseconds(200)
                        }
                    ];

                    // Nach unten schieben
                    element.RenderTransform = TransformOperations.Parse($"translate(0px, {distance}px)");
                    element.Opacity = 0;

                    // Warten bis Animation fertig, dann ausblenden — nur wenn nicht
                    // zwischendurch ein neuer Toggle reinkam.
                    await Task.Delay(300, ct);
                    if (!ct.IsCancellationRequested)
                        element.IsVisible = false;
                }
            }
            catch (TaskCanceledException)
            {
                // Erwartet: neuer Toggle hat diesen Task gestoppt — Element-State wird vom
                // neuen Task richtig gesetzt. Nichts zu tun.
            }
        });
}
