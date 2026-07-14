using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Scratchdeck.Services;

namespace Scratchdeck.Tests;

public sealed class InkStrokeSerializationServiceTests
{
    [Fact]
    public void Serialize_WithNullOrEmptyCollection_ReturnsEmptyPayload()
    {
        Assert.Equal(string.Empty, InkStrokeSerializationService.Serialize(null));
        Assert.Equal(string.Empty, InkStrokeSerializationService.Serialize(new StrokeCollection()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not base64 or ink")]
    [InlineData("AQIDBA==")]
    public void Deserialize_WithEmptyOrMalformedPayload_ReturnsEmptyCollection(string? payload)
    {
        var strokes = InkStrokeSerializationService.Deserialize(payload);

        Assert.NotNull(strokes);
        Assert.Empty(strokes);
    }

    [Fact]
    public void StrokeCollection_RoundTripsGeometryPressureAndDrawingAttributes()
    {
        var firstStroke = CreateStroke(
            [
                new StylusPoint(12.25, 18.5, 0.2f),
                new StylusPoint(44.75, 50.125, 0.65f),
                new StylusPoint(90.5, 77.25, 1f)
            ],
            Color.FromRgb(0x16, 0xD9, 0xEF),
            width: 6.5,
            height: 7.5,
            StylusTip.Ellipse,
            fitToCurve: true,
            ignorePressure: false);

        var secondStroke = CreateStroke(
            [
                new StylusPoint(2, 4),
                new StylusPoint(8, 16)
            ],
            Color.FromRgb(0xFF, 0xB8, 0x30),
            width: 12,
            height: 9,
            StylusTip.Rectangle,
            fitToCurve: false,
            ignorePressure: true);

        var source = new StrokeCollection { firstStroke, secondStroke };

        var payload = InkStrokeSerializationService.Serialize(source);
        var restored = InkStrokeSerializationService.Deserialize(payload);

        Assert.NotEmpty(payload);
        Assert.Equal(2, restored.Count);
        AssertStrokeEquivalent(firstStroke, restored[0]);
        AssertStrokeEquivalent(secondStroke, restored[1]);
    }

    private static Stroke CreateStroke(
        IEnumerable<StylusPoint> points,
        Color color,
        double width,
        double height,
        StylusTip stylusTip,
        bool fitToCurve,
        bool ignorePressure)
    {
        return new Stroke(new StylusPointCollection(points))
        {
            DrawingAttributes = new DrawingAttributes
            {
                Color = color,
                Width = width,
                Height = height,
                StylusTip = stylusTip,
                FitToCurve = fitToCurve,
                IgnorePressure = ignorePressure
            }
        };
    }

    private static void AssertStrokeEquivalent(Stroke expected, Stroke actual)
    {
        Assert.Equal(expected.StylusPoints.Count, actual.StylusPoints.Count);
        for (var index = 0; index < expected.StylusPoints.Count; index++)
        {
            Assert.InRange(
                Math.Abs(expected.StylusPoints[index].X - actual.StylusPoints[index].X),
                0,
                0.02);
            Assert.InRange(
                Math.Abs(expected.StylusPoints[index].Y - actual.StylusPoints[index].Y),
                0,
                0.02);
            Assert.InRange(
                Math.Abs(expected.StylusPoints[index].PressureFactor - actual.StylusPoints[index].PressureFactor),
                0,
                0.01);
        }

        Assert.Equal(expected.DrawingAttributes.Color, actual.DrawingAttributes.Color);
        Assert.InRange(
            Math.Abs(expected.DrawingAttributes.Width - actual.DrawingAttributes.Width),
            0,
            0.02);
        Assert.InRange(
            Math.Abs(expected.DrawingAttributes.Height - actual.DrawingAttributes.Height),
            0,
            0.02);
        Assert.Equal(expected.DrawingAttributes.StylusTip, actual.DrawingAttributes.StylusTip);
        Assert.Equal(expected.DrawingAttributes.FitToCurve, actual.DrawingAttributes.FitToCurve);
        Assert.Equal(expected.DrawingAttributes.IgnorePressure, actual.DrawingAttributes.IgnorePressure);
    }
}
