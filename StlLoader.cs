using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ModelPeek;

internal sealed record StlModel(
    MeshGeometry3D Geometry,
    int TriangleCount,
    double Radius);

internal static class StlLoader
{
    private const long MaximumTriangles = 5_000_000;

    public static StlModel Load(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.SequentialScan);

        if (stream.Length < 15)
        {
            throw new InvalidDataException("The file is empty or too small to be a valid STL.");
        }

        return LooksLikeBinaryStl(stream)
            ? LoadBinary(stream)
            : LoadAscii(stream);
    }

    private static bool LooksLikeBinaryStl(FileStream stream)
    {
        if (stream.Length < 84)
        {
            stream.Position = 0;
            return false;
        }

        Span<byte> header = stackalloc byte[84];
        stream.ReadExactly(header);
        stream.Position = 0;

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(header[80..84]);
        long expectedLength = 84L + count * 50L;
        if (expectedLength == stream.Length)
        {
            return true;
        }

        string start = Encoding.ASCII.GetString(header[..5]);
        return !start.Equals("solid", StringComparison.OrdinalIgnoreCase) &&
               expectedLength > 84 &&
               expectedLength <= stream.Length;
    }

    private static StlModel LoadBinary(FileStream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        reader.ReadBytes(80);
        uint triangleCount = reader.ReadUInt32();
        ValidateTriangleCount(triangleCount);

        var points = new Point3D[checked((int)triangleCount * 3)];
        Bounds bounds = Bounds.Empty;
        int pointIndex = 0;

        for (int triangle = 0; triangle < triangleCount; triangle++)
        {
            EnsureBytesRemain(stream, 50);
            reader.ReadSingle();
            reader.ReadSingle();
            reader.ReadSingle();

            for (int vertex = 0; vertex < 3; vertex++)
            {
                var point = new Point3D(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle());
                ValidatePoint(point);
                points[pointIndex++] = point;
                bounds.Include(point);
            }

            reader.ReadUInt16();
        }

        return BuildModel(points, checked((int)triangleCount), bounds);
    }

    private static StlModel LoadAscii(FileStream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: true);
        var points = new List<Point3D>();
        Bounds bounds = Bounds.Empty;

        while (reader.ReadLine() is { } line)
        {
            ReadOnlySpan<char> trimmed = line.AsSpan().Trim();
            if (!trimmed.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] parts = trimmed.ToString()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
                !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) ||
                !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
            {
                throw new InvalidDataException("The ASCII STL contains an invalid vertex.");
            }

            var point = new Point3D(x, y, z);
            ValidatePoint(point);
            points.Add(point);
            bounds.Include(point);

            if (points.Count / 3 > MaximumTriangles)
            {
                throw new InvalidDataException("This STL is too large for the current Model Peek version.");
            }
        }

        if (points.Count < 3 || points.Count % 3 != 0)
        {
            throw new InvalidDataException("No complete triangles were found in the STL.");
        }

        return BuildModel(points.ToArray(), points.Count / 3, bounds);
    }

    private static StlModel BuildModel(Point3D[] rawPoints, int triangleCount, Bounds bounds)
    {
        if (!bounds.IsValid)
        {
            throw new InvalidDataException("The STL does not contain valid geometry.");
        }

        var center = new Point3D(
            (bounds.MinX + bounds.MaxX) / 2.0,
            (bounds.MinY + bounds.MaxY) / 2.0,
            (bounds.MinZ + bounds.MaxZ) / 2.0);

        var positions = new Point3DCollection(rawPoints.Length);
        var normals = new Vector3DCollection(rawPoints.Length);
        var indices = new Int32Collection(rawPoints.Length);
        double radiusSquared = 0;

        for (int i = 0; i < rawPoints.Length; i += 3)
        {
            Point3D a = rawPoints[i] - (Vector3D)center;
            Point3D b = rawPoints[i + 1] - (Vector3D)center;
            Point3D c = rawPoints[i + 2] - (Vector3D)center;

            Vector3D normal = Vector3D.CrossProduct(b - a, c - a);
            if (normal.LengthSquared > 0.000000000001)
            {
                normal.Normalize();
            }
            else
            {
                normal = new Vector3D(0, 0, 1);
            }

            positions.Add(a);
            positions.Add(b);
            positions.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            indices.Add(i);
            indices.Add(i + 1);
            indices.Add(i + 2);

            radiusSquared = Math.Max(radiusSquared, ((Vector3D)a).LengthSquared);
            radiusSquared = Math.Max(radiusSquared, ((Vector3D)b).LengthSquared);
            radiusSquared = Math.Max(radiusSquared, ((Vector3D)c).LengthSquared);
        }

        positions.Freeze();
        normals.Freeze();
        indices.Freeze();

        var geometry = new MeshGeometry3D
        {
            Positions = positions,
            Normals = normals,
            TriangleIndices = indices
        };
        geometry.Freeze();

        return new StlModel(geometry, triangleCount, Math.Sqrt(radiusSquared));
    }

    private static void ValidateTriangleCount(uint triangleCount)
    {
        if (triangleCount == 0)
        {
            throw new InvalidDataException("The STL does not contain any triangles.");
        }

        if (triangleCount > MaximumTriangles)
        {
            throw new InvalidDataException(
                $"This STL contains {triangleCount:N0} triangles, which is too large for the current Model Peek version.");
        }
    }

    private static void ValidatePoint(Point3D point)
    {
        if (!double.IsFinite(point.X) ||
            !double.IsFinite(point.Y) ||
            !double.IsFinite(point.Z))
        {
            throw new InvalidDataException("The STL contains a coordinate that is not a finite number.");
        }
    }

    private static void EnsureBytesRemain(Stream stream, int required)
    {
        if (stream.Length - stream.Position < required)
        {
            throw new InvalidDataException("The binary STL ended before all triangles could be read.");
        }
    }

    private struct Bounds
    {
        public double MinX;
        public double MinY;
        public double MinZ;
        public double MaxX;
        public double MaxY;
        public double MaxZ;
        public bool IsValid;

        public static Bounds Empty => new()
        {
            MinX = double.PositiveInfinity,
            MinY = double.PositiveInfinity,
            MinZ = double.PositiveInfinity,
            MaxX = double.NegativeInfinity,
            MaxY = double.NegativeInfinity,
            MaxZ = double.NegativeInfinity,
            IsValid = false
        };

        public void Include(Point3D point)
        {
            MinX = Math.Min(MinX, point.X);
            MinY = Math.Min(MinY, point.Y);
            MinZ = Math.Min(MinZ, point.Z);
            MaxX = Math.Max(MaxX, point.X);
            MaxY = Math.Max(MaxY, point.Y);
            MaxZ = Math.Max(MaxZ, point.Z);
            IsValid = true;
        }
    }
}
