using Avalonia.Controls;
using Avalonia.Labs.Controls;
using BomberBlast.Graphics;
using BomberBlast.Models.Entities;

namespace BomberBlast.Views;

public partial class HelpView : UserControl
{
    private bool _canvasesBound;

    public HelpView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // OnLoaded kann bei Re-Attach erneut feuern — PaintSurface nur einmal binden,
        // sonst stapeln sich die Handler (mehrfaches Rendern + Leak).
        if (_canvasesBound) return;
        _canvasesBound = true;

        // PowerUp-Icons registrieren
        BindPowerUpCanvas(PuBombUp, PowerUpType.BombUp);
        BindPowerUpCanvas(PuFire, PowerUpType.Fire);
        BindPowerUpCanvas(PuSpeed, PowerUpType.Speed);
        BindPowerUpCanvas(PuWallpass, PowerUpType.Wallpass);
        BindPowerUpCanvas(PuDetonator, PowerUpType.Detonator);
        BindPowerUpCanvas(PuBombpass, PowerUpType.Bombpass);
        BindPowerUpCanvas(PuFlamepass, PowerUpType.Flamepass);
        BindPowerUpCanvas(PuMystery, PowerUpType.Mystery);
        BindPowerUpCanvas(PuKick, PowerUpType.Kick);
        BindPowerUpCanvas(PuLineBomb, PowerUpType.LineBomb);
        BindPowerUpCanvas(PuPowerBomb, PowerUpType.PowerBomb);
        BindPowerUpCanvas(PuSkull, PowerUpType.Skull);

        // Gegner-Icons registrieren
        BindEnemyCanvas(EnBallom, EnemyType.Ballom);
        BindEnemyCanvas(EnOnil, EnemyType.Onil);
        BindEnemyCanvas(EnDoll, EnemyType.Doll);
        BindEnemyCanvas(EnMinvo, EnemyType.Minvo);
        BindEnemyCanvas(EnKondoria, EnemyType.Kondoria);
        BindEnemyCanvas(EnOvapi, EnemyType.Ovapi);
        BindEnemyCanvas(EnPass, EnemyType.Pass);
        BindEnemyCanvas(EnPontan, EnemyType.Pontan);
    }

    /// <summary>
    /// PaintSurface-Handler für PowerUp-SKCanvasView binden.
    /// </summary>
    private static void BindPowerUpCanvas(SKCanvasView canvas, PowerUpType type)
    {
        if (canvas == null) return;
        canvas.PaintSurface += (_, args) =>
        {
            var skCanvas = args.Surface.Canvas;
            skCanvas.Clear();
            var bounds = skCanvas.LocalClipBounds;
            float cx = bounds.MidX;
            float cy = bounds.MidY;
            float size = Math.Min(bounds.Width, bounds.Height);
            HelpIconRenderer.DrawPowerUp(skCanvas, cx, cy, size, type);
        };
    }

    /// <summary>
    /// PaintSurface-Handler für Gegner-SKCanvasView binden.
    /// </summary>
    private static void BindEnemyCanvas(SKCanvasView canvas, EnemyType type)
    {
        if (canvas == null) return;
        canvas.PaintSurface += (_, args) =>
        {
            var skCanvas = args.Surface.Canvas;
            skCanvas.Clear();
            var bounds = skCanvas.LocalClipBounds;
            float cx = bounds.MidX;
            float cy = bounds.MidY;
            float size = Math.Min(bounds.Width, bounds.Height);
            HelpIconRenderer.DrawEnemy(skCanvas, cx, cy, size, type);
        };
    }
}
