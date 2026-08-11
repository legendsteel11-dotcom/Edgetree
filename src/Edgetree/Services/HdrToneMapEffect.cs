using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
// System.Drawing is in scope through the implicit usings and has a Brush of
// its own; the sampler property is WPF's.
using Brush = System.Windows.Media.Brush;

namespace SidebarExplorer.App.Services;

// Pulls an HDR film back into something an SDR screen can show.
//
// WHY IT EXISTS. The panel plays through WPF's MediaElement, which is the old
// Windows Media Player pipeline - it reads the file's colour flags (BT.2020
// primaries, the SMPTE ST 2084 "PQ" transfer curve) and then ignores them,
// handing the PQ-encoded values over as though they were ordinary sRGB. Drawn
// that way an HDR film comes out pale and flat, with the highlights washed to
// grey: not a bug in the file and not a bug in the drawing, a missing
// conversion between the two.
//
// WHY IT IS A SHADER. Nothing in this pipeline exposes a frame, so the
// conversion cannot happen before the picture is drawn - but WPF composites an
// Effect over a MediaElement (measured 2026-08-11 with a BlurEffect, which was
// the whole reason to try), and an effect runs after the frame exists. So the
// correction is applied to the drawn result rather than to the decode.
//
// WHAT IT IS NOT. This is an approximation, not colour management: one global
// tone curve, no mastering-display metadata, no per-scene adaptation. A real
// player does better. The bar it has to clear is the untreated picture, which
// is wrong by a wide margin.
public sealed class HdrToneMapEffect : ShaderEffect
{
    private static readonly PixelShader Shader = new()
    {
        UriSource = new Uri("pack://application:,,,/Resources/HdrToneMap.ps")
    };

    public HdrToneMapEffect()
    {
        PixelShader = Shader;
        UpdateShaderValue(InputProperty);
        UpdateShaderValue(ExposureProperty);
        UpdateShaderValue(SaturationProperty);
        UpdateShaderValue(ContrastProperty);
    }

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(nameof(Input), typeof(HdrToneMapEffect), 0);

    public Brush Input
    {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    // The signal is normalised so 1.0 means 10,000 nits, so 100 puts 100-nit
    // diffuse white at 1.0 - the level an SDR screen calls white. Left as a
    // property because the right number is a matter of taste and of how the
    // film was graded, and because the .ps blob cannot be edited in place.
    public static readonly DependencyProperty ExposureProperty =
        DependencyProperty.Register(
            nameof(Exposure), typeof(double), typeof(HdrToneMapEffect),
            new UIPropertyMetadata(100.0, PixelShaderConstantCallback(0)));

    public double Exposure
    {
        get => (double)GetValue(ExposureProperty);
        set => SetValue(ExposureProperty, value);
    }

    // A last dial after the curve, because the curve alone does not get all of
    // it back: a tone map is a compression, and compression costs colour. Above
    // 1 puts some of it back around the same luminance.
    public static readonly DependencyProperty SaturationProperty =
        DependencyProperty.Register(
            nameof(Saturation), typeof(double), typeof(HdrToneMapEffect),
            new UIPropertyMetadata(1.15, PixelShaderConstantCallback(1)));

    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    // Around mid grey, after the gamma encode - see the shader. The tone curve
    // already decides most of the contrast; this is the part of it that is
    // taste rather than maths, and different films want different answers.
    public static readonly DependencyProperty ContrastProperty =
        DependencyProperty.Register(
            nameof(Contrast), typeof(double), typeof(HdrToneMapEffect),
            new UIPropertyMetadata(1.0, PixelShaderConstantCallback(2)));

    public double Contrast
    {
        get => (double)GetValue(ContrastProperty);
        set => SetValue(ContrastProperty, value);
    }
}
