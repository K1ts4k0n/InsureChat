using System;
using PDFtoImage;
using SkiaSharp;

namespace InsureChatBot.Api.Services.Document
{
    public class PdfRasterizer : IPdfRasterizer
    {
        public byte[] RenderFirstPagePng(byte[] pdf)
        {
            if (pdf == null || pdf.Length == 0)
                throw new ArgumentException("PDF ว่าง", nameof(pdf));

            // render หน้าแรก (index 0) → SKBitmap → PNG
            using var bitmap = Conversion.ToImage(pdf, page: 0, options: new RenderOptions(Dpi: 150));
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }
}
