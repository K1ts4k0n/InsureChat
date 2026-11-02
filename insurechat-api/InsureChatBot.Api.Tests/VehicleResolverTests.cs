using System.Collections.Generic;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Motor;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class VehicleResolverTests
    {
        private static List<FacetItem> F(params string[] vals)
        {
            var list = new List<FacetItem>();
            foreach (var v in vals) list.Add(new FacetItem { Value = v, Label = v });
            return list;
        }

        [Fact]
        public void Brand_exact_normalized_match()
            => Assert.Equal("KIA", VehicleResolver.ResolveBrand("kia", F("KIA", "HONDA")));

        [Fact]
        public void Brand_contains_match()
            => Assert.Equal("KIA", VehicleResolver.ResolveBrand("KIA MOTORS", F("KIA", "HONDA")));

        [Fact]
        public void Brand_no_match_returns_null()
            => Assert.Null(VehicleResolver.ResolveBrand("FORD", F("TOYOTA", "HONDA")));

        [Fact]
        public void Brand_too_short_fuzzy_returns_null()
            => Assert.Null(VehicleResolver.ResolveBrand("K", F("KIA", "HONDA")));

        [Fact]
        public void Model_maps_spaced_to_canonical()
            => Assert.Equal("K2700", VehicleResolver.ResolveModel("K 2700 M", F("K2700", "CERATO")));

        [Fact]
        public void Model_prefers_exact_over_contains()
            => Assert.Equal("CIVIC", VehicleResolver.ResolveModel("CIVIC", F("CIVIC FD", "CIVIC")));

        [Fact]
        public void Model_no_match_returns_null()
            => Assert.Null(VehicleResolver.ResolveModel("ZZZ", F("CIVIC")));

        [Fact]
        public void Year_present_returns_year()
            => Assert.Equal(2005, VehicleResolver.ResolveYear(2005, F("2004", "2005", "2006")));

        [Fact]
        public void Year_absent_returns_null()
            => Assert.Null(VehicleResolver.ResolveYear(2000, F("2004", "2005")));

        [Fact]
        public void Year_null_input_returns_null()
            => Assert.Null(VehicleResolver.ResolveYear(null, F("2005")));
    }
}
