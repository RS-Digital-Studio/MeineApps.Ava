using Android.Opengl;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Rendert den ARCore-Kamera-Hintergrund als Fullscreen-Quad via OpenGL ES 2.0.
/// ARCore schreibt die Kamerabilder auf eine GL_TEXTURE_EXTERNAL_OES Textur.
/// Dieser Renderer zeichnet diese Textur als bildschirmfuellenden Quad.
/// </summary>
public sealed class ArBackgroundRenderer : IDisposable
{
    private int _programId;
    private int _positionHandle;
    private int _texCoordHandle;

    // Gecachte ByteBuffer/FloatBuffer (vermeidet Native Memory Leak bei 30fps Allokation)
    private Java.Nio.ByteBuffer? _texCoordByteBuffer;
    private Java.Nio.FloatBuffer? _texCoordFloatBuffer;

    // Fullscreen-Quad (2 Dreiecke, NDC -1..1)
    private static readonly float[] QuadVertices =
    [
        -1.0f, -1.0f,   // unten links
         1.0f, -1.0f,   // unten rechts
        -1.0f,  1.0f,   // oben links
         1.0f,  1.0f,   // oben rechts
    ];

    // Textur-Koordinaten (werden von ARCore per Frame angepasst)
    private float[] _texCoords =
    [
        0.0f, 1.0f,     // unten links
        1.0f, 1.0f,     // unten rechts
        0.0f, 0.0f,     // oben links
        1.0f, 0.0f,     // oben rechts
    ];

    // Vertex Shader: Gibt Position und Texturkoordinate durch
    private const string VertexShaderSource = @"
        attribute vec4 a_Position;
        attribute vec2 a_TexCoord;
        varying vec2 v_TexCoord;
        void main() {
            gl_Position = a_Position;
            v_TexCoord = a_TexCoord;
        }";

    // Fragment Shader: Sampelt von der externen OES-Textur (Kamera)
    private const string FragmentShaderSource = @"
        #extension GL_OES_EGL_image_external : require
        precision mediump float;
        varying vec2 v_TexCoord;
        uniform samplerExternalOES u_Texture;
        void main() {
            gl_FragColor = texture2D(u_Texture, v_TexCoord);
        }";

    // Vertex/TexCoord Buffers (cached, keine Allokation pro Frame)
    private int _vertexBuffer;
    private int _texCoordBuffer;

    private bool _initialized;

    /// <summary>Shader kompilieren und Buffers erstellen. Muss auf dem GL-Thread aufgerufen werden.</summary>
    public void Initialize()
    {
        if (_initialized) return;

        // Shader kompilieren
        var vertexShader = CompileShader(GLES20.GlVertexShader, VertexShaderSource);
        var fragmentShader = CompileShader(GLES20.GlFragmentShader, FragmentShaderSource);

        // Programm linken
        _programId = GLES20.GlCreateProgram();
        GLES20.GlAttachShader(_programId, vertexShader);
        GLES20.GlAttachShader(_programId, fragmentShader);
        GLES20.GlLinkProgram(_programId);

        // Attribute-Handles holen
        _positionHandle = GLES20.GlGetAttribLocation(_programId, "a_Position");
        _texCoordHandle = GLES20.GlGetAttribLocation(_programId, "a_TexCoord");

        // Vertex Buffer Object erstellen
        var buffers = new int[2];
        GLES20.GlGenBuffers(2, buffers, 0);
        _vertexBuffer = buffers[0];
        _texCoordBuffer = buffers[1];

        // Vertex-Daten hochladen
        UploadVertexBuffer();
        UploadTexCoordBuffer();

        // Shader aufraeumen (sind jetzt im Programm gelinkt)
        GLES20.GlDeleteShader(vertexShader);
        GLES20.GlDeleteShader(fragmentShader);

        _initialized = true;
    }

    /// <summary>
    /// Kamera-Hintergrund zeichnen. Muss pro Frame aufgerufen werden.
    /// </summary>
    /// <param name="textureId">Die von ARCore beschriebene Kamera-Textur-ID</param>
    public void Draw(int textureId)
    {
        if (!_initialized) return;

        // Depth-Test deaktivieren (Hintergrund ist immer hinter allem)
        GLES20.GlDisable(GLES20.GlDepthTest);
        GLES20.GlDepthMask(false);

        GLES20.GlUseProgram(_programId);

        // Kamera-Textur binden
        GLES20.GlActiveTexture(GLES20.GlTexture0);
        GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, textureId);

        // Vertex-Positionen
        GLES20.GlBindBuffer(GLES20.GlArrayBuffer, _vertexBuffer);
        GLES20.GlEnableVertexAttribArray(_positionHandle);
        GLES20.GlVertexAttribPointer(_positionHandle, 2, GLES20.GlFloat, false, 0, 0);

        // Texturkoordinaten
        GLES20.GlBindBuffer(GLES20.GlArrayBuffer, _texCoordBuffer);
        GLES20.GlEnableVertexAttribArray(_texCoordHandle);
        GLES20.GlVertexAttribPointer(_texCoordHandle, 2, GLES20.GlFloat, false, 0, 0);

        // Zeichnen (Triangle Strip: 4 Vertices = 2 Dreiecke)
        GLES20.GlDrawArrays(GLES20.GlTriangleStrip, 0, 4);

        // Aufraeumen
        GLES20.GlDisableVertexAttribArray(_positionHandle);
        GLES20.GlDisableVertexAttribArray(_texCoordHandle);
        GLES20.GlBindBuffer(GLES20.GlArrayBuffer, 0);

        // Depth-Test wieder aktivieren (fuer nachfolgendes 3D-Rendering)
        GLES20.GlDepthMask(true);
        GLES20.GlEnable(GLES20.GlDepthTest);
    }

    /// <summary>
    /// Texturkoordinaten aktualisieren (ARCore passt diese pro Frame an die Display-Rotation an).
    /// </summary>
    public void UpdateTexCoords(float[] transformedTexCoords)
    {
        if (transformedTexCoords.Length < 8) return;
        _texCoords = transformedTexCoords;
        if (_initialized)
            UploadTexCoordBuffer();
    }

    private void UploadVertexBuffer()
    {
        var buffer = Java.Nio.ByteBuffer.AllocateDirect(QuadVertices.Length * 4)!;
        // NativeOrder() liefert immer eine gültige ByteOrder (nur nullable annotiert)
        buffer.Order(Java.Nio.ByteOrder.NativeOrder()!);
        var floatBuffer = buffer.AsFloatBuffer()!;
        floatBuffer.Put(QuadVertices);
        floatBuffer.Position(0);

        GLES20.GlBindBuffer(GLES20.GlArrayBuffer, _vertexBuffer);
        GLES20.GlBufferData(GLES20.GlArrayBuffer, QuadVertices.Length * 4, floatBuffer, GLES20.GlStaticDraw);
    }

    private void UploadTexCoordBuffer()
    {
        // ByteBuffer einmal erstellen und wiederverwenden (kein Native Memory Leak bei 30fps)
        if (_texCoordByteBuffer == null)
        {
            _texCoordByteBuffer = Java.Nio.ByteBuffer.AllocateDirect(_texCoords.Length * 4)!;
            // NativeOrder() liefert immer eine gültige ByteOrder (nur nullable annotiert)
            _texCoordByteBuffer.Order(Java.Nio.ByteOrder.NativeOrder()!);
            _texCoordFloatBuffer = _texCoordByteBuffer.AsFloatBuffer()!;
        }

        _texCoordFloatBuffer!.Clear();
        _texCoordFloatBuffer.Put(_texCoords);
        _texCoordFloatBuffer.Position(0);

        GLES20.GlBindBuffer(GLES20.GlArrayBuffer, _texCoordBuffer);
        GLES20.GlBufferData(GLES20.GlArrayBuffer, _texCoords.Length * 4, _texCoordFloatBuffer, GLES20.GlDynamicDraw);
    }

    private static int CompileShader(int type, string source)
    {
        var shader = GLES20.GlCreateShader(type);
        GLES20.GlShaderSource(shader, source);
        GLES20.GlCompileShader(shader);

        // Kompilierungsfehler pruefen
        var status = new int[1];
        GLES20.GlGetShaderiv(shader, GLES20.GlCompileStatus, status, 0);
        if (status[0] == 0)
        {
            var log = GLES20.GlGetShaderInfoLog(shader);
            GLES20.GlDeleteShader(shader);
            throw new InvalidOperationException($"Shader-Kompilierung fehlgeschlagen: {log}");
        }

        return shader;
    }

    public void Dispose()
    {
        // GL-Objekte freigeben — in try/catch, weil Dispose nach dem EGL-Kontext-Teardown laufen
        // kann (synchroner Aufruf aus OnDestroy ohne aktiven Kontext). Der Java-NIO-Buffer-Dispose
        // unten ist kontextunabhängig und MUSS laufen, sonst Native-Memory-Leak pro Session.
        try
        {
            if (_programId != 0)
            {
                GLES20.GlDeleteProgram(_programId);
                _programId = 0;
            }

            if (_vertexBuffer != 0 || _texCoordBuffer != 0)
            {
                var buffers = new[] { _vertexBuffer, _texCoordBuffer };
                GLES20.GlDeleteBuffers(2, buffers, 0);
                _vertexBuffer = 0;
                _texCoordBuffer = 0;
            }
        }
        catch { /* kein aktiver GL-Kontext — die Objekte werden mit dem Kontext ohnehin zerstört */ }

        _texCoordByteBuffer?.Dispose();
        _texCoordByteBuffer = null;
        _texCoordFloatBuffer = null;

        _initialized = false;
    }
}
