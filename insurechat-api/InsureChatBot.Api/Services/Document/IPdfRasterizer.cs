namespace InsureChatBot.Api.Services.Document
{
    /// <summary>PDF → PNG หน้าแรก (สำหรับส่งให้ vision LLM). รับหน้าแรกเท่านั้น (ADR 0018)</summary>
    public interface IPdfRasterizer
    {
        byte[] RenderFirstPagePng(byte[] pdf);
    }
}
