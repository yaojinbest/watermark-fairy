using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WatermarkFairy.Controls;

/// <summary>
/// v0.3.3.3 文字描边控件（真正的字符轮廓，不是 Border 矩形边框）
/// 用 FormattedText.BuildGeometry + DrawingContext.DrawGeometry 实现
/// 描边宽度 = StrokeThickness（像素），描边颜色 = Stroke
/// </summary>
public class OutlinedText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(OutlinedText),
            new FrameworkPropertyMetadata("",
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(OutlinedText),
            new FrameworkPropertyMetadata(new FontFamily("Microsoft YaHei"),
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(OutlinedText),
            new FrameworkPropertyMetadata(12.0,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(OutlinedText),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(OutlinedText),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(OutlinedText),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (string.IsNullOrEmpty(Text)) return new Size(0, 0);
        var ft = MakeFormattedText();
        // bbox 含描边 padding（让 WatermarkBorder 知道真实尺寸）
        return new Size(ft.Width + StrokeThickness + 2, ft.Height + StrokeThickness + 2);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;
        var ft = MakeFormattedText();
        // offset 让描边外扩（stroke 在 glyph 外侧）
        var origin = new Point(StrokeThickness / 2.0, StrokeThickness / 2.0);
        var geo = ft.BuildGeometry(origin);

        // 先画描边（外侧 Pen）
        if (Stroke != null && StrokeThickness > 0)
        {
            var pen = new Pen(Stroke, StrokeThickness);
            pen.LineJoin = PenLineJoin.Round;
            dc.DrawGeometry(null, pen, geo);
        }

        // 再画填充（覆盖内部 stroke 像素）
        if (Fill != null)
            dc.DrawGeometry(Fill, null, geo);
    }

    private FormattedText MakeFormattedText()
    {
        var typeface = new Typeface(
            FontFamily ?? new FontFamily("Microsoft YaHei"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

        return new FormattedText(
            Text ?? "",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize > 0 ? FontSize : 12.0,
            Fill ?? Brushes.White,
            1.0);
    }
}