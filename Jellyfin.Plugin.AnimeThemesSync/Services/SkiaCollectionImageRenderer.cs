using System;
using System.Collections.Generic;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Services;
using SkiaSharp;

namespace Jellyfin.Plugin.AnimeThemesSync.Services;

/// <summary>
/// Collection artwork compositor backed by the SkiaSharp 3.x assembly shipped with the Jellyfin server.
/// </summary>
public sealed class SkiaCollectionImageRenderer : ICollectionImageRenderer
{
    private const int JpegQuality = 90;

    /// <inheritdoc />
    public CollectionImageRenderResult? Render(
        IReadOnlyList<CollectionImageRenderSource> sources,
        int canvasWidth,
        int canvasHeight,
        Func<IReadOnlyList<CollectionImageLayoutSource>, CollectionImageLayoutResult> layoutForSources,
        string? overlayColor,
        int overlayOpacityPercent,
        string canvasColor,
        int canvasOpacityPercent)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return null;
        }

        var bitmaps = new List<SKBitmap>();
        var layoutSources = new List<CollectionImageLayoutSource>();
        try
        {
            foreach (var source in sources)
            {
                var bitmap = TryDecode(source.Path);
                if (bitmap != null)
                {
                    bitmaps.Add(bitmap);
                    layoutSources.Add(new CollectionImageLayoutSource(source.Kind, bitmap.Width, bitmap.Height));
                }
            }

            if (bitmaps.Count == 0)
            {
                return null;
            }

            var layout = layoutForSources(layoutSources);
            if (layout.Cells.Count == 0)
            {
                return null;
            }

            if (!SKColor.TryParse(canvasColor ?? "#000000", out var background))
            {
                background = SKColors.Black;
            }

            var backgroundAlpha = (byte)Math.Min(255, Math.Max(0, canvasOpacityPercent) * 255 / 100);
            var transparent = backgroundAlpha < 255;
            var info = new SKImageInfo(canvasWidth, canvasHeight, SKColorType.Bgra8888, transparent ? SKAlphaType.Premul : SKAlphaType.Opaque);
            using var surface = SKSurface.Create(info);
            if (surface == null)
            {
                return null;
            }

            var canvas = surface.Canvas;
            canvas.Clear(transparent ? background.WithAlpha(backgroundAlpha) : background.WithAlpha(255));
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            foreach (var cell in layout.Cells)
            {
                var source = bitmaps[cell.SourceIndex];
                var sourceRect = CenterCrop(source.Width, source.Height, cell.Width, cell.Height);
                var destRect = SKRect.Create(cell.X, cell.Y, cell.Width, cell.Height);
                using var image = SKImage.FromBitmap(source);
                if (image != null)
                {
                    canvas.DrawImage(image, sourceRect, destRect, sampling);
                }
            }

            if (overlayOpacityPercent > 0 && SKColor.TryParse(overlayColor ?? "#000000", out var color))
            {
                var alpha = (byte)Math.Min(255, overlayOpacityPercent * 255 / 100);
                using var paint = new SKPaint { Color = color.WithAlpha(alpha) };
                canvas.DrawRect(SKRect.Create(0, 0, canvasWidth, canvasHeight), paint);
            }

            using var snapshot = surface.Snapshot();
            using var data = snapshot?.Encode(transparent ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, JpegQuality);
            var bytes = data?.ToArray();
            return bytes == null
                ? null
                : new CollectionImageRenderResult(bytes, transparent ? "image/png" : "image/jpeg");
        }
        finally
        {
            foreach (var bitmap in bitmaps)
            {
                bitmap.Dispose();
            }
        }
    }

    private static SKBitmap? TryDecode(string path)
    {
        try
        {
            var bitmap = SKBitmap.Decode(path);
            if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                bitmap?.Dispose();
                return null;
            }

            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static SKRect CenterCrop(int sourceWidth, int sourceHeight, int cellWidth, int cellHeight)
    {
        var cellAspect = (double)cellWidth / cellHeight;
        var sourceAspect = (double)sourceWidth / sourceHeight;
        if (sourceAspect > cellAspect)
        {
            var cropWidth = (float)(sourceHeight * cellAspect);
            var x = (sourceWidth - cropWidth) / 2f;
            return new SKRect(x, 0, x + cropWidth, sourceHeight);
        }

        var cropHeight = (float)(sourceWidth / cellAspect);
        var y = (sourceHeight - cropHeight) / 2f;
        return new SKRect(0, y, sourceWidth, y + cropHeight);
    }
}
