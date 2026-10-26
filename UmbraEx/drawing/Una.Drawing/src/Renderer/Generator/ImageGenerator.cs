using Una.Drawing.Texture;

namespace Una.Drawing.Generator;

internal class ImageGenerator : IGenerator
{
    /// <inheritdoc/>
    public int RenderOrder => 3;

    /// <inheritdoc/>
    public bool Generate(SKCanvas canvas, Node node, Vector2 origin)
    {
        SKRect relativeRect = new(
            node.ComputedStyle.ImageInset?.Left ?? 0,
            node.ComputedStyle.ImageInset?.Top ?? 0,
            node.Bounds.PaddingSize.Width - (node.ComputedStyle.ImageInset?.Right ?? 0),
            node.Bounds.PaddingSize.Height - (node.ComputedStyle.ImageInset?.Bottom ?? 0)
        );

        if (relativeRect.IsEmpty) return false;

        SKImage? image = GetImage(node);
        if (image == null) return false;

        float userScale = MathF.Max(0.1f, node.ComputedStyle.ImageScale);

        SKShaderTileMode tileMode = node.ComputedStyle.ImageTileMode switch {
            ImageTileMode.Clamp  => SKShaderTileMode.Clamp,
            ImageTileMode.Repeat => SKShaderTileMode.Repeat,
            ImageTileMode.Mirror => SKShaderTileMode.Mirror,
            ImageTileMode.Decal  => SKShaderTileMode.Decal,
            _ => throw new ArgumentOutOfRangeException(nameof(node.ComputedStyle.ImageTileMode),
                node.ComputedStyle.ImageTileMode, null)
        };

        SKMatrix imageTransformMatrix;
        SKMatrix shaderOffsetMatrix = SKMatrix.CreateTranslation(
            (node.ComputedStyle.ImageOffset?.X ?? 0),
            (node.ComputedStyle.ImageOffset?.Y ?? 0)
        );

        if (tileMode is SKShaderTileMode.Repeat or SKShaderTileMode.Mirror) {
            // Tiling Mode Shader Matrix
            SKMatrix tileScaleMatrix = SKMatrix.CreateScale(userScale, userScale);
            Vector2  rotationPivot   = new Vector2(relativeRect.Width / 2f, relativeRect.Height / 2f);
            SKMatrix rotationMatrix =
                SKMatrix.CreateRotationDegrees(node.ComputedStyle.ImageRotation, rotationPivot.X, rotationPivot.Y);

            imageTransformMatrix = SKMatrix.Identity;
            imageTransformMatrix = imageTransformMatrix.PreConcat(shaderOffsetMatrix);
            imageTransformMatrix = imageTransformMatrix.PreConcat(rotationMatrix);
            imageTransformMatrix = imageTransformMatrix.PreConcat(tileScaleMatrix);
        } else {
            // Non-Tiling Mode Shader Matrix
            SKMatrix scaleMatrix = node.ComputedStyle.ImageScaleMode switch {
                ImageScaleMode.Adapt => SKMatrix.CreateScale(
                    (relativeRect.Width / image.Width) * userScale,
                    (relativeRect.Height / image.Height) * userScale
                ),
                ImageScaleMode.Original => SKMatrix.CreateScale(userScale, userScale),
                _                       => SKMatrix.Identity
            };

            Vector2 rotationPivot = node.ComputedStyle.ImageScaleMode switch {
                ImageScaleMode.Adapt    => new Vector2(relativeRect.Width / 2f, relativeRect.Height / 2f),
                ImageScaleMode.Original => new Vector2(image.Width / 2f, image.Height / 2f),
                _                       => new Vector2(relativeRect.Width / 2f, relativeRect.Height / 2f)
            };

            SKMatrix rotationMatrix =
                SKMatrix.CreateRotationDegrees(node.ComputedStyle.ImageRotation, rotationPivot.X, rotationPivot.Y);

            imageTransformMatrix = SKMatrix.Identity;
            imageTransformMatrix = imageTransformMatrix.PreConcat(shaderOffsetMatrix);
            imageTransformMatrix = imageTransformMatrix.PreConcat(rotationMatrix);
            imageTransformMatrix = imageTransformMatrix.PreConcat(scaleMatrix);
        }

        using SKPaint paint = new();
        paint.IsAntialias = node.ComputedStyle.IsAntialiased;
        paint.IsDither    = false;

        if (node.ComputedStyle.ImageBlur != Vector2.Zero) {
            using SKImageFilter blur = SKImageFilter.CreateBlur(
                node.ComputedStyle.ImageBlur.X,
                node.ComputedStyle.ImageBlur.Y,
                tileMode
            );
            paint.ImageFilter = blur;
        }

        SKShader imageShader = SKShader.CreateImage(image, tileMode, tileMode, imageTransformMatrix);

        imageShader = imageShader.WithColorFilter(
            SKColorFilter.CreateHighContrast(new() { Grayscale = node.ComputedStyle.ImageGrayscale, Contrast = node.ComputedStyle.ImageContrast })
        );

        imageShader = imageShader.WithColorFilter(
            SKColorFilter.CreateBlendMode(
                Color.ToSkColor(node.ComputedStyle.ImageColor),
                (SKBlendMode)node.ComputedStyle.ImageBlendMode
            )
        );
        paint.Shader = imageShader;


        int saveCount = canvas.Save();
        try {
            canvas.Translate(origin.X + relativeRect.Left, origin.Y + relativeRect.Top);
            SKRect drawingRect = SKRect.Create(0, 0, relativeRect.Width, relativeRect.Height);

            float radius = node.ComputedStyle.ImageRounding;

            if (radius < 0.01f) {
                canvas.DrawRect(drawingRect, paint);
            } else {
                var            style   = node.ComputedStyle;
                RoundedCorners corners = style.ImageRoundedCorners;

                SKPoint topLeft     = corners.HasFlag(RoundedCorners.TopLeft) ? new(radius, radius) : SKPoint.Empty;
                SKPoint topRight    = corners.HasFlag(RoundedCorners.TopRight) ? new(radius, radius) : SKPoint.Empty;
                SKPoint bottomRight = corners.HasFlag(RoundedCorners.BottomRight) ? new(radius, radius) : SKPoint.Empty;
                SKPoint bottomLeft  = corners.HasFlag(RoundedCorners.BottomLeft) ? new(radius, radius) : SKPoint.Empty;

                using SKRoundRect roundRect = new();
                roundRect.SetRectRadii(drawingRect, [topLeft, topRight, bottomRight, bottomLeft]);

                canvas.DrawRoundRect(roundRect, paint);
            }
        } finally {
            canvas.RestoreToCount(saveCount);
        }

        return true;
    }

    private SKImage? GetImage(Node node)
    {
        if (node.ComputedStyle.BitmapFontIcon is not null) {
            return TextureLoader.LoadGfdIcon(node.ComputedStyle.BitmapFontIcon.Value);
        }

        if (node.ComputedStyle.ImageBytes is not null) {
            return TextureLoader.LoadFromBytes(node.ComputedStyle.ImageBytes);
        }

        if (node.ComputedStyle.IconId is not null) {
            return TextureLoader.LoadIcon(node.ComputedStyle.IconId.Value);
        }

        if (!string.IsNullOrWhiteSpace(node.ComputedStyle.UldResource) && node.ComputedStyle is
            { UldPartsId: not null, UldPartId: not null }) {
            var uld = TextureLoader.LoadUld(
                node.ComputedStyle.UldResource,
                node.ComputedStyle.UldPartsId.Value,
                node.ComputedStyle.UldPartId.Value,
                node.ComputedStyle.UldStyle ?? UldStyle.Default
            );

            return uld?.Texture.Subset(new SKRectI(
                (int)uld.Value.Rect.X1,
                (int)uld.Value.Rect.Y1,
                (int)uld.Value.Rect.X2,
                (int)uld.Value.Rect.Y2
            ));
        }

        return null;
    }
}