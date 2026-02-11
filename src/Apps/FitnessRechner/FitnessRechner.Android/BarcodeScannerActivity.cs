using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using Java.Util.Concurrent;
using Xamarin.Google.MLKit.Vision.Barcode.Common;
using Xamarin.Google.MLKit.Vision.BarCode;

namespace FitnessRechner.Android;

/// <summary>
/// Native Barcode-Scanner-Activity mit CameraX + ML Kit.
/// Erkennt EAN-13, EAN-8, UPC-A, UPC-E Barcodes.
/// </summary>
[Activity(
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public class BarcodeScannerActivity : AndroidX.AppCompat.App.AppCompatActivity
{
    public const string EXTRA_BARCODE = "barcode_result";
    public const int REQUEST_CODE = 9001;

    private PreviewView? _previewView;
    private bool _barcodeDetected;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Programmatische UI erstellen
        var rootLayout = new RelativeLayout(this)
        {
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };

        // Kamera-Preview
        _previewView = new PreviewView(this)
        {
            LayoutParameters = new RelativeLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };
        rootLayout.AddView(_previewView);

        // Semi-transparentes Overlay mit Scan-Bereich
        var overlayView = new BarcodeScanOverlay(this);
        overlayView.LayoutParameters = new RelativeLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent);
        rootLayout.AddView(overlayView);

        // Zurück-Button (oben links)
        var backButton = new ImageButton(this);
        backButton.SetImageResource(global::Android.Resource.Drawable.IcMenuCloseClearCancel);
        backButton.SetBackgroundColor(Color.Transparent);
        backButton.SetColorFilter(Color.White);
        var backParams = new RelativeLayout.LayoutParams(
            (int)(48 * Resources!.DisplayMetrics!.Density),
            (int)(48 * Resources.DisplayMetrics.Density));
        backParams.AddRule(LayoutRules.AlignParentTop);
        backParams.AddRule(LayoutRules.AlignParentStart);
        backParams.TopMargin = (int)(16 * Resources.DisplayMetrics.Density);
        backParams.LeftMargin = (int)(16 * Resources.DisplayMetrics.Density);
        backButton.LayoutParameters = backParams;
        backButton.Click += (_, _) =>
        {
            SetResult(Result.Canceled);
            Finish();
        };
        rootLayout.AddView(backButton);

        // Hinweis-Text (unten)
        var hintText = new TextView(this)
        {
            Text = "Barcode in den Rahmen halten",
            TextSize = 16,
            Gravity = GravityFlags.Center
        };
        hintText.SetTextColor(Color.White);
        hintText.SetShadowLayer(4f, 0f, 0f, Color.Black);
        var hintParams = new RelativeLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        hintParams.AddRule(LayoutRules.AlignParentBottom);
        hintParams.BottomMargin = (int)(80 * Resources.DisplayMetrics.Density);
        hintText.LayoutParameters = hintParams;
        rootLayout.AddView(hintText);

        SetContentView(rootLayout);

        StartCamera();
    }

    private void StartCamera()
    {
        var cameraProviderFuture = ProcessCameraProvider.GetInstance(this);
        cameraProviderFuture!.AddListener(new Java.Lang.Runnable(() =>
        {
            try
            {
                var cameraProvider = (ProcessCameraProvider)cameraProviderFuture.Get()!;
                BindCameraUseCases(cameraProvider);
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error("BarcodeScanner", $"Kamera-Start fehlgeschlagen: {ex}");
                SetResult(Result.Canceled);
                Finish();
            }
        }), ContextCompat.GetMainExecutor(this)!);
    }

    private void BindCameraUseCases(ProcessCameraProvider cameraProvider)
    {
        // Preview
        var preview = new Preview.Builder()!
            .Build()!;
        var mainExecutor = ContextCompat.GetMainExecutor(this)!;
        preview.SetSurfaceProvider(mainExecutor, _previewView!.SurfaceProvider);

        // Image Analysis fuer Barcode-Erkennung
        var imageAnalysis = new ImageAnalysis.Builder()!
            .SetTargetResolution(new global::Android.Util.Size(1280, 720))!
            .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)!
            .Build()!;

        var executor = Executors.NewSingleThreadExecutor()!;
        imageAnalysis.SetAnalyzer(executor, new BarcodeAnalyzer(OnBarcodeFound));

        // Kamera binden
        var cameraSelector = CameraSelector.DefaultBackCamera!;

        try
        {
            cameraProvider.UnbindAll();
            cameraProvider.BindToLifecycle(this, cameraSelector, preview, imageAnalysis);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("BarcodeScanner", $"Kamera-Binding fehlgeschlagen: {ex}");
        }
    }

    private void OnBarcodeFound(string barcode)
    {
        if (_barcodeDetected) return;
        _barcodeDetected = true;

        RunOnUiThread(() =>
        {
            var resultIntent = new Intent();
            resultIntent.PutExtra(EXTRA_BARCODE, barcode);
            SetResult(Result.Ok, resultIntent);
            Finish();
        });
    }

    /// <summary>
    /// ML Kit Barcode-Analyzer fuer CameraX ImageAnalysis
    /// </summary>
    private class BarcodeAnalyzer : Java.Lang.Object, ImageAnalysis.IAnalyzer
    {
        private readonly Action<string> _onBarcodeFound;
        private readonly Xamarin.Google.MLKit.Vision.BarCode.IBarcodeScanner _scanner;

        public BarcodeAnalyzer(Action<string> onBarcodeFound)
        {
            _onBarcodeFound = onBarcodeFound;

            var options = new BarcodeScannerOptions.Builder()
                .SetBarcodeFormats(
                    Barcode.FormatEan13,
                    Barcode.FormatEan8,
                    Barcode.FormatUpcA,
                    Barcode.FormatUpcE)
                .Build();

            _scanner = BarcodeScanning.GetClient(options);
        }

        public void Analyze(IImageProxy? imageProxy)
        {
            if (imageProxy == null) return;

            var mediaImage = imageProxy.Image;
            if (mediaImage == null)
            {
                imageProxy.Close();
                return;
            }

            var inputImage = Xamarin.Google.MLKit.Vision.Common.InputImage
                .FromMediaImage(mediaImage, imageProxy.ImageInfo.RotationDegrees);

            _scanner.Process(inputImage)!
                .AddOnSuccessListener(new BarcodeSuccessListener(barcode =>
                {
                    _onBarcodeFound(barcode);
                }))!
                .AddOnCompleteListener(new BarcodeCompleteListener(() =>
                {
                    imageProxy.Close();
                }));
        }
    }

    /// <summary>
    /// Listener fuer erfolgreiche Barcode-Erkennung
    /// </summary>
    private class BarcodeSuccessListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnSuccessListener
    {
        private readonly Action<string> _onBarcode;

        public BarcodeSuccessListener(Action<string> onBarcode)
        {
            _onBarcode = onBarcode;
        }

        public void OnSuccess(Java.Lang.Object? result)
        {
            if (result is not Java.Util.IList barcodes) return;

            for (int i = 0; i < barcodes.Size(); i++)
            {
                var item = barcodes.Get(i);
                if (item is Barcode barcode && !string.IsNullOrEmpty(barcode.RawValue))
                {
                    _onBarcode(barcode.RawValue!);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Listener fuer Task-Abschluss (ImageProxy schliessen)
    /// </summary>
    private class BarcodeCompleteListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnCompleteListener
    {
        private readonly Action _onComplete;

        public BarcodeCompleteListener(Action onComplete)
        {
            _onComplete = onComplete;
        }

        public void OnComplete(global::Android.Gms.Tasks.Task task)
        {
            _onComplete();
        }
    }

    /// <summary>
    /// Semi-transparentes Overlay mit transparentem Scan-Bereich
    /// </summary>
    private class BarcodeScanOverlay : View
    {
        private readonly Paint _overlayPaint;
        private readonly Paint _borderPaint;

        public BarcodeScanOverlay(Context context) : base(context)
        {
            SetWillNotDraw(false);

            _overlayPaint = new Paint
            {
                Color = Color.Argb(120, 0, 0, 0)
            };

            _borderPaint = new Paint
            {
                Color = Color.Argb(200, 100, 181, 246), // Material Blue 300
                StrokeWidth = 4f,
            };
            _borderPaint.SetStyle(Paint.Style.Stroke);
        }

        protected override void OnDraw(Canvas? canvas)
        {
            base.OnDraw(canvas);
            if (canvas == null) return;

            var width = canvas.Width;
            var height = canvas.Height;

            // Scan-Bereich: 80% Breite, 30% Hoehe, zentriert
            var scanWidth = width * 0.8f;
            var scanHeight = height * 0.15f;
            var left = (width - scanWidth) / 2f;
            var top = (height - scanHeight) / 2f;

            // Dunkles Overlay zeichnen (4 Rechtecke um den Scan-Bereich)
            canvas.DrawRect(0, 0, width, top, _overlayPaint);
            canvas.DrawRect(0, top, left, top + scanHeight, _overlayPaint);
            canvas.DrawRect(left + scanWidth, top, width, top + scanHeight, _overlayPaint);
            canvas.DrawRect(0, top + scanHeight, width, height, _overlayPaint);

            // Scan-Bereich Rahmen
            canvas.DrawRect(left, top, left + scanWidth, top + scanHeight, _borderPaint);

            // Ecken-Akzente (Material Blue)
            var cornerLength = 40f;
            var cornerPaint = new Paint
            {
                Color = Color.Argb(255, 100, 181, 246),
                StrokeWidth = 6f,
                StrokeCap = Paint.Cap.Round
            };

            // Oben links
            canvas.DrawLine(left, top, left + cornerLength, top, cornerPaint);
            canvas.DrawLine(left, top, left, top + cornerLength, cornerPaint);
            // Oben rechts
            canvas.DrawLine(left + scanWidth - cornerLength, top, left + scanWidth, top, cornerPaint);
            canvas.DrawLine(left + scanWidth, top, left + scanWidth, top + cornerLength, cornerPaint);
            // Unten links
            canvas.DrawLine(left, top + scanHeight, left + cornerLength, top + scanHeight, cornerPaint);
            canvas.DrawLine(left, top + scanHeight - cornerLength, left, top + scanHeight, cornerPaint);
            // Unten rechts
            canvas.DrawLine(left + scanWidth - cornerLength, top + scanHeight, left + scanWidth, top + scanHeight, cornerPaint);
            canvas.DrawLine(left + scanWidth, top + scanHeight - cornerLength, left + scanWidth, top + scanHeight, cornerPaint);
        }
    }
}
