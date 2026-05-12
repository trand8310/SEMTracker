using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Drawing;

namespace BDInsight.Swiper
{
    public static class SwipeTraceRenderer
    {
        public static void DrawTrajectoriesPng(
            List<SwipeTrace> traces,
            string filePath,
            int width,
            int height)
        {
            if (traces == null || traces.Count == 0)
                return;

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory);

            var colors = new Color[]
            {
                Color.Red,
                Color.Blue,
                Color.Green,
                Color.Orange,
                Color.Purple,
                Color.Brown,
                Color.DeepPink
            };

            using var image = new Image<Rgba32>(width, height);
            image.Mutate(ctx => ctx.Clear(Color.White));

            for (int t = 0; t < traces.Count; t++)
            {
                var trace = traces[t];
                var color = colors[t % colors.Length];

                if (trace.Points == null || trace.Points.Count == 0)
                    continue;

                image.Mutate(ctx =>
                {
                    ctx.Fill(Color.DarkGreen, new EllipsePolygon(trace.Start.X, trace.Start.Y, 5));
                    ctx.Fill(Color.DarkRed, new EllipsePolygon(trace.End.X, trace.End.Y, 5));
                });

                for (int i = 1; i < trace.Points.Count; i++)
                {
                    var p1 = trace.Points[i - 1];
                    var p2 = trace.Points[i];

                    image.Mutate(ctx =>
                    {
                        ctx.DrawLine(color, 2, new PointF[]
                        {
                            new PointF(p1.X, p1.Y),
                            new PointF(p2.X, p2.Y)
                        });

                        ctx.Fill(Color.Black, new EllipsePolygon(p2.X, p2.Y, 1.8f));
                    });
                }
            }

            image.Save(filePath);
        }

        public static void DrawSwipeGif(
            List<SwipeTrace> traces,
            string filePath,
            int width,
            int height,
            int frameDelayMs = 40,
            bool drawStartEnd = true)
        {
            if (traces == null || traces.Count == 0)
                return;

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory);

            var colors = new Color[]
            {
                Color.Red,
                Color.Blue,
                Color.Green,
                Color.Orange,
                Color.Purple,
                Color.Brown,
                Color.DeepPink
            };

            using var gif = new Image<Rgba32>(width, height);
            gif.Mutate(ctx => ctx.Clear(Color.White));

            bool hasAnyFrame = false;

            for (int t = 0; t < traces.Count; t++)
            {
                var trace = traces[t];
                var color = colors[t % colors.Length];

                if (trace.Points == null || trace.Points.Count == 0)
                    continue;

                for (int i = 0; i < trace.Points.Count; i++)
                {
                    using var frame = new Image<Rgba32>(width, height);
                    frame.Mutate(ctx => ctx.Clear(Color.White));

                    if (drawStartEnd)
                    {
                        frame.Mutate(ctx =>
                        {
                            ctx.Fill(Color.DarkGreen, new EllipsePolygon(trace.Start.X, trace.Start.Y, 5));
                            ctx.Fill(Color.DarkRed, new EllipsePolygon(trace.End.X, trace.End.Y, 5));
                        });
                    }

                    for (int j = 1; j <= i; j++)
                    {
                        var p1 = trace.Points[j - 1];
                        var p2 = trace.Points[j];

                        frame.Mutate(ctx =>
                        {
                            ctx.DrawLine(color, 2, new PointF[]
                            {
                                new PointF(p1.X, p1.Y),
                                new PointF(p2.X, p2.Y)
                            });

                            ctx.Fill(Color.Black, new EllipsePolygon(p2.X, p2.Y, 2));
                        });
                    }

                    var current = trace.Points[i];

                    frame.Mutate(ctx =>
                    {
                        ctx.Fill(Color.Black, new EllipsePolygon(current.X, current.Y, 3.5f));
                    });

                    frame.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = Math.Max(1, frameDelayMs / 10);
                    gif.Frames.AddFrame(frame.Frames.RootFrame);
                    hasAnyFrame = true;
                }
            }

            if (!hasAnyFrame)
                return;

            // 删除默认空白首帧，否则 GIF 第一帧会是一张空白图
            if (gif.Frames.Count > 1)
            {
                gif.Frames.RemoveFrame(0);
            }

            gif.SaveAsGif(filePath);
        }

        public static void DrawPngAndGif(
            List<SwipeTrace> traces,
            string pngPath,
            string gifPath,
            int width,
            int height,
            int frameDelayMs = 40)
        {
            DrawTrajectoriesPng(
                traces: traces,
                filePath: pngPath,
                width: width,
                height: height);

            DrawSwipeGif(
                traces: traces,
                filePath: gifPath,
                width: width,
                height: height,
                frameDelayMs: frameDelayMs);
        }

        public static void DrawEachTraceGif(
            List<SwipeTrace> traces,
            string outputDir,
            int width,
            int height,
            int frameDelayMs = 40)
        {
            if (traces == null || traces.Count == 0)
                return;

            Directory.CreateDirectory(outputDir);

            for (int i = 0; i < traces.Count; i++)
            {
                var one = new List<SwipeTrace> { traces[i] };

                string pngPath = System.IO.Path.Combine(outputDir, $"trace_{i + 1:000}.png");
                string gifPath = System.IO.Path.Combine(outputDir, $"trace_{i + 1:000}.gif");

                DrawPngAndGif(
                    traces: one,
                    pngPath: pngPath,
                    gifPath: gifPath,
                    width: width,
                    height: height,
                    frameDelayMs: frameDelayMs);
            }
        }
    }
}