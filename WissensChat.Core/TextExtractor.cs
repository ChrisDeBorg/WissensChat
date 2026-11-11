using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PDFtoImage;
using SkiaSharp;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace WissensChat.Core;

public static class TextExtractor
{
    public static string Culture { get; set; } = "en-US";

    public static IEnumerable<Segment> Extract(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => ExtractPdf(path),
            ".doc" or ".docx" => ExtractDocx(path),
            var ext => throw new NotSupportedException(
                $"Dateityp '{ext}' wird nicht unterstützt.")
        };

    private static IEnumerable<Segment> ExtractPdf(string path)
    {
        using var doc = PdfDocument.Open(path);

        // Tesseract nur einmal initialisieren.
        using var engine = CreateTesseractEngine(Culture);
        var results = new List<Segment>();

        foreach (var page in doc.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);

            if (!string.IsNullOrWhiteSpace(text))
            {
                results.Add(new Segment(
                    text,
                    $"S. {page.Number}"));

                continue;
            }


            try
            {
                var png = RenderPdfPageToPngBytes(
                    path,
                    page.Number,
                    300);

                if (png.Length == 0)
                {
                    continue;
                }

                var ocr = OcrImageBytes(
                    png,
                    engine);

                if (!string.IsNullOrWhiteSpace(ocr))
                {
                    results.Add(new Segment(
                        ocr,
                        $"S. {page.Number} (OCR)"));
                }
            }
            catch (Exception ex)
            {
                continue;
            }

        }

        return results;
    }

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
                nameof(pageNumber));

        if (dpi <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(dpi));

        using var pdfStream = File.OpenRead(pdfPath);

        using var bitmap = Conversion.ToImage(
            pdfStream,
            page: pageNumber - 1,
            options: new RenderOptions(Dpi: dpi));

        using var png = bitmap.Encode(
            SKEncodedImageFormat.Png,
            100);

        if (png is null)
            throw new InvalidOperationException(
                $"PDF-Seite {pageNumber} konnte nicht als PNG kodiert werden.");

        using var stream = new MemoryStream();
        png.SaveTo(stream);

        return stream.ToArray();
    }

    private static TesseractEngine CreateTesseractEngine(
        string language)
    {
        var lang = NormalizeTesseractLanguage(language);

        var tessDataPath = Path.Combine(
            AppContext.BaseDirectory,
            "tessdata");

        if (!Directory.Exists(tessDataPath))
        {
            throw new DirectoryNotFoundException(
                $"Tesseract tessdata-Verzeichnis nicht gefunden: {tessDataPath}");
        }

        var languageFile = Path.Combine(
            tessDataPath,
            $"{lang}.traineddata");

        if (!File.Exists(languageFile))
        {
            throw new FileNotFoundException(
                $"Tesseract-Sprachdatei nicht gefunden: {languageFile}");
        }

        return new TesseractEngine(
            tessDataPath,
            lang,
            EngineMode.Default);
    }

    private static string NormalizeTesseractLanguage(
        string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "eng";

        return language.Trim().ToLowerInvariant() switch
        {
            "de" => "deu",
            "de-de" => "deu",
            "de-at" => "deu",
            "de-ch" => "deu",

            "en" => "eng",
            "en-us" => "eng",
            "en-gb" => "eng",

            "deu" => "deu",
            "eng" => "eng",

            var value => value
        };
    }

    private static string OcrImageBytes(
        byte[] pngImageBytes,
        TesseractEngine engine)
    {
        if (pngImageBytes is null || pngImageBytes.Length == 0)
            throw new ArgumentNullException(nameof(pngImageBytes));

        using var pix = Pix.LoadFromMemory(pngImageBytes);
        using var page = engine.Process(pix);

        return page.GetText() ?? string.Empty;
    }

    private static IEnumerable<Segment> ExtractDocx(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);

        var body = doc.MainDocumentPart?
            .Document?
            .Body;

        if (body is null)
            yield break;

        var text = string.Join(
            "\n",
            body
                .Descendants<Paragraph>()
                .Select(p => p.InnerText));

        if (!string.IsNullOrWhiteSpace(text))
            yield return new Segment(text, null);
    }
}