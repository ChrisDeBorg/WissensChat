using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualBasic;
using PDFtoImage;
using SkiaSharp;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace WissensChat.Core;

public static class TextExtractor
{
    public static string Culture { get; set; } = "en-US";
    public static IEnumerable<Segment> Extract(string path) =>
        System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => ExtractPdf(path),
            ".docx" => ExtractDocx(path),
            var ext => throw new NotSupportedException($"Dateityp '{ext}' wird nicht unterstützt.")
        };

    private static IEnumerable<Segment> ExtractPdf(string path)
    {
        using var doc = PdfDocument.Open(path);
        foreach (var page in doc.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            if (!string.IsNullOrWhiteSpace(text))
                yield return new Segment(text, $"S. {page.Number}");
            else
            {
                string? ocr = null;
                // Versuch: Rendern der Seite als PNG (300 DPI) und OCR mit Tesseract
                try
                {
                    var png = RenderPdfPageToPngBytes(path, page.Number, 300);
                    if (png != null && png.Length > 0)
                    {
                        ocr = OcrImageBytes(png, Culture);
                    }
                }
                catch
                {
                    // ignore
                }

                if (!string.IsNullOrWhiteSpace(ocr))
                {
                    yield return new Segment(ocr, $"S. {page.Number} (OCR)");
                }
                else
                {
                    yield return new Segment(string.Empty, $"S. {page.Number} (kein Text extrahiert)");
                }
            }
        }
    }

    // Rendert eine einzelne PDF-Seite direkt mit PDFtoImage als PNG-Bytes.
    private static byte[] RenderPdfPageToPngBytes(
        string pdfPath,
        int pageNumber,
        int dpi)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentNullException(nameof(pdfPath));

        if (!File.Exists(pdfPath))
            throw new FileNotFoundException(
                "Die PDF-Datei wurde nicht gefunden.",
                pdfPath);

        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                "Die Seitennummer muss >= 1 sein.");

        if (dpi <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(dpi),
                "Die DPI muss > 0 sein.");

        // PDFtoImage verwendet 0-basierte Seitenindizes.
        var pageIndex = pageNumber - 1;

        using var pdfStream = File.OpenRead(pdfPath);

        using var bitmap = PDFtoImage.Conversion.ToImage(
            pdfStream,
            page: pageIndex,
            options: new RenderOptions(Dpi: dpi));

        if (bitmap is null)
            throw new InvalidOperationException(
                $"Die PDF-Seite {pageNumber} konnte nicht gerendert werden.");

        // SKBitmap -> PNG
        using var png = bitmap.Encode(
            SKEncodedImageFormat.Png,
            quality: 100);

        if (png is null)
            throw new InvalidOperationException(
                "Das gerenderte Bild konnte nicht als PNG kodiert werden.");

        using var memoryStream = new MemoryStream();
        png.SaveTo(memoryStream);

        return memoryStream.ToArray();
    }


    // OCR eines PNG-Bildes mit Tesseract.
    public static string OcrImageBytes(
        byte[] pngImageBytes,
        string? language = null)
    {
        if (pngImageBytes is null || pngImageBytes.Length == 0)
            throw new ArgumentNullException(nameof(pngImageBytes));

        var lang = language
            ?? CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        lang = lang switch
        {
            "de" => "deu",
            "en" => "eng",
            _ => lang
        };

        var tessDataPath = Path.Combine(
            AppContext.BaseDirectory,
            "tessdata");

        using var engine = new TesseractEngine(
            tessDataPath,
            lang,
            EngineMode.Default);

        using var pix = Pix.LoadFromMemory(pngImageBytes);
        using var page = engine.Process(pix);

        return page.GetText() ?? string.Empty;
    }



    private static IEnumerable<Segment> ExtractDocx(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) yield break;
        var text = string.Join("\n", body.Descendants<Paragraph>().Select(p => p.InnerText));
        if (!string.IsNullOrWhiteSpace(text)) yield return new Segment(text, null);
    }
}
