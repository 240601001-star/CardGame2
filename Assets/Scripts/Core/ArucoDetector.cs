using System;
using System.Collections.Generic;

namespace CardGame.Core
{
    /// <summary>Managed, conservative DICT_4X4_50 ID 0-9 detector. No Unity/native dependency.</summary>
    public sealed class ArucoDetector
    {
        // OpenCV predefined_dictionaries.hpp, DICT_4X4_1000_BYTES first ten entries.
        // DICT_4X4_50 uses the first 50 entries of that table. Row-major, white = 1.
        // https://github.com/opencv/opencv/blob/4.x/modules/objdetect/src/aruco/predefined_dictionaries.hpp
        private static readonly ushort[] Codes =
            { 0xB532, 0x0F9A, 0x332D, 0x9946, 0x549E, 0x79CD, 0x9E2E, 0xC4F2, 0xFEDA, 0xCF56 };

        public struct Point
        {
            public float X, Y;
            public Point(float x, float y) { X = x; Y = y; }
        }

        public struct Detection
        {
            public int ID;
            public Point Center;
            public float Contrast;
        }

        private byte[] _binary;
        private bool[] _visited;
        private int[] _queue;
        private long[] _integral;
        private readonly List<Point> _boundary = new List<Point>();
        private readonly List<Point> _hull = new List<Point>();
        private readonly List<Point> _polygon = new List<Point>();
        private readonly byte[] _samples = new byte[36 * 25];
        private readonly int[] _histogram = new int[256];

        public static ushort Rotate90(ushort bits)
        {
            ushort result = 0;
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                    result |= (ushort)(((bits >> (15 - (3 - col) * 4 - row)) & 1) << (15 - row * 4 - col));
            return result;
        }

        /// <summary>Exact matches only; never chooses a nearest or tied dictionary entry.</summary>
        public static int Match(ushort bits)
        {
            int match = -1;
            for (int id = 0; id < Codes.Length; id++)
            {
                ushort code = Codes[id];
                for (int rotation = 0; rotation < 4; rotation++, code = Rotate90(code))
                {
                    if (bits != code) continue;
                    if (match >= 0) return -1;
                    match = id;
                }
            }
            return match;
        }

        public void Detect(byte[] gray, int width, int height, int blockSize, int adaptiveC,
            float minArea, float maxAreaFraction, List<Detection> results)
        {
            results.Clear();
            if (width < 12 || height < 12 || gray == null || gray.Length != width * height) return;
            int count = gray.Length;
            if (_binary == null || _binary.Length != count)
            {
                _binary = new byte[count];
                _visited = new bool[count];
                _queue = new int[count];
                _integral = new long[count];
            }

            // A global pass preserves solid borders; a local pass handles uneven illumination.
            Array.Clear(_histogram, 0, 256);
            for (int i = 0; i < count; i++) _histogram[gray[i]]++;
            int global = Otsu(_histogram, count);
            for (int i = 0; i < count; i++) _binary[i] = (byte)(gray[i] <= global ? 0 : 255);
            FindCandidates(gray, width, height, minArea, maxAreaFraction, results);

            for (int y = 0; y < height; y++)
            {
                long sum = 0;
                for (int x = 0; x < width; x++)
                {
                    sum += gray[y * width + x];
                    _integral[y * width + x] = sum + (y > 0 ? _integral[(y - 1) * width + x] : 0);
                }
            }
            int half = Math.Max(1, blockSize / 2);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int x0 = Math.Max(0, x - half), x1 = Math.Min(width - 1, x + half);
                    int y0 = Math.Max(0, y - half), y1 = Math.Min(height - 1, y + half);
                    long sum = _integral[y1 * width + x1];
                    if (x0 > 0) sum -= _integral[y1 * width + x0 - 1];
                    if (y0 > 0) sum -= _integral[(y0 - 1) * width + x1];
                    if (x0 > 0 && y0 > 0) sum += _integral[(y0 - 1) * width + x0 - 1];
                    float mean = (float)sum / ((x1 - x0 + 1) * (y1 - y0 + 1));
                    _binary[y * width + x] = (byte)(gray[y * width + x] <= mean - adaptiveC ? 0 : 255);
                }
            FindCandidates(gray, width, height, minArea, maxAreaFraction, results);
        }

        private void FindCandidates(byte[] gray, int w, int h, float minArea, float maxFraction,
            List<Detection> results)
        {
            Array.Clear(_visited, 0, _visited.Length);
            for (int start = 0; start < _binary.Length; start++)
            {
                if (_visited[start] || _binary[start] != 0) continue;
                int head = 0, tail = 1;
                _queue[0] = start;
                _visited[start] = true;
                _boundary.Clear();
                bool touchesEdge = false;
                while (head < tail)
                {
                    int index = _queue[head++], x = index % w, y = index / w;
                    bool boundary = false;
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1) touchesEdge = true;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int next = ny * w + nx;
                            if (_binary[next] != 0) { boundary = true; continue; }
                            if (_visited[next]) continue;
                            _visited[next] = true;
                            _queue[tail++] = next;
                        }
                    if (boundary) _boundary.Add(new Point(x, y));
                }
                if (touchesEdge || tail < minArea || _boundary.Count < 4) continue;
                BuildHull();
                if (_hull.Count < 4) continue;
                float hullArea = Area(_hull);
                if (hullArea < minArea || hullArea > w * h * maxFraction) continue;
                _polygon.Clear();
                _polygon.AddRange(_hull);
                // Remove raster stair steps, keeping the four dominant convex corners.
                while (_polygon.Count > 4)
                {
                    int remove = 0;
                    float least = float.MaxValue;
                    for (int i = 0; i < _polygon.Count; i++)
                    {
                        float a = Math.Abs(Cross(_polygon[(i + _polygon.Count - 1) % _polygon.Count],
                            _polygon[i], _polygon[(i + 1) % _polygon.Count]));
                        if (a < least) { least = a; remove = i; }
                    }
                    _polygon.RemoveAt(remove);
                }
                if (Area(_polygon) < hullArea * 0.94f) continue;
                bool valid = true;
                float minSide = float.MaxValue, maxSide = 0;
                for (int i = 0; i < 4; i++)
                {
                    Point a = _polygon[i], b = _polygon[(i + 1) % 4], c = _polygon[(i + 2) % 4];
                    float side = Distance(a, b);
                    minSide = Math.Min(minSide, side); maxSide = Math.Max(maxSide, side);
                    float cosine = Math.Abs(((a.X - b.X) * (c.X - b.X) + (a.Y - b.Y) * (c.Y - b.Y)) /
                        Math.Max(0.001f, side * Distance(b, c)));
                    if (cosine > 0.65f) valid = false;
                }
                if (!valid || minSide < 24 || maxSide > minSide * 2) continue;
                if (!Decode(gray, w, h, _polygon, out int id, out float contrast)) continue;
                Point center = Project(_polygon, 0.5f, 0.5f);
                bool duplicate = false;
                foreach (Detection detection in results)
                    if (detection.ID == id && Distance(detection.Center, center) < minSide * 0.3f)
                        duplicate = true;
                if (!duplicate) results.Add(new Detection { ID = id, Center = center, Contrast = contrast });
            }
        }

        private void BuildHull()
        {
            _boundary.Sort((a, b) => a.X == b.X ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            _hull.Clear();
            foreach (Point p in _boundary)
            {
                while (_hull.Count >= 2 && Cross(_hull[_hull.Count - 2], _hull[_hull.Count - 1], p) <= 0)
                    _hull.RemoveAt(_hull.Count - 1);
                _hull.Add(p);
            }
            int lower = _hull.Count;
            for (int i = _boundary.Count - 2; i >= 0; i--)
            {
                Point p = _boundary[i];
                while (_hull.Count > lower && Cross(_hull[_hull.Count - 2], _hull[_hull.Count - 1], p) <= 0)
                    _hull.RemoveAt(_hull.Count - 1);
                _hull.Add(p);
            }
            if (_hull.Count > 1) _hull.RemoveAt(_hull.Count - 1);
        }

        private bool Decode(byte[] gray, int w, int h, List<Point> quad, out int id, out float contrast)
        {
            id = -1; contrast = 0;
            Array.Clear(_histogram, 0, 256);
            float darkest = 255, lightest = 0;
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                {
                    int sum = 0, cell = y * 6 + x;
                    for (int sy = 0; sy < 5; sy++)
                        for (int sx = 0; sx < 5; sx++)
                        {
                            Point p = Project(quad, (x + 0.25f + sx * 0.125f) / 6,
                                (y + 0.25f + sy * 0.125f) / 6);
                            if (!Sample(gray, w, h, p, out byte value)) return false;
                            _samples[cell * 25 + sy * 5 + sx] = value;
                            _histogram[value]++;
                            sum += value;
                        }
                    float mean = sum / 25f;
                    darkest = Math.Min(darkest, mean); lightest = Math.Max(lightest, mean);
                }
            contrast = lightest - darkest;
            if (contrast < 60) return false;
            int threshold = Otsu(_histogram, _samples.Length);
            ushort bits = 0;
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                {
                    int white = 0, cell = y * 6 + x;
                    for (int i = 0; i < 25; i++) if (_samples[cell * 25 + i] > threshold) white++;
                    bool border = x == 0 || y == 0 || x == 5 || y == 5;
                    if (border) { if (white > 2) return false; }
                    else
                    {
                        if (white > 2 && white < 23) return false;
                        bits = (ushort)((bits << 1) | (white >= 23 ? 1 : 0));
                    }
                }
            id = Match(bits);
            if (id < 0) return false;
            // Require a clear margin around every side, not just a black rectangle in artwork.
            int quietWhite = 0;
            for (int side = 0; side < 4; side++)
                for (int i = 0; i < 12; i++)
                {
                    float t = (i + 0.5f) / 12;
                    float u = side == 0 ? -0.08f : side == 1 ? 1.08f : t;
                    float v = side == 2 ? -0.08f : side == 3 ? 1.08f : t;
                    if (!Sample(gray, w, h, Project(quad, u, v), out byte value)) return false;
                    if (value > threshold && value > darkest + contrast * 0.5f) quietWhite++;
                }
            return quietWhite >= 44;
        }

        // Unit square -> arbitrary convex quad homography (including perspective division).
        private static Point Project(List<Point> q, float u, float v)
        {
            float dx1 = q[1].X - q[2].X, dx2 = q[3].X - q[2].X;
            float dy1 = q[1].Y - q[2].Y, dy2 = q[3].Y - q[2].Y;
            float sx = q[0].X - q[1].X + q[2].X - q[3].X;
            float sy = q[0].Y - q[1].Y + q[2].Y - q[3].Y;
            float det = dx1 * dy2 - dx2 * dy1;
            if (Math.Abs(det) < 0.001f) return new Point(float.NaN, float.NaN);
            float g = (sx * dy2 - dx2 * sy) / det, k = (dx1 * sy - sx * dy1) / det;
            float divisor = g * u + k * v + 1;
            return new Point(((q[1].X - q[0].X + g * q[1].X) * u +
                (q[3].X - q[0].X + k * q[3].X) * v + q[0].X) / divisor,
                ((q[1].Y - q[0].Y + g * q[1].Y) * u +
                (q[3].Y - q[0].Y + k * q[3].Y) * v + q[0].Y) / divisor);
        }

        private static bool Sample(byte[] gray, int w, int h, Point p, out byte value)
        {
            value = 0;
            if (float.IsNaN(p.X) || float.IsNaN(p.Y) || p.X < 0 || p.Y < 0 || p.X > w - 1 || p.Y > h - 1)
                return false;
            value = gray[(int)Math.Round(p.Y) * w + (int)Math.Round(p.X)];
            return true;
        }

        private static float Cross(Point a, Point b, Point c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        private static float Distance(Point a, Point b) =>
            (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        private static float Area(List<Point> points)
        {
            float area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Point a = points[i], b = points[(i + 1) % points.Count];
                area += a.X * b.Y - a.Y * b.X;
            }
            return Math.Abs(area) * 0.5f;
        }

        private static int Otsu(int[] histogram, int count)
        {
            double sum = 0, backgroundSum = 0, best = -1;
            for (int i = 0; i < 256; i++) sum += (double)i * histogram[i];
            int background = 0, threshold = 0;
            for (int i = 0; i < 256; i++)
            {
                background += histogram[i]; backgroundSum += (double)i * histogram[i];
                int foreground = count - background;
                if (background == 0 || foreground == 0) continue;
                double delta = backgroundSum / background - (sum - backgroundSum) / foreground;
                double variance = (double)background * foreground * delta * delta;
                if (variance > best) { best = variance; threshold = i; }
            }
            return threshold;
        }
    }
}
