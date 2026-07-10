using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Vetheprevit.MoCau
{
    public static class GeometryUtils
    {
        public static List<Solid> GetAllSolids(Element element, Options options)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (options == null) throw new ArgumentNullException(nameof(options));

            List<Solid> solids = new List<Solid>();
            GeometryElement geometry = element.get_Geometry(options);
            CollectSolids(geometry, solids);
            return solids;
        }

        private static void CollectSolids(GeometryElement geometry, ICollection<Solid> solids)
        {
            if (geometry == null) return;

            foreach (GeometryObject geometryObject in geometry)
            {
                if (geometryObject is Solid solid)
                {
                    if (solid.Faces.Size > 0 && solid.Volume > 0.0)
                    {
                        solids.Add(solid);
                    }
                }
                else if (geometryObject is GeometryInstance geometryInstance)
                {
                    CollectSolids(geometryInstance.GetInstanceGeometry(), solids);
                }
            }
        }
    }

    public class AbutmentCoordinateSystem
    {
        public XYZ Origin { get; set; }
        public XYZ BasisX { get; set; }
        public XYZ BasisY { get; set; }
        public XYZ BasisZ { get; set; }

        public static AbutmentCoordinateSystem FromInstance(FamilyInstance instance)
        {
            Transform transform = instance.GetTransform();
            return new AbutmentCoordinateSystem
            {
                Origin = transform.Origin,
                BasisX = transform.BasisX.Normalize(),
                BasisY = transform.BasisY.Normalize(),
                BasisZ = transform.BasisZ.Normalize()
            };
        }
    }

    public class AbutmentGeoInfo
    {
        public AbutmentCoordinateSystem CoordinateSystem { get; set; }
        public int SourceSolidCount { get; set; }

        public XYZ AbsBasePt1 { get; set; }
        public XYZ StemThicknessDir { get; set; }
        public XYZ StemLongDir { get; set; }

        public double MinZ { get; set; }
        public double MinUpZ { get; set; }
        public double MaxZ { get; set; }

        public double FootMinL { get; set; }
        public double FootMaxL { get; set; }
        public double FootMinT { get; set; }
        public double FootMaxT { get; set; }

        public double StemMinT { get; set; }
        public double StemMaxT { get; set; }
        public double StemMinL { get; set; }
        public double StemMaxL { get; set; }

        public double FootingHeight => MinUpZ - MinZ;
        public double FootingWidth => FootMaxT - FootMinT;
        public double FootingLength => FootMaxL - FootMinL;
        public double StemWidth => StemMaxT - StemMinT;
        public double StemLength => StemMaxL - StemMinL;
        public double StemHeight => StemMaxZ - MinUpZ;
        public double StemMaxZ { get; set; }
        public IList<AbutmentProfilePoint> StemSideProfile { get; set; } =
            new List<AbutmentProfilePoint>();
    }

    public class AbutmentProfilePoint
    {
        public double T { get; set; }
        public double Z { get; set; }

        public AbutmentProfilePoint(double t, double z)
        {
            T = t;
            Z = z;
        }
    }

    public class AbutmentGeometryReader
    {
        private const double VerticalTolerance = 0.001;
        private const double ParallelTolerance = 0.99;

        public AbutmentGeoInfo Read(Document doc, FamilyInstance abutment)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (abutment == null) throw new ArgumentNullException(nameof(abutment));
            if (!Equals(abutment.Document, doc))
                throw new ArgumentException("Mố cầu không thuộc document hiện tại.", nameof(abutment));

            Options options = new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                ComputeReferences = true
            };

            List<Solid> solids = GeometryUtils.GetAllSolids(abutment, options);
            if (solids.Count == 0)
                throw new Exception("Không tìm thấy khối Solid nào trong Family mố cầu.");

            PlanarFace bottomFace = null;
            double minZ = double.MaxValue;
            double minUpZ = double.MaxValue;
            double maxZ = double.MinValue;
            List<PlanarFace> verticalFaces = new List<PlanarFace>();

            foreach (Solid solid in solids)
            {
                foreach (Face face in solid.Faces)
                {
                    if (!(face is PlanarFace planarFace)) continue;

                    XYZ normal = planarFace.FaceNormal;
                    if (normal.IsAlmostEqualTo(-XYZ.BasisZ))
                    {
                        if (planarFace.Origin.Z < minZ)
                        {
                            minZ = planarFace.Origin.Z;
                            bottomFace = planarFace;
                        }
                    }
                    else if (normal.IsAlmostEqualTo(XYZ.BasisZ))
                    {
                        minUpZ = Math.Min(minUpZ, planarFace.Origin.Z);
                        maxZ = Math.Max(maxZ, planarFace.Origin.Z);
                    }
                    else if (Math.Abs(normal.Z) < VerticalTolerance)
                    {
                        verticalFaces.Add(planarFace);
                    }
                }
            }

            if (bottomFace == null || minUpZ == double.MaxValue)
                throw new Exception("Không nhận dạng được mặt đáy hoặc mặt trên của bệ mố.");

            AbutmentCoordinateSystem coordinateSystem =
                AbutmentCoordinateSystem.FromInstance(abutment);
            (PlanarFace stemFace1, PlanarFace stemFace2) =
                FindStemFaces(verticalFaces, coordinateSystem, minUpZ);
            XYZ stemThicknessDir = NormalizeDirection(stemFace1.FaceNormal);
            XYZ stemLongDir = XYZ.BasisZ.CrossProduct(stemThicknessDir).Normalize();

            BoundingBoxUV bottomUvBox = bottomFace.GetBoundingBox();
            XYZ basePoint = bottomFace.Evaluate(bottomUvBox.Min);

            AbutmentGeoInfo info = new AbutmentGeoInfo
            {
                CoordinateSystem = coordinateSystem,
                SourceSolidCount = solids.Count,
                AbsBasePt1 = basePoint,
                StemThicknessDir = stemThicknessDir,
                StemLongDir = stemLongDir,
                MinZ = minZ,
                MinUpZ = minUpZ,
                MaxZ = maxZ
            };

            info.StemMinT = Math.Min(
                (stemFace1.Origin - basePoint).DotProduct(stemThicknessDir),
                (stemFace2.Origin - basePoint).DotProduct(stemThicknessDir));
            info.StemMaxT = Math.Max(
                (stemFace1.Origin - basePoint).DotProduct(stemThicknessDir),
                (stemFace2.Origin - basePoint).DotProduct(stemThicknessDir));

            SetStemLongitudinalLimits(info, verticalFaces, stemFace1, stemFace2);
            SetFootingLimits(info, bottomFace);
            info.StemMaxZ = GetStemMaxZ(minUpZ, stemFace1, stemFace2);
            info.StemSideProfile = ExtractStemSideProfile(solids, info);

            Validate(info);
            return info;
        }

        private static IList<AbutmentProfilePoint> ExtractStemSideProfile(
            IEnumerable<Solid> solids,
            AbutmentGeoInfo info)
        {
            try
            {
                double sectionThickness = UnitUtils.ConvertToInternalUnits(
                    2,
                    UnitTypeId.Millimeters);
                double margin = UnitUtils.ConvertToInternalUnits(
                    1000,
                    UnitTypeId.Millimeters);
                double middleL = (info.StemMinL + info.StemMaxL) / 2;
                double minT = Math.Min(info.FootMinT, info.StemMinT) - margin;
                double maxT = Math.Max(info.FootMaxT, info.StemMaxT) + margin;
                double minZ = info.MinZ - margin;
                double maxZ = Math.Max(info.MaxZ, info.StemMaxZ) + margin;

                XYZ sectionOrigin =
                    info.AbsBasePt1 +
                    info.StemLongDir * (middleL - sectionThickness / 2) +
                    info.StemThicknessDir * minT +
                    XYZ.BasisZ * (minZ - info.MinZ);
                XYZ alongT = info.StemThicknessDir * (maxT - minT);
                XYZ alongZ = XYZ.BasisZ * (maxZ - minZ);

                CurveLoop rectangle = new CurveLoop();
                XYZ p0 = sectionOrigin;
                XYZ p1 = p0 + alongT;
                XYZ p2 = p1 + alongZ;
                XYZ p3 = p0 + alongZ;
                rectangle.Append(Line.CreateBound(p0, p1));
                rectangle.Append(Line.CreateBound(p1, p2));
                rectangle.Append(Line.CreateBound(p2, p3));
                rectangle.Append(Line.CreateBound(p3, p0));

                Solid sectionVolume = GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { rectangle },
                    info.StemLongDir,
                    sectionThickness);

                List<AbutmentProfilePoint> bestProfile = null;
                double bestArea = 0;

                foreach (Solid sourceSolid in solids)
                {
                    Solid sectionedSolid;
                    try
                    {
                        sectionedSolid = BooleanOperationsUtils.ExecuteBooleanOperation(
                            sourceSolid,
                            sectionVolume,
                            BooleanOperationsType.Intersect);
                    }
                    catch
                    {
                        continue;
                    }

                    if (sectionedSolid == null || sectionedSolid.Volume <= 0) continue;

                    foreach (Face face in sectionedSolid.Faces)
                    {
                        if (!(face is PlanarFace planarFace)) continue;
                        if (Math.Abs(planarFace.FaceNormal.DotProduct(info.StemLongDir)) <
                            ParallelTolerance)
                        {
                            continue;
                        }

                        foreach (EdgeArray edgeLoop in planarFace.EdgeLoops)
                        {
                            List<AbutmentProfilePoint> profile =
                                GetProjectedLoop(edgeLoop, planarFace, info);
                            profile = ClipProfileAbove(profile, info.MinUpZ);
                            if (profile.Count < 3) continue;

                            double area = Math.Abs(GetProfileArea(profile));
                            if (area > bestArea)
                            {
                                bestArea = area;
                                bestProfile = profile;
                            }
                        }
                    }
                }

                return bestProfile ?? new List<AbutmentProfilePoint>();
            }
            catch
            {
                // Preview may fall back to the rectangular bounds if a Family
                // contains geometry that cannot participate in Boolean operations.
                Vetheprevit.TienIch.Logger.AddWarning("⚠ Không đọc được profile nghiêng của thân mố, đang dùng biên dạng chữ nhật gần đúng.");
                return new List<AbutmentProfilePoint>();
            }
        }

        private static List<AbutmentProfilePoint> GetProjectedLoop(
            EdgeArray edgeLoop,
            Face face,
            AbutmentGeoInfo info)
        {
            List<AbutmentProfilePoint> result = new List<AbutmentProfilePoint>();
            foreach (Edge edge in edgeLoop)
            {
                Curve curve = edge.AsCurveFollowingFace(face);
                IList<XYZ> points = curve.Tessellate();
                foreach (XYZ point in points)
                {
                    double t = (point - info.AbsBasePt1)
                        .DotProduct(info.StemThicknessDir);
                    AddProfilePoint(result, new AbutmentProfilePoint(t, point.Z));
                }
            }

            if (result.Count > 1 && AreSamePoint(result[0], result[result.Count - 1]))
                result.RemoveAt(result.Count - 1);

            return result;
        }

        private static List<AbutmentProfilePoint> ClipProfileAbove(
            IList<AbutmentProfilePoint> profile,
            double minimumZ)
        {
            List<AbutmentProfilePoint> output = new List<AbutmentProfilePoint>();
            if (profile == null || profile.Count == 0) return output;

            AbutmentProfilePoint previous = profile[profile.Count - 1];
            bool previousInside = previous.Z >= minimumZ - VerticalTolerance;

            foreach (AbutmentProfilePoint current in profile)
            {
                bool currentInside = current.Z >= minimumZ - VerticalTolerance;
                if (currentInside != previousInside)
                {
                    double deltaZ = current.Z - previous.Z;
                    if (Math.Abs(deltaZ) > VerticalTolerance)
                    {
                        double ratio = (minimumZ - previous.Z) / deltaZ;
                        double t = previous.T + ratio * (current.T - previous.T);
                        AddProfilePoint(
                            output,
                            new AbutmentProfilePoint(t, minimumZ));
                    }
                }

                if (currentInside)
                    AddProfilePoint(output, current);

                previous = current;
                previousInside = currentInside;
            }

            return output;
        }

        private static void AddProfilePoint(
            ICollection<AbutmentProfilePoint> points,
            AbutmentProfilePoint point)
        {
            AbutmentProfilePoint last = points.LastOrDefault();
            if (last == null || !AreSamePoint(last, point))
                points.Add(point);
        }

        private static bool AreSamePoint(
            AbutmentProfilePoint first,
            AbutmentProfilePoint second)
        {
            return Math.Abs(first.T - second.T) < VerticalTolerance &&
                   Math.Abs(first.Z - second.Z) < VerticalTolerance;
        }

        private static double GetProfileArea(IList<AbutmentProfilePoint> profile)
        {
            double area = 0;
            for (int i = 0; i < profile.Count; i++)
            {
                AbutmentProfilePoint current = profile[i];
                AbutmentProfilePoint next = profile[(i + 1) % profile.Count];
                area += current.T * next.Z - next.T * current.Z;
            }

            return area / 2;
        }

        private static (PlanarFace First, PlanarFace Second) FindStemFaces(
            IEnumerable<PlanarFace> verticalFaces,
            AbutmentCoordinateSystem coordinateSystem,
            double footingTopZ)
        {
            List<PlanarFace> sortedFaces = verticalFaces
                .OrderByDescending(face => face.Area)
                .ToList();

            if (sortedFaces.Count < 2)
                throw new Exception("Không tìm thấy đủ mặt đứng để nhận dạng thân mố.");

            PlanarFace bestFirst = null;
            PlanarFace bestSecond = null;
            double bestScore = double.MinValue;

            for (int i = 0; i < sortedFaces.Count - 1; i++)
            {
                PlanarFace first = sortedFaces[i];
                XYZ firstNormal = first.FaceNormal.Normalize();
                XYZ longitudinalDirection =
                    XYZ.BasisZ.CrossProduct(firstNormal).Normalize();
                FaceProjectionBounds firstBounds =
                    GetFaceBounds(first, longitudinalDirection);

                if (firstBounds.MaxZ <= footingTopZ + VerticalTolerance) continue;

                for (int j = i + 1; j < sortedFaces.Count; j++)
                {
                    PlanarFace second = sortedFaces[j];
                    double normalDot =
                        firstNormal.DotProduct(second.FaceNormal.Normalize());

                    // Two physical sides of a wall normally have opposite normals.
                    if (normalDot > -ParallelTolerance) continue;

                    double separation = Math.Abs(
                        (second.Origin - first.Origin).DotProduct(firstNormal));
                    if (separation < 1.0 / 304.8) continue;

                    FaceProjectionBounds secondBounds =
                        GetFaceBounds(second, longitudinalDirection);
                    double longitudinalOverlapRatio = GetOverlapRatio(
                        firstBounds.MinLongitudinal,
                        firstBounds.MaxLongitudinal,
                        secondBounds.MinLongitudinal,
                        secondBounds.MaxLongitudinal);
                    double verticalOverlapRatio = GetOverlapRatio(
                        Math.Max(firstBounds.MinZ, footingTopZ),
                        firstBounds.MaxZ,
                        Math.Max(secondBounds.MinZ, footingTopZ),
                        secondBounds.MaxZ);

                    if (longitudinalOverlapRatio < 0.5 ||
                        verticalOverlapRatio < 0.5)
                    {
                        continue;
                    }

                    double axisAlignment = Math.Max(
                        Math.Abs(firstNormal.DotProduct(coordinateSystem.BasisX)),
                        Math.Abs(firstNormal.DotProduct(coordinateSystem.BasisY)));
                    double score =
                        Math.Min(first.Area, second.Area) *
                        longitudinalOverlapRatio *
                        verticalOverlapRatio *
                        (0.5 + 0.5 * axisAlignment);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestFirst = first;
                        bestSecond = second;
                    }
                }
            }

            if (bestFirst != null && bestSecond != null)
                return (bestFirst, bestSecond);

            // Compatibility fallback for unusual Families whose face normals are
            // not consistently oriented.
            PlanarFace fallbackFirst = sortedFaces[0];
            PlanarFace fallbackSecond = sortedFaces
                .Skip(1)
                .FirstOrDefault(face =>
                    Math.Abs(face.FaceNormal.DotProduct(fallbackFirst.FaceNormal)) > ParallelTolerance);

            if (fallbackSecond == null)
                throw new Exception("Không tìm thấy hai mặt thân mố song song.");

            return (fallbackFirst, fallbackSecond);
        }

        private static FaceProjectionBounds GetFaceBounds(
            PlanarFace face,
            XYZ longitudinalDirection)
        {
            FaceProjectionBounds bounds = new FaceProjectionBounds();
            foreach (EdgeArray edgeLoop in face.EdgeLoops)
            {
                foreach (Edge edge in edgeLoop)
                {
                    foreach (XYZ point in edge.Tessellate())
                    {
                        bounds.Include(point, longitudinalDirection);
                    }
                }
            }

            return bounds;
        }

        private static double GetOverlapRatio(
            double firstMin,
            double firstMax,
            double secondMin,
            double secondMax)
        {
            double firstLength = firstMax - firstMin;
            double secondLength = secondMax - secondMin;
            double referenceLength = Math.Min(firstLength, secondLength);
            if (referenceLength <= VerticalTolerance) return 0;

            double overlap =
                Math.Min(firstMax, secondMax) - Math.Max(firstMin, secondMin);
            return Math.Max(0, overlap) / referenceLength;
        }

        private static XYZ NormalizeDirection(XYZ direction)
        {
            XYZ normalized = direction.Normalize();
            if (normalized.X < 0 ||
                (Math.Abs(normalized.X) < VerticalTolerance && normalized.Y < 0))
            {
                normalized = -normalized;
            }

            return normalized;
        }

        private static void SetStemLongitudinalLimits(
            AbutmentGeoInfo info,
            IEnumerable<PlanarFace> verticalFaces,
            PlanarFace stemFace1,
            PlanarFace stemFace2)
        {
            info.StemMinL = double.MaxValue;
            info.StemMaxL = double.MinValue;

            foreach (PlanarFace face in verticalFaces)
            {
                bool isFirstFace = IsCoplanarWith(face, stemFace1);
                bool isSecondFace = IsCoplanarWith(face, stemFace2);
                if (!isFirstFace && !isSecondFace) continue;

                foreach (EdgeArray edgeLoop in face.EdgeLoops)
                {
                    foreach (Edge edge in edgeLoop)
                    {
                        IncludeLongitudinalPoint(info, edge.Evaluate(0));
                        IncludeLongitudinalPoint(info, edge.Evaluate(1));
                    }
                }
            }
        }

        private static bool IsCoplanarWith(PlanarFace candidate, PlanarFace reference)
        {
            bool isParallel =
                Math.Abs(candidate.FaceNormal.DotProduct(reference.FaceNormal)) > ParallelTolerance;
            double planeDistance =
                Math.Abs((candidate.Origin - reference.Origin).DotProduct(reference.FaceNormal));
            return isParallel && planeDistance < 0.01;
        }

        private static void IncludeLongitudinalPoint(AbutmentGeoInfo info, XYZ point)
        {
            double value = (point - info.AbsBasePt1).DotProduct(info.StemLongDir);
            info.StemMinL = Math.Min(info.StemMinL, value);
            info.StemMaxL = Math.Max(info.StemMaxL, value);
        }

        private static void SetFootingLimits(AbutmentGeoInfo info, PlanarFace bottomFace)
        {
            info.FootMinL = double.MaxValue;
            info.FootMaxL = double.MinValue;
            info.FootMinT = double.MaxValue;
            info.FootMaxT = double.MinValue;

            foreach (EdgeArray edgeLoop in bottomFace.EdgeLoops)
            {
                foreach (Edge edge in edgeLoop)
                {
                    IncludeFootingPoint(info, edge.Evaluate(0));
                    IncludeFootingPoint(info, edge.Evaluate(1));
                }
            }
        }

        private static void IncludeFootingPoint(AbutmentGeoInfo info, XYZ point)
        {
            double thickness = (point - info.AbsBasePt1).DotProduct(info.StemThicknessDir);
            double longitudinal = (point - info.AbsBasePt1).DotProduct(info.StemLongDir);

            info.FootMinT = Math.Min(info.FootMinT, thickness);
            info.FootMaxT = Math.Max(info.FootMaxT, thickness);
            info.FootMinL = Math.Min(info.FootMinL, longitudinal);
            info.FootMaxL = Math.Max(info.FootMaxL, longitudinal);
        }

        private static double GetStemMaxZ(
            double minUpZ,
            params PlanarFace[] stemFaces)
        {
            double stemMaxZ = minUpZ;
            foreach (PlanarFace face in stemFaces)
            {
                if (face == null) continue;

                foreach (EdgeArray edgeLoop in face.EdgeLoops)
                {
                    foreach (Edge edge in edgeLoop)
                    {
                        stemMaxZ = Math.Max(stemMaxZ, edge.Evaluate(0).Z);
                        stemMaxZ = Math.Max(stemMaxZ, edge.Evaluate(1).Z);
                    }
                }
            }

            return stemMaxZ;
        }

        private static void Validate(AbutmentGeoInfo info)
        {
            if (info.FootingLength <= 0 || info.FootingWidth <= 0 || info.FootingHeight <= 0)
                throw new Exception("Kích thước bệ mố nhận dạng được không hợp lệ.");

            if (info.StemLength <= 0 || info.StemWidth <= 0 || info.StemHeight <= 0)
                throw new Exception("Kích thước thân mố nhận dạng được không hợp lệ.");
        }

        private class FaceProjectionBounds
        {
            public double MinLongitudinal { get; private set; } = double.MaxValue;
            public double MaxLongitudinal { get; private set; } = double.MinValue;
            public double MinZ { get; private set; } = double.MaxValue;
            public double MaxZ { get; private set; } = double.MinValue;

            public void Include(XYZ point, XYZ longitudinalDirection)
            {
                double longitudinal = point.DotProduct(longitudinalDirection);
                MinLongitudinal = Math.Min(MinLongitudinal, longitudinal);
                MaxLongitudinal = Math.Max(MaxLongitudinal, longitudinal);
                MinZ = Math.Min(MinZ, point.Z);
                MaxZ = Math.Max(MaxZ, point.Z);
            }
        }
    }
}
