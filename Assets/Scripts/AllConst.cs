using UnityEngine;

public static class AllConst {
    /// <summary>
    /// Number of pixels per unit, used in this project. All resources should be imported using the same
    /// value for "Pixels Per Unit" parameter. 
    /// </summary>
    public const int PixelsPerUnit = 32;
    
    /// <summary>
    /// Size of a pixel in units, actually, inverse dependence of `PixelsPerUnit`. Useful for usage in
    /// ray casting to check nearest pixel, for example, to check it object touches ground. 
    /// </summary>
    public const float PixelSize = 1f / PixelsPerUnit;
    public const float HalfPixelSize = PixelSize * 0.5f;

    /// <summary>
    /// Logical viewport resolution (in pixels) used as the designed screen size.
    /// Pixel-perfect rendering scales this resolution to the actual display using 
    /// integer zoom factors, ensuring crisp, distortion-free pixel art.
    /// 
    /// Only the aspect ratio affects composition. At runtime the camera may show 
    /// slightly more or less world space depending on the screen resolution and 
    /// pixel-perfect scaling settings (Crop Frames, Stretch Fill, etc).
    /// </summary>
    public static readonly Vector2Int ReferenceResolution = new Vector2Int(480, 270);

    // TODO: [BG] Set orthographic size automatically when scene is loaded.
    /// <summary>
    /// Orthographic size required for the camera to display the reference resolution
    /// at the chosen Pixels-Per-Unit (PPU). Must be assigned to the Cinemachine 
    /// Virtual Camera to ensure consistent, pixel-perfect world scaling.
    /// 
    /// Computed as:
    ///     orthographicSize = (ReferenceResolution.y / PPU) * 0.5
    /// 
    /// This converts the designed pixel height into world units and provides 
    /// the half-height expected by Unity's orthographic camera.
    /// This value should be set to every Cinamachine Virtual Camera's Orthographic Size property.
    /// </summary>
    public static readonly float OrthographicSize = ReferenceResolution.y * 0.5f / PixelsPerUnit;
    
    public const string EntryPointName = "EntryPoint";
    public const string CanvasesName = "SysCanvases";
    public const string FadeOverlayName = "FadeOverlay";
}
