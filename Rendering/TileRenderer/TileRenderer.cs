using Mapster.Common.MemoryMappedTypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Mapster.Rendering;

// Define enumerations for different types of features
public enum LandscapeType
{
    Forest,
    Orchard,
    Residential,
    Plain,
    Water,
}

public enum RoadType
{
    Highway,
}

public enum RiverType
{
    Waterway,
}

// Define a bit flag enumeration for feature properties
[Flags]
public enum FeatureProperties
{
    None = 0,
    HasHighway = 1,
    IsWaterway = 2,
    IsBoundary = 4,
    IsRailway = 8,
}


public static class TileRenderer
{
    public static BaseShape Tessellate(this MapFeatureData feature, ref BoundingBox boundingBox, ref PriorityQueue<BaseShape, int> shapes)
    {
        BaseShape? baseShape = null;

        var featureType = feature.Type;
            var properties = GetFeatureProperties(feature);

            if ((properties & FeatureProperties.HasHighway) != 0)
            {
                var coordinates = feature.Coordinates;
                var road = new Road(coordinates);
                baseShape = road;
                shapes.Enqueue(road, road.ZIndex);
            }
            else if ((properties & FeatureProperties.IsWaterway) != 0 && featureType != GeometryType.Point)
            {
                var coordinates = feature.Coordinates;
                var waterway = new Waterway(coordinates, featureType == GeometryType.Polygon);
                baseShape = waterway;
                shapes.Enqueue(waterway, waterway.ZIndex);
            }
            else if ((properties & FeatureProperties.IsBoundary) != 0)
            {
                var coordinates = feature.Coordinates;
                var border = new Border(coordinates);
                baseShape = border;
                shapes.Enqueue(border, border.ZIndex);
            }
            // Handle other feature types and properties using bitwise comparisons

            if (baseShape != null)
            {
                for (var j = 0; j < baseShape.ScreenCoordinates.Length; ++j)
                {
                    boundingBox.MinX = Math.Min(boundingBox.MinX, baseShape.ScreenCoordinates[j].X);
                    boundingBox.MaxX = Math.Max(boundingBox.MaxX, baseShape.ScreenCoordinates[j].X);
                    boundingBox.MinY = Math.Min(boundingBox.MinY, baseShape.ScreenCoordinates[j].Y);
                    boundingBox.MaxY = Math.Max(boundingBox.MaxY, baseShape.ScreenCoordinates[j].Y);
                }
            }

            return baseShape;
        }

        private static FeatureProperties GetFeatureProperties(MapFeatureData feature)
        {
            var featureProps = FeatureProperties.None;

            // Map the string properties to corresponding feature properties
            if (feature.Properties.Any(p => p.Key == "highway" && MapFeature.HighwayTypes.Any(v => p.Value.StartsWith(v))))
            {
                featureProps |= FeatureProperties.HasHighway;
            }

            if (feature.Properties.Any(p => p.Key.StartsWith("water")) && feature.Type != GeometryType.Point)
            {
                featureProps |= FeatureProperties.IsWaterway;
            }

            if (Border.ShouldBeBorder(feature))
            {
                featureProps |= FeatureProperties.IsBoundary;
            }

            // Map other properties as needed

            return featureProps;
        }

    public static Image<Rgba32> Render(this PriorityQueue<BaseShape, int> shapes, BoundingBox boundingBox, int width, int height)
    {
        var canvas = new Image<Rgba32>(width, height);

        // Calculate the scale for each pixel, essentially applying a normalization
        var scaleX = canvas.Width / (boundingBox.MaxX - boundingBox.MinX);
        var scaleY = canvas.Height / (boundingBox.MaxY - boundingBox.MinY);
        var scale = Math.Min(scaleX, scaleY);

        // Background Fill
        canvas.Mutate(x => x.Fill(Color.White));
        while (shapes.Count > 0)
        {
            var entry = shapes.Dequeue();
            // FIXME: Hack
            if (entry.ScreenCoordinates.Length < 2)
            {
                continue;
            }
            entry.TranslateAndScale(boundingBox.MinX, boundingBox.MinY, scale, canvas.Height);
            canvas.Mutate(x => entry.Render(x));
        }

        return canvas;
    }

    public struct BoundingBox
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;
    }
}
