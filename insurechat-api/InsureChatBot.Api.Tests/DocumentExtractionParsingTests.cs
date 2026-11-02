using InsureChatBot.Api.Services.Document;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class DocumentExtractionParsingTests
    {
        [Fact]
        public void Parses_plain_json()
        {
            var r = DocumentExtractionService.ParseVisionJson(
                "{\"isVehicleDocument\":true,\"docType\":\"policy_schedule\",\"fields\":{\"brand\":\"KIA\",\"year\":2005}}");
            Assert.True(r.IsVehicleDocument);
            Assert.Equal("KIA", r.Fields.Brand);
            Assert.Equal(2005, r.Fields.Year);
        }

        [Fact]
        public void Parses_json_wrapped_in_code_fence()
        {
            var r = DocumentExtractionService.ParseVisionJson(
                "```json\n{\"isVehicleDocument\":false,\"docType\":\"other\"}\n```");
            Assert.False(r.IsVehicleDocument);
            Assert.Equal("other", r.DocType);
        }

        [Fact]
        public void Reads_year_when_returned_as_string()
        {
            var r = DocumentExtractionService.ParseVisionJson(
                "{\"isVehicleDocument\":true,\"fields\":{\"year\":\"2020\"}}");
            Assert.Equal(2020, r.Fields.Year);
        }
    }
}
