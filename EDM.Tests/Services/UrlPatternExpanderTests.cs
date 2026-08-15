using System.Collections.Generic;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class UrlPatternExpanderTests
    {
        [Fact]
        public void Expand_NumericPatternWithPadding_ExpandsCorrectly()
        {
            string url = "http://example.com/archive_[01-05].zip";

            List<string> expanded = UrlPatternExpander.Expand(url);

            expanded.Should().HaveCount(5);
            expanded[0].Should().Be("http://example.com/archive_01.zip");
            expanded[4].Should().Be("http://example.com/archive_05.zip");
        }

        [Fact]
        public void Expand_AlphaPattern_ExpandsCorrectly()
        {
            string url = "http://example.com/part_[a-c].rar";

            List<string> expanded = UrlPatternExpander.Expand(url);

            expanded.Should().HaveCount(3);
            expanded[0].Should().Be("http://example.com/part_a.rar");
            expanded[1].Should().Be("http://example.com/part_b.rar");
            expanded[2].Should().Be("http://example.com/part_c.rar");
        }
    }
}
