using System;
using InsureChatBot.Api.Services.Document;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class PdfRasterizerTests
    {
        [Fact]
        public void Empty_pdf_throws()
        {
            var r = new PdfRasterizer();
            Assert.Throws<ArgumentException>(() => r.RenderFirstPagePng(Array.Empty<byte>()));
        }

        [Fact]
        public void Null_pdf_throws()
        {
            var r = new PdfRasterizer();
            Assert.Throws<ArgumentException>(() => r.RenderFirstPagePng(null));
        }
    }
}
