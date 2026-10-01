using System.Windows;
using System.Windows.Controls;

namespace StreamKartoteka.Controls;

public sealed class FilingPanel : Panel
{
    public double CardHeight { get; set; } = 104;
    public double NormalGap { get; set; } = 12;
    public double MinimumStep { get; set; } = 34;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        var childSize = new Size(Math.Max(0, width), CardHeight);

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(childSize);
        }

        var height = double.IsInfinity(availableSize.Height)
            ? Math.Max(CardHeight, InternalChildren.Count * (CardHeight + NormalGap))
            : availableSize.Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0)
        {
            return finalSize;
        }

        var naturalStep = CardHeight + NormalGap;
        var fittingStep = InternalChildren.Count == 1
            ? naturalStep
            : (finalSize.Height - CardHeight) / (InternalChildren.Count - 1);
        var step = Math.Clamp(fittingStep, MinimumStep, naturalStep);
        var y = 0d;

        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(0, y, finalSize.Width, CardHeight));
            y += step;
        }

        return finalSize;
    }
}

