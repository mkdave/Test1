using BusinessObjects;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMapLibrary.Helpers;
using GMapLibrary.MapObjects;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using Location = GMapLibrary.MapObjects.Location;
using MapType = GMapLibrary.MapObjects.MapType;

namespace GMapLibrary
{
    public class DistributionCircuitMap
    {
        LocationRectangle _BoundingBox;
        MapOverview overview;
        Circuit circuit;
        BusinessObjects.Attribute attribute;
        AttributeGroup attributeGroup;
        List<BusinessObjects.Attribute> attributeList;
        List<LegendItem> legendItemList;
        GMapControl mapControl;
        MapPrintProperties printProperties;
        string imagePath;

        private const string OverlayPoles = "OverlayPoles";
        private const string OverlaySpans = "OverlaySpans";
        private const string OverlayAttributeSpans = "OverlayAttributeSpans";
        private const string OverlayPOIs = "OverlayPOIs";
        private const string OverlayLegend = "OverlayLegend";
        private const string OverlaySubStation = "OverlaySubStation";
        private const string OverlayMapBoxesLabels = "OverlayMapBoxesLabels";
        private const string OverlayMapBoxes = "OverlayMapBoxes";
        private const string OverlayCircuitBoundingBox = "OverlayCircuitBoundingBox";

        bool doneTileLoad = false;
        double scaleOneInchMeters = 0;
        double pageWidthInches = 17.0;
        double pageHeightInches = 11.0;
        double pageBottomMarginInches = 1.0;
        double dpi = 96.0;
        double drawingSizeScaleFactor = 2.0;//make bigger so when we crop the image we don't degrade image
        double drawingWidth;
        double drawingHeight;
        double boxWidthInMeters;
        double boxHeightInMeters;
        int TextX = 0;
        int TextY = 0;
        int FontSize = 10;
        bool SatelliteBackground = false;

        public DistributionCircuitMap(double widthInches, double heightInches, double scaleOneInchMeters)
        {
            if (scaleOneInchMeters == 0.0)
            {
                this.scaleOneInchMeters = 118;
            }
            else
            {
                this.scaleOneInchMeters = scaleOneInchMeters;
            }
            scaleOneInchMeters = widthInches * this.scaleOneInchMeters;
            pageWidthInches = widthInches;
            pageHeightInches = heightInches;

            TextX = 10;
            TextY = 10;
            FontSize = 9;

            drawingWidth = pageWidthInches * dpi;
            drawingHeight = (pageHeightInches - pageBottomMarginInches) * dpi;
            if (pageWidthInches > pageHeightInches)
            {
                boxWidthInMeters = scaleOneInchMeters;
                boxHeightInMeters = scaleOneInchMeters * (pageHeightInches - pageBottomMarginInches) / pageWidthInches;
            }
            else
            {
                boxWidthInMeters = scaleOneInchMeters * pageWidthInches / (pageHeightInches - pageBottomMarginInches);
                boxHeightInMeters = scaleOneInchMeters;
            }

            mapControl = new GMapControl();
        }

        public MapOverview BuildMaps(Circuit circuit, List<BusinessObjects.Attribute> attributeList, MapPrintProperties mapPrintProperties, string imageFolder)
        {
            this.circuit = circuit;
            this.attributeList = attributeList;
            printProperties = mapPrintProperties;
            imagePath = imageFolder;

            mapControl.Size = new Size((int)(drawingWidth * drawingSizeScaleFactor), (int)(drawingHeight * drawingSizeScaleFactor));
            mapControl.OnTileLoadComplete += new TileLoadComplete(this.GMap_OnTileLoadComplete);
            mapControl.Load += new EventHandler(this.GMap_Load);
            mapControl.CreateControl();
            doneTileLoad = false;

            LoadMapTypes(printProperties.MapType);
            AddPolesToMap();
            BuildOverviewMap();
            CaptureOverviewImage();
            CaptureDetailImages();
            return overview;
        }

        private void AddPolylines()
        {
            foreach (Span span in circuit.SpanList)
            {
                if (span != null)
                {
                    GMapOverlay routes = new GMapOverlay(OverlaySpans);
                    List<PointLatLng> points = new List<PointLatLng>();
                    foreach (GeographyPoint point in span.Points)
                    {
                        points.Add(new PointLatLng(point.Latitude, point.Longitude));
                    }
                    GMapRoute route = new GMapRoute(points, circuit.CircuitName);
                    route.Stroke = new Pen(Color.Red, 3);
                    if (span.Phase == 2)
                    {
                        route.Stroke.DashStyle = DashStyle.Custom;
                        route.Stroke.DashPattern = new float[] { 4, 1, 1, 1 };
                    }
                    else if (span.Phase == 3)
                    {
                        route.Stroke.DashStyle = DashStyle.Custom;
                        route.Stroke.DashPattern = new float[] { 4, 1, 1, 1, 1, 1, 1, 1 };
                    }

                    routes.Routes.Add(route);
                    mapControl.Overlays.Add(routes);
                }
            }
        }

        private void AddAttributeSpansToMap()
        {
            GMapOverlay routes = new GMapOverlay(OverlayAttributeSpans);

            if (circuit.CircuitAttributeList == null) return;
            if (attribute == null && attributeGroup == null) return;
            if (attributeList == null) return;
            foreach (CircuitAttribute ca in circuit.CircuitAttributeList)
            {
                Brush lineBrush = GetBrush(ca.AttributePKID, ca.AttributeDetailPKID);
                if (attribute != null && ca.AttributePKID == attribute.AttributePKID)
                {
                    if (attribute.AttributeGroupList != null && attribute.AttributeGroupList.Count > 0) lineBrush = GetBrushByGroup(ca.AttributePKID, ca.AttributeGroupPKID);
                }

                if (ca.Points == null) continue;
                List<PointLatLng> pointList = new List<PointLatLng>();
                foreach (GeographyPoint point in ca.Points)
                {
                    pointList.Add(new PointLatLng(point.Latitude, point.Longitude));
                }

                GMapRoute route = new GMapRoute(pointList, circuit.CircuitName);
                route.Stroke = new Pen(lineBrush, 5);
                if (ca.Phase == 2)
                {
                    route.Stroke.DashStyle = DashStyle.Custom;
                    route.Stroke.DashPattern = new float[] { 4, 1, 1, 1 };//dashes/spaces are as wide as the line thickness. 4 long dash, 1 space, 1 dot, 1 space... back to 4..
                }
                else if (ca.Phase == 3)
                {
                    route.Stroke.DashStyle = DashStyle.Custom;
                    route.Stroke.DashPattern = new float[] { 3F, .75F, .75F, .75F, .75F, .75F, .75F, .75F };
                }

                routes.Routes.Add(route);
                mapControl.Overlays.Add(routes);
            }
        }

        private void AddPOIsToMap()
        {
            if (circuit == null || circuit.POIList == null) return;
            GMapOverlay markerOverlay = new GMapOverlay(OverlayPOIs);
            foreach (CircuitPOI poi in circuit.POIList)
            {
                mapControl.Overlays.Add(markerOverlay);
                try
                {
                    GMapMarkerWithLabel marker = new GMapMarkerWithLabel(new PointLatLng(poi.Latitute, poi.Longitude), poi.Title, GetImagePath(poi.Icon), 0, 16, 0, TextX, TextY, FontSize, SatelliteBackground);
                    markerOverlay.Markers.Add(marker);
                }
                catch (Exception)
                {
                    GMapMarkerWithLabel marker = new GMapMarkerWithLabel(new PointLatLng(poi.Latitute, poi.Longitude), poi.Title, GMap.NET.WindowsForms.Markers.GMarkerGoogleType.blue_small, 0, TextX, TextY, FontSize, SatelliteBackground);
                    markerOverlay.Markers.Add(marker);
                }
            }
        }

        private void AddPolesToMap()
        {
            GMapOverlay markerOverlay = new GMapOverlay(OverlayPoles);
            mapControl.Overlays.Add(markerOverlay);
            string label = string.Empty;
            foreach (Pole pole in circuit.Poles)
            {
                long poleNum = 0;
                if (long.TryParse(pole.Number, out poleNum) == true)
                {
                    label = pole.PolePrefix.Trim() + poleNum.ToString();
                }
                else
                {
                    label = pole.PolePrefix.Trim() + pole.Number;
                }

                string path = GetImagePath("RoundMarkerSmallGray");
                double angle = 0.0;
                int textX = 0;
                int textY = 8;
                GMapMarkerWithLabel marker = new GMapMarkerWithLabel(new PointLatLng(pole.Latitude, pole.Longitude), label, path , textX, textY, angle, TextX, TextY, FontSize, SatelliteBackground);
                markerOverlay.Markers.Add(marker);

            }
        }

        private void AddSubStationToMap()
        {
            if (circuit == null || circuit.CircuitSubStation == null) return;

            GMapOverlay markerOverlay = new GMapOverlay(OverlaySubStation);
            mapControl.Overlays.Add(markerOverlay);
            GMapMarkerWithLabel marker = new GMapMarkerWithLabel(new PointLatLng(circuit.CircuitSubStation.Latitude, circuit.CircuitSubStation.Longitude), circuit.CircuitSubStation.Name, GetImagePath("substationmarker"), 0, 8, 0, TextX, TextY, FontSize, SatelliteBackground);
            markerOverlay.Markers.Add(marker);
        }

        private void BuildLegend()
        {
            legendItemList = new List<LegendItem>();
            if (attributeGroup != null)
            {
                foreach (AttributeDetail detail in attributeGroup.DetailList)
                {
                    legendItemList.Add(new LegendItem() { Color = detail.Color, Description = detail.Description });
                }
            }
            else if (attribute != null)
            {
                if (attribute.AttributeGroupList != null && attribute.AttributeGroupList.Count == 0)
                {
                    foreach (AttributeDetail detail in attribute.AttributeDetailList)
                    {
                        legendItemList.Add(new LegendItem() { Color = detail.Color, Description = detail.Description });
                    }
                }
                else if (attribute.AttributeGroupList != null && attribute.AttributeGroupList.Count > 0)
                {
                    foreach (AttributeGroup group in attribute.AttributeGroupList)
                    {
                        legendItemList.Add(new LegendItem() { Color = group.Color, Description = group.Description });
                    }
                }
            }

        }

        private void AddLegendToBitmap(Bitmap _bitmap, Rectangle rect)
        {
            if (legendItemList == null || legendItemList == null) return;
            int X = (int)((double)rect.Width * 85 / 100.0) + rect.Left;
            int Y = (int)((double)rect.Height * 5 / 100.0) + rect.Top; ;
            int colorSwatchWidth = 32;
            int colorSwatchHeight = 32;
            int legendFontSize = 24;
            int legendTextX = 32;
            int legendTextY = 0;

            foreach (LegendItem item in legendItemList)
            {
                Bitmap bitmap = new Bitmap(colorSwatchWidth, colorSwatchHeight);
                using (Graphics gfx = Graphics.FromImage(bitmap))
                {
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(0, 0, 0)))
                    {
                        gfx.FillRectangle(brush, 0, 0, colorSwatchWidth, colorSwatchHeight);
                    }
                    using (Brush brush = ImageHelper.GetBrush(item.Color))
                    {
                        gfx.FillRectangle(brush, 1, 1, colorSwatchWidth - 2, colorSwatchHeight - 2);
                    }
                }

                Graphics g = Graphics.FromImage(_bitmap);
                g.DrawImage(bitmap, X, Y, colorSwatchWidth, colorSwatchHeight);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Font font = new Font("Arial", legendFontSize);
                Brush _brush = Brushes.Black;
                if (SatelliteBackground) _brush = Brushes.White;
                g.DrawString(item.Description, font, _brush, (float)X + (float)legendTextX, (float)Y + (float)legendTextY);
                g.Flush();
                Y += (int)((double)colorSwatchHeight * 3.0 / 2.0);
            }
        }

        private void AddLegendToMap(Rectangle rect)
        {
            if (legendItemList == null || legendItemList == null) return;
            int X = (int)((double)rect.Width * 85 / 100.0) + rect.Left;
            int Y = (int)((double)rect.Height * 5 / 100.0) + rect.Top; ;
            int width = 32;
            int height = 32;
            int legendFontSize = 24;
            int legendTextX = width * 2 / 3;
            int legendTextY = 0;
            double legendAngle = 0.0;
            int legendAnchorX = 0;
            int legendAnchorY = 0;

            GMapOverlay markerOverlay = new GMapOverlay(OverlayLegend);
            foreach (LegendItem item in legendItemList)
            {
                mapControl.Overlays.Add(markerOverlay);
                Bitmap bitmap = new Bitmap(width, height);
                using (Graphics gfx = Graphics.FromImage(bitmap))
                {
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(0, 0, 0)))
                    {
                        gfx.FillRectangle(brush, 0, 0, width, height);
                    }
                    using (Brush brush = ImageHelper.GetBrush(item.Color))
                    {
                        gfx.FillRectangle(brush, 1, 1, width - 2, height - 2);
                    }
                }

                PointLatLng _pt = mapControl.FromLocalToLatLng(X, Y);
                PointLatLng pt = new PointLatLng(_pt.Lat, _pt.Lng);
                GPoint local = mapControl.FromLatLngToLocal(pt);
                GMapLegendItem marker = new GMapLegendItem(pt, item.Description, bitmap, legendAnchorX, legendAnchorY, legendAngle, legendTextX, legendTextY, legendFontSize, SatelliteBackground);
                markerOverlay.Markers.Add(marker);
                Y += (int)((double)height * 3.0 / 2.0);
            }
        }

        private void BuildMapGeometry()
        {
            if (circuit.SpanList != null) AddPolylines();
            if (circuit.CircuitAttributeList != null) AddAttributeSpansToMap();
            if (circuit.CircuitSubStation != null) AddSubStationToMap();
            DrawBoundingBox();
        }

        private void BuildOverviewMap()
        {
            List<Location> allBoxes = new List<Location>();

            BuildMapGeometry();
            AddPOIsToMap();
            BuildLegend();

            overview = new MapOverview(pageWidthInches, pageHeightInches);
            overview.MapPageWidth = GISHelper.GetMetersBetweenPoints(circuit.BoundingBox[0].Latitude, circuit.BoundingBox[0].Longitude, circuit.BoundingBox[1].Latitude, circuit.BoundingBox[1].Longitude);
            overview.MapPageHeight = GISHelper.GetMetersBetweenPoints(circuit.BoundingBox[1].Latitude, circuit.BoundingBox[1].Longitude, circuit.BoundingBox[2].Latitude, circuit.BoundingBox[2].Longitude);
            overview.ColumnCount = (int)Math.Ceiling(overview.MapPageWidth / boxWidthInMeters);
            overview.RowCount = (int)Math.Ceiling(overview.MapPageHeight / boxHeightInMeters);

            Location startingPoint = GISHelper.GetNorthWestCorner(circuit.BoundingBox);
            overview.LocationStartPoint = GISHelper.GetNorthWestCorner(circuit.BoundingBox);
            overview.MapOverviewArea = _BoundingBox;
            double longitudeAmount = GISHelper.GetGPSDistanceXMetersEast(circuit.BoundingBox[0].Latitude, circuit.BoundingBox[0].Longitude, boxWidthInMeters);
            double latitudeAmount = GISHelper.GetGPSDistanceXMetersSouth(circuit.BoundingBox[0].Latitude, circuit.BoundingBox[0].Longitude, boxHeightInMeters);
            int PageCount = 0;
            overview.PageList = new List<MapPage>();
            GMapOverlay routes = new GMapOverlay(OverlayMapBoxes);
            GMapOverlay markerOverlay = new GMapOverlay(OverlayMapBoxesLabels);

            for (int i = 1; i <= overview.ColumnCount; i++)
            {
                for (int j = 1; j <= overview.RowCount; j++)
                {
                    MapPage mapPage = new MapPage();
                    mapPage.MapArea = new LocationRectangle(startingPoint, new Location(startingPoint.Latitude - latitudeAmount, startingPoint.Longitude + longitudeAmount));

                    List<PointLatLng> points = new List<PointLatLng>();
                    List<Location> locations = new List<Location>();
                    locations.Add(mapPage.MapArea.Northeast);
                    locations.Add(mapPage.MapArea.Southeast);
                    locations.Add(mapPage.MapArea.Southwest);
                    locations.Add(mapPage.MapArea.Northwest);
                    locations.Add(mapPage.MapArea.Northeast);

                    foreach (Location location in locations)
                    {
                        allBoxes.Add(location);
                        points.Add(new PointLatLng(location.Latitude, location.Longitude));
                    }

                    GMapRoute route = new GMapRoute(points, "");
                    route.Stroke = new Pen(Color.Red, 2);
                    routes.Routes.Add(route);
                    mapControl.Overlays.Add(routes);

                    _BoundingBox = GISHelper.GetBoundingBox(locations);

                    if (RectangleContainsPoles(locations) == true)
                    {
                        mapPage.Column = i;
                        mapPage.Row = j;
                        PageCount = PageCount + 1;
                        mapPage.PageNumber = PageCount;
                        overview.PageList.Add(mapPage);

                        mapControl.Overlays.Add(markerOverlay);
                        PointLatLng point = new PointLatLng(mapPage.MapArea.Center.Latitude, mapPage.MapArea.Center.Longitude);
                        GMapMarkerWithLabel marker = new GMapMarkerWithLabel(point, PageCount.ToString(), GetImagePath("OnePixel"), 0, 8, 0, 0, -10, 40, SatelliteBackground);
                        markerOverlay.Markers.Add(marker);
                    }
                    startingPoint.Latitude = startingPoint.Latitude - latitudeAmount;

                }
                startingPoint.Longitude = startingPoint.Longitude + longitudeAmount;
                startingPoint.Latitude = overview.LocationStartPoint.Latitude;
            }

            overview.MapOverviewArea = GISHelper.GetBoundingBox(allBoxes);
            ZoomToBox(overview.MapOverviewArea);
        }

        protected string GetImagePath(string name)
        {
            string path = System.IO.Path.Combine(imagePath, string.Concat(name, ".png"));
            if (!System.IO.File.Exists(path)) path = System.IO.Path.Combine(imagePath, "NotFound.png");
            return path;
        }

        private Bitmap GetMapBitmap()
        {
            Bitmap b = new Bitmap(mapControl.Width, mapControl.Height);
            return b;
        }

        private void CaptureOverviewImage()
        {
            ShowLayer(OverlayPOIs, false);
            ShowLayer(OverlayCircuitBoundingBox, false);
            ShowLayer(OverlayMapBoxes, true);
            ShowLayer(OverlayMapBoxesLabels, true);
            ShowLayer(OverlayPoles, false);
            ShowLayer(OverlaySubStation, true);
            ShowLayer(OverlayLegend, printProperties.ShowLegendOnOverView);

            doneTileLoad = false;
            ZoomToBox(overview.MapOverviewArea);


            int i = 1;
            while (!doneTileLoad)
            {
                System.Threading.Thread.Sleep(1000);
                if (i++ == 10)
                {
                    doneTileLoad = true;
                }
            }

            //first attempt - hard to do on scaled pages, switched to bitmap approach
            //AddLegendToMap(new System.Drawing.Rectangle(0, 0, myMap.Width, myMap.Height));

            Bitmap bitmap = GetMapBitmap();
            mapControl.DrawToBitmap(bitmap, new Rectangle(0, 0, mapControl.Width, mapControl.Height));
            if (printProperties.ShowLegendOnOverView) AddLegendToBitmap(bitmap, new Rectangle(0, 0, mapControl.Width, mapControl.Height));

            System.IO.MemoryStream stream = new System.IO.MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
            stream.Position = 0;
            overview.ImageStream = stream;
        }

        private void CaptureDetailImages()
        {
            ShowLayer(OverlayPOIs, true);
            ShowLayer(OverlayPoles, true);
            ShowLayer(OverlayCircuitBoundingBox, false);
            ShowLayer(OverlayMapBoxes, false);
            ShowLayer(OverlayMapBoxesLabels, false);
            ShowLayer(OverlaySubStation, true);
            ShowLayer(OverlayLegend, false);

            foreach (MapPage mapPage in overview.PageList)
            {
                doneTileLoad = false;
                ZoomToBox(mapPage.MapArea);
                int i = 1;
                while (!doneTileLoad)
                {
                    System.Threading.Thread.Sleep(1000);
                    if (i++ == 10)
                    {
                        doneTileLoad = true;
                    }
                }

                using (Bitmap b = GetMapBitmap())
                {
                    GPoint nw = mapControl.FromLatLngToLocal(new PointLatLng(mapPage.MapArea.Northwest.Latitude, mapPage.MapArea.Northwest.Longitude));
                    GPoint se = mapControl.FromLatLngToLocal(new PointLatLng(mapPage.MapArea.Southeast.Latitude, mapPage.MapArea.Southeast.Longitude));
                    int w = (int)Math.Abs(se.X - nw.X);
                    int h = (int)Math.Abs(nw.Y - se.Y);
                    int x = (int)Math.Min(se.X, nw.X);
                    int y = (int)Math.Min(se.Y, nw.Y);
                    if (w > mapControl.Width) w = mapControl.Width;
                    if (h > mapControl.Height) h = mapControl.Height;

                    mapControl.DrawToBitmap(b, new Rectangle(0, 0, mapControl.Width, mapControl.Height));
                    Rectangle rect = new Rectangle(x, y, w, h);
                    using (Bitmap croppedImage = b.Clone(rect, b.PixelFormat))
                    {

                        using (Bitmap finalImage = ImageHelper.ResizeImage(croppedImage, mapControl.Width, mapControl.Height))
                        {
                            if (printProperties.ShowLegendOnPages)
                            {
                                AddLegendToBitmap(finalImage, new Rectangle(0, 0, mapControl.Width, mapControl.Height));
                            }

                            System.IO.MemoryStream stream = new System.IO.MemoryStream();
                            finalImage.Save(stream, ImageFormat.Jpeg);
                            stream.Position = 0;
                            mapPage.ImageStream = stream;
                        }
                    }
                }
            }
        }

        private void DrawBoundingBox()
        {
            GMapOverlay routes = new GMapOverlay(OverlayCircuitBoundingBox);
            List<PointLatLng> pointList = new List<PointLatLng>();
            List<Location> locationList = new List<Location>();
            foreach (GeographyPoint point in circuit.BoundingBox)
            {
                pointList.Add(new PointLatLng(point.Latitude, point.Longitude));
                locationList.Add(new Location(point.Latitude, point.Longitude));
            }
            GMapRoute route = new GMapRoute(pointList, string.Empty);
            route.Stroke = new Pen(Color.Black, 2);
            routes.Routes.Add(route);
            mapControl.Overlays.Add(routes);

            _BoundingBox = GISHelper.GetBoundingBox(locationList);
        }

        private string GetAttributeDetailColor(int AttributeID, int DetailID)
        {
            string color = "000000";
            foreach (BusinessObjects.Attribute attribute in attributeList)
            {
                if (attribute.AttributePKID == AttributeID)
                {
                    foreach (AttributeDetail detail in attribute.AttributeDetailList)
                    {
                        if (detail.AttributeDetailPKID == DetailID) return detail.Color;
                    }
                }
            }
            return color;
        }

        private string GetAttributeGroupDetailColor(int AttributeID, int GroupID)
        {
            string color = "000000";
            foreach (BusinessObjects.Attribute attribute in attributeList)
            {
                if (attribute.AttributePKID == AttributeID)
                {
                    foreach (AttributeGroup _Group in attribute.AttributeGroupList)
                    {
                        if (_Group.ID == GroupID) return _Group.Color;
                    }
                }
            }
            return color;
        }

        private Brush GetBrush(int attributePkid, int detailPkid)
        {
            string color = GetAttributeDetailColor(attributePkid, detailPkid);
            return ImageHelper.GetBrush(color);
        }

        private Brush GetBrushByGroup(int attributePkid, int groupID)
        {
            string color = GetAttributeGroupDetailColor(attributePkid, groupID);
            return ImageHelper.GetBrush(color);
        }

        private List<BusinessObjects.Attribute> GetCircuitAttributes()
        {
            List<BusinessObjects.Attribute> list = null;
            List<int> attributeIds = (from a in circuit.CircuitAttributeList
                                      orderby a.AttributePKID
                                      select a.AttributePKID).Distinct().ToList();
            list = (from a in attributeList
                    where attributeIds.Contains(a.AttributePKID)
                    orderby a.Description
                    select a).ToList();

            return list;
        }

        private List<AttributeGroup> GetCircuitAttributeGroups(BusinessObjects.Attribute attribute)
        {
            List<AttributeGroup> list = null;
            if (attribute == null) return list;
            List<int> attributeIds = (from a in circuit.CircuitAttributeList
                                      orderby a.AttributeGroupPKID
                                      select a.AttributeGroupPKID).Distinct().ToList();
            list = (from a in attribute.AttributeGroupList
                    where attributeIds.Contains(a.ID)
                    orderby a.Description
                    select a).ToList();

            return list;
        }

        private void GMap_Load(object sender, EventArgs e)
        {
            GMaps.Instance.Mode = AccessMode.ServerOnly;
            mapControl.Position = new PointLatLng(42, -74);
            mapControl.MaxZoom = 21;
            mapControl.MinZoom = 2;
            mapControl.Zoom = 16;
            mapControl.ShowCenter = false;

            SatelliteBackground = false;
            mapControl.MapProvider = MapProviderHelper.GetMapProvider((MapTileTypes)Enum.Parse(typeof(MapTileTypes), printProperties.TileType), out SatelliteBackground);

            GMapProvider.UserAgent = "Open Street";//don't delete this is used to make open street work
        }

        private void GMap_OnTileLoadComplete(long ms)
        {
            doneTileLoad = true;
        }

        private void LoadMapTypes(string type)
        {
            List<MapType> MapTypes = new List<MapType>();
            MapType mt = new MapType();
            mt.Content = "Planning Map";
            mt.Tag = "Planning";
            mt.Background = new SolidBrush(Color.AntiqueWhite);
            MapTypes.Add(mt);

            List<BusinessObjects.Attribute> circuitAttributes = GetCircuitAttributes();
            BusinessObjects.Attribute AttributeWithGroups = null;
            if (circuitAttributes != null)
            {
                foreach (BusinessObjects.Attribute attribute in circuitAttributes)
                {
                    MapType itemAttribute = new MapType();
                    itemAttribute.Content = attribute.Description;
                    itemAttribute.Tag = attribute;
                    itemAttribute.Background = new SolidBrush(Color.LightGray);
                    MapTypes.Add(itemAttribute);
                    if (attribute.AttributeGroupList != null && attribute.AttributeGroupList.Count > 0) AttributeWithGroups = attribute;
                }
                List<AttributeGroup> circuitGroups = GetCircuitAttributeGroups(AttributeWithGroups);
                if (circuitGroups != null)
                {
                    foreach (AttributeGroup group in circuitGroups)
                    {
                        MapType itemGroup = new MapType();
                        itemGroup.Content = group.Description;
                        itemGroup.Background = new SolidBrush(Color.LightBlue);
                        itemGroup.Tag = group;
                        MapTypes.Add(itemGroup);
                    }
                }
            }

            MapType selectedItem = (from m in MapTypes where m.Content.ToLower() == type.ToLower() select m).FirstOrDefault();

            attribute = null;
            attributeGroup = null;
            if (selectedItem != null && selectedItem.Tag != null)
            {
                object tag = selectedItem.Tag;
                if (tag is BusinessObjects.Attribute) attribute = (BusinessObjects.Attribute)tag;
                if (tag is AttributeGroup) attributeGroup = (AttributeGroup)tag;
            }
        }

        private bool RectangleContainsPoles(List<Location> locationList)
        {
            foreach (Pole pole in circuit.Poles)
            {
                if (GISHelper.PointInPolygon(locationList, new Location(pole.Latitude, pole.Longitude))) return true;
            }
            return false;
        }

        private void ShowLayer(string layer, bool show)
        {
            IEnumerable<GMapOverlay> layers = mapControl.Overlays.Where(x => x.Id == layer);
            if (layers != null && layers.Any()) layers.First().IsVisibile = show;
        }

        private void ZoomToBox(LocationRectangle rectangle)
        {
            try
            {
                mapControl.SetZoomToFitRect(new RectLatLng(rectangle.Northwest.Latitude, rectangle.Northwest.Longitude, rectangle.Width, rectangle.Height));
            }
            catch
            {

            }
        }

    }
}
