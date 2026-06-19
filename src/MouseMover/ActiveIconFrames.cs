using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MouseMover;

// base 아이콘 전체 밝기를 맥동시켜 트레이 애니메이션 프레임을 만든다.
// 활성(절전방지 동작) 중에만 사용. 비활성에서는 호출자가 base 아이콘으로 되돌린다.
public sealed class ActiveIconFrames : IDisposable
{
    // 전체 밝기 배수 (루프: 보통 → 밝게 → 보통).
    private static readonly float[] PulseBrightness = { 1.0f, 1.35f, 1.75f, 1.35f };

    private readonly List<Icon> _frames = new();

    public IReadOnlyList<Icon> Frames => _frames;

    public ActiveIconFrames(Icon baseIcon, int size = 32)
    {
        using var baseBmp = baseIcon.ToBitmap();
        foreach (var brightness in PulseBrightness)
        {
            var icon = BuildFrame(baseBmp, size, brightness);
            if (icon is not null) _frames.Add(icon);
        }
    }

    private static Icon? BuildFrame(Bitmap baseBmp, int size, float brightness)
    {
        try
        {
            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                // RGB만 배수(알파 유지) → 보이는 픽셀만 밝아지고 투명 영역은 그대로.
                var cm = new ColorMatrix(new[]
                {
                    new[] { brightness, 0f, 0f, 0f, 0f },
                    new[] { 0f, brightness, 0f, 0f, 0f },
                    new[] { 0f, 0f, brightness, 0f, 0f },
                    new[] { 0f, 0f, 0f, 1f, 0f },
                    new[] { 0f, 0f, 0f, 0f, 1f },
                });
                using var ia = new ImageAttributes();
                ia.SetColorMatrix(cm);
                g.DrawImage(baseBmp, new Rectangle(0, 0, size, size),
                    0, 0, baseBmp.Width, baseBmp.Height, GraphicsUnit.Pixel, ia);
            }

            IntPtr h = bmp.GetHicon();
            try { return (Icon)Icon.FromHandle(h).Clone(); }
            finally { NativeMethods.DestroyIcon(h); }
        }
        catch
        {
            // 프레임 생성 실패 시 해당 프레임 생략(애니메이션은 남은 프레임으로 동작).
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var f in _frames) f.Dispose();
        _frames.Clear();
    }
}
