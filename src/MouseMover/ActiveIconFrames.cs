using System.Drawing;
using System.Drawing.Drawing2D;

namespace MouseMover;

// base 아이콘 위에 맥동하는 초록 "활성" 점을 덧그려 트레이 애니메이션 프레임을 만든다.
// 활성(절전방지 동작) 중에만 사용. 비활성에서는 호출자가 base 아이콘으로 되돌린다.
public sealed class ActiveIconFrames : IDisposable
{
    // 점 크기/투명도 맥동 단계 (루프: 작게 → 크게 → 작게).
    private static readonly double[] PulseScale = { 0.5, 0.75, 1.0, 0.75 };

    private readonly List<Icon> _frames = new();

    public IReadOnlyList<Icon> Frames => _frames;

    public ActiveIconFrames(Icon baseIcon, int size = 32)
    {
        foreach (var scale in PulseScale)
        {
            var icon = BuildFrame(baseIcon, size, scale);
            if (icon is not null) _frames.Add(icon);
        }
    }

    private static Icon? BuildFrame(Icon baseIcon, int size, double scale)
    {
        try
        {
            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.DrawIcon(baseIcon, new Rectangle(0, 0, size, size));

                float margin = size * 0.06f;
                float maxR = size * 0.26f;
                float cx = size - margin - maxR;   // 우하단 점 중심
                float cy = size - margin - maxR;
                float r = (float)(maxR * scale);
                int alpha = (int)(255 * (0.45 + 0.55 * scale));

                var rect = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
                using var fill = new SolidBrush(Color.FromArgb(alpha, 60, 200, 90));
                g.FillEllipse(fill, rect);
                using var outline = new Pen(Color.FromArgb(alpha, 20, 70, 30), Math.Max(1f, size / 24f));
                g.DrawEllipse(outline, rect);
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
