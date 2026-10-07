using System;
using System.Collections.Generic;
using System.IO;
using invetoryBackGroundServices.Enums;
using invetoryBackGroundServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace invetoryBackGroundServices.Machine.Rendering
{
    /// <summary>
    /// <see cref="ICardRenderer"/> implementation using SkiaSharp (Evolis Integration Plan §9,
    /// decision Q-20 — not <c>System.Drawing.Common</c>: that's unsupported inside a Windows
    /// service/ASP.NET host, GDI-handle exhaustion is a known long-running-process failure, and
    /// it's Windows-only from .NET 7 anyway). A pure function from <see cref="CardRenderRequest"/>
    /// to bytes — no printer, no connection, fully testable in isolation.
    /// <para>
    /// Encodes BMP by hand (decision Q-23/Phase 0 step 8: BMP, not whatever SkiaSharp's own
    /// encoder defaults to) — confirmed that <c>SKImage.Encode</c> does not support
    /// <see cref="SKEncodedImageFormat.Bmp"/> as an output format in this SkiaSharp version (it
    /// decodes BMP via <c>SKCodec</c>, but its own encoder only writes Png/Jpeg/Webp), so the two
    /// private <c>Encode*Bmp</c> methods below write the file/DIB headers and pixel data directly
    /// from the rendered <see cref="SKBitmap"/>.
    /// </para>
    /// </summary>
    public sealed class SkiaCardRenderer : ICardRenderer
    {
        private readonly EvolisOptions _options;
        private readonly ILogger<SkiaCardRenderer> _logger;

        /// <summary>Creates the renderer.</summary>
        public SkiaCardRenderer(IOptions<EvolisOptions> options, ILogger<SkiaCardRenderer> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public CardRenderResult Render(CardRenderRequest request)
        {
            // Hard business rule, enforced rather than degraded (per the developer, pending a
            // separate future phase that makes this configurable per product): Arabic/RTL names
            // are rejected outright, not silently mis-rendered - SkiaCardRenderer does not shape
            // or bidi-reorder Arabic text (confirmed live; see CLAUDE.md's Phase 4 section), so a
            // name this renderer can't lay out correctly must never reach a printed card.
            string cardHolderName = ValidateAndNormalizeCardHolderName(request.CardHolderName);

            var warnings = new List<string>();

            int width = request.PrintWay == PrintWay.Landscape ? CardGeometry.LongSidePx : CardGeometry.ShortSidePx;
            int height = request.PrintWay == PrintWay.Landscape ? CardGeometry.ShortSidePx : CardGeometry.LongSidePx;

            CardPanel panel = DecidePanel(request.PrintColor, _options.MonochromeThreshold);

            SKColor backgroundColor = ParseColorOrDefault(request.BackgroundColor, SKColors.White, warnings, "background");
            if (panel == CardPanel.ResinBlack && backgroundColor != SKColors.White)
            {
                // Plan §9: a non-white background on a resin panel would mean printing a solid
                // black field across the whole card - flagged to the operator, not silent.
                warnings.Add(
                    $"Non-white background '{request.BackgroundColor}' is not supported on a resin (monochrome) panel; forced to white.");
                backgroundColor = SKColors.White;
            }

            SKColor textColor = ParseColorOrDefault(request.PrintColor, SKColors.Black, warnings, "text");

            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(backgroundColor);

                using SKTypeface typeface = ResolveTypeface(request.FontFamily, request.FontStyle, warnings);
                int fontSizePx = CardGeometry.PointsToPixels(request.FontSize);
                using var font = new SKFont(typeface, fontSizePx);
                using var paint = new SKPaint { Color = textColor, IsAntialias = true };

                (float x, float y) = ClampToCanvas(request.X, request.Y, width, height, warnings);

                canvas.DrawText(cardHolderName, x, y, font, paint);
            }

            byte[] bmpBytes = panel == CardPanel.ResinBlack
                ? EncodeMonochromeBmp(bitmap, _options.MonochromeThreshold)
                : EncodeRgb24Bmp(bitmap);

            foreach (string warning in warnings)
            {
                _logger.LogWarning("{Warning}", warning);
            }

            return new CardRenderResult(bmpBytes, panel, width, height, warnings);
        }

        /// <summary>
        /// Enforces the current cardholder-name rule: English letters, digits, spaces, and basic
        /// name punctuation only (<c>. , - '</c>) — anything else (Arabic included) is rejected
        /// outright via <see cref="InvalidCardHolderNameException"/>, not degraded. Also
        /// normalizes to uppercase, per the same rule. ASCII ranges only, deliberately not
        /// <see cref="char.IsLetter(char)"/>/<see cref="char.IsDigit(char)"/>, which both accept
        /// non-Latin Unicode letters/digits (Arabic included) that this rule exists to exclude.
        /// </summary>
        private static string ValidateAndNormalizeCardHolderName(string cardHolderName)
        {
            if (string.IsNullOrWhiteSpace(cardHolderName))
            {
                throw new InvalidCardHolderNameException("Cardholder name must not be empty.");
            }

            foreach (char c in cardHolderName)
            {
                bool isAllowed =
                    (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                    || c is ' ' or '.' or ',' or '-' or '\'';
                if (!isAllowed)
                {
                    throw new InvalidCardHolderNameException(
                        $"Cardholder name '{cardHolderName}' contains unsupported character '{c}'. " +
                        "Only English letters, digits, spaces, and . , - ' are allowed.");
                }
            }

            return cardHolderName.ToUpperInvariant();
        }

        /// <summary>Decision Q-25: a <c>PrintColor</c> whose R/G/B are all within the configured threshold of black is monochrome.</summary>
        private static CardPanel DecidePanel(string printColorHex, byte threshold)
        {
            if (SKColor.TryParse(printColorHex, out SKColor color)
                && color.Red <= threshold && color.Green <= threshold && color.Blue <= threshold)
            {
                return CardPanel.ResinBlack;
            }

            return CardPanel.Colour;
        }

        private static SKColor ParseColorOrDefault(string hex, SKColor fallback, List<string> warnings, string label)
        {
            if (SKColor.TryParse(hex, out SKColor color))
            {
                return color;
            }

            warnings.Add($"Could not parse {label} color '{hex}'; using the default.");
            return fallback;
        }

        /// <summary>
        /// Resolves the configured font, falling back to <see cref="EvolisOptions.DefaultFontFamily"/>
        /// (with a warning, never silently) when the family isn't installed on this host.
        /// <see cref="SKTypeface.FromFamilyName(string, SKFontStyle)"/> never returns null — it
        /// silently substitutes the system default typeface when the family is missing, so the
        /// substitution is detected explicitly by comparing the resolved family name.
        /// </summary>
        private SKTypeface ResolveTypeface(string fontFamily, string fontStyle, List<string> warnings)
        {
            SKFontStyle style = ParseFontStyle(fontStyle);
            SKTypeface typeface = SKTypeface.FromFamilyName(fontFamily, style);

            if (!string.Equals(typeface.FamilyName, fontFamily, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Font family '{fontFamily}' is not installed on this host; substituted '{_options.DefaultFontFamily}'.");
                typeface.Dispose();
                typeface = SKTypeface.FromFamilyName(_options.DefaultFontFamily, style);
            }

            return typeface;
        }

        private static SKFontStyle ParseFontStyle(string? fontStyle) => fontStyle?.Trim().ToLowerInvariant() switch
        {
            "bold" => SKFontStyle.Bold,
            "italic" => SKFontStyle.Italic,
            "bolditalic" or "bold italic" => SKFontStyle.BoldItalic,
            _ => SKFontStyle.Normal
        };

        private static (float X, float Y) ClampToCanvas(int x, int y, int width, int height, List<string> warnings)
        {
            int clampedX = Math.Clamp(x, 0, width);
            int clampedY = Math.Clamp(y, 0, height);
            if (clampedX != x || clampedY != y)
            {
                warnings.Add(
                    $"Text origin ({x},{y}) was outside the {width}x{height} canvas; clamped to ({clampedX},{clampedY}).");
            }

            return (clampedX, clampedY);
        }

        /// <summary>Encodes a 24-bpp BGR BMP (the <see cref="CardPanel.Colour"/> case) directly from the rendered bitmap's pixels.</summary>
        private static byte[] EncodeRgb24Bmp(SKBitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int rowStride = (width * 3 + 3) / 4 * 4; // each row padded to a multiple of 4 bytes
            int pixelDataSize = rowStride * height;
            const int headerSize = 14 + 40;

            using var stream = new MemoryStream(headerSize + pixelDataSize);
            using var writer = new BinaryWriter(stream);

            WriteBmpFileHeader(writer, headerSize, headerSize + pixelDataSize);
            WriteDibHeader(writer, width, height, bitsPerPixel: 24, colorsUsed: 0, pixelDataSize);

            byte[] row = new byte[rowStride];
            for (int y = height - 1; y >= 0; y--) // BMP rows are stored bottom-up
            {
                Array.Clear(row);
                for (int x = 0; x < width; x++)
                {
                    SKColor c = bitmap.GetPixel(x, y);
                    int offset = x * 3;
                    row[offset] = c.Blue;
                    row[offset + 1] = c.Green;
                    row[offset + 2] = c.Red;
                }

                writer.Write(row);
            }

            return stream.ToArray();
        }

        /// <summary>
        /// Encodes a 1-bpp black/white BMP (the <see cref="CardPanel.ResinBlack"/> case) — a
        /// pixel is white when its average channel value exceeds <paramref name="threshold"/>,
        /// black otherwise, matching the same threshold the panel decision itself uses.
        /// </summary>
        private static byte[] EncodeMonochromeBmp(SKBitmap bitmap, byte threshold)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int rowStride = (width + 31) / 32 * 4; // 1 bit per pixel, padded to a multiple of 4 bytes
            int pixelDataSize = rowStride * height;
            const int paletteSize = 8; // 2 entries * 4 bytes (B,G,R,0)
            int headerSize = 14 + 40 + paletteSize;

            using var stream = new MemoryStream(headerSize + pixelDataSize);
            using var writer = new BinaryWriter(stream);

            WriteBmpFileHeader(writer, headerSize, headerSize + pixelDataSize);
            WriteDibHeader(writer, width, height, bitsPerPixel: 1, colorsUsed: 2, pixelDataSize);

            // Palette index 0 = black, index 1 = white.
            writer.Write(new byte[] { 0, 0, 0, 0 });
            writer.Write(new byte[] { 255, 255, 255, 0 });

            byte[] row = new byte[rowStride];
            for (int y = height - 1; y >= 0; y--)
            {
                Array.Clear(row);
                for (int x = 0; x < width; x++)
                {
                    SKColor c = bitmap.GetPixel(x, y);
                    bool isWhite = (c.Red + c.Green + c.Blue) / 3 > threshold;
                    if (isWhite)
                    {
                        row[x / 8] |= (byte)(0x80 >> (x % 8));
                    }
                }

                writer.Write(row);
            }

            return stream.ToArray();
        }

        private static void WriteBmpFileHeader(BinaryWriter writer, int pixelDataOffset, int fileSize)
        {
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(fileSize);
            writer.Write(0); // reserved
            writer.Write(pixelDataOffset);
        }

        private static void WriteDibHeader(BinaryWriter writer, int width, int height, short bitsPerPixel, int colorsUsed, int pixelDataSize)
        {
            writer.Write(40); // BITMAPINFOHEADER size
            writer.Write(width);
            writer.Write(height); // positive => bottom-up row order, matching the encoders above
            writer.Write((short)1); // planes
            writer.Write(bitsPerPixel);
            writer.Write(0); // compression: BI_RGB
            writer.Write(pixelDataSize);
            writer.Write(0); // x pixels per meter
            writer.Write(0); // y pixels per meter
            writer.Write(colorsUsed);
            writer.Write(0); // colors important
        }
    }
}
