using System.Text;
using ZIONShop.Admin.Application.Common;

namespace ZIONShop.Admin.Tests.Common;

public class CsvWriterTests
{
    [Fact]
    public void Write_Should_PrependUtf8Bom()
    {
        var bytes = CsvWriter.Write(new[] { "A", "B" }, new[] { (IReadOnlyList<string>)new[] { "1", "2" } });

        bytes.Take(3).Should().BeEquivalentTo(Encoding.UTF8.GetPreamble());
    }

    [Fact]
    public void Write_Should_EscapeFieldsContainingCommaOrQuote()
    {
        var bytes = CsvWriter.Write(new[] { "Name" }, new[] { (IReadOnlyList<string>)new[] { "A, \"B\"" } });
        var text = Encoding.UTF8.GetString(bytes.Skip(3).ToArray());

        text.Should().Contain("\"A, \"\"B\"\"\"");
    }

    [Fact]
    public void Write_Should_UseCrlfLineEndings_And_IncludeHeaderRow()
    {
        var bytes = CsvWriter.Write(new[] { "Col1", "Col2" }, new[] { (IReadOnlyList<string>)new[] { "x", "y" } });
        var text = Encoding.UTF8.GetString(bytes.Skip(3).ToArray());

        text.Should().Be("Col1,Col2\r\nx,y\r\n");
    }

    [Theory]
    [InlineData(1234.4, "1234")]
    [InlineData(1234.5, "1235")]
    [InlineData(0, "0")]
    public void FormatMoney_Should_RoundToPlainInteger(decimal value, string expected)
    {
        CsvWriter.FormatMoney(value).Should().Be(expected);
    }
}
