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
}
