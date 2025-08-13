using GMap.NET;
using GMap.NET.WindowsForms.Markers;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Serialization;

namespace GMapLibrary.MapObjects
{
    public class GMapMarkerWithLabel : GMarkerGoogle, ISerializable
    {
        public Font font;
        public double angle = 0.0;
        public string caption;
        public int TextX = 0;
        public int TextY = 0;
        public int FontSize = 8;
        public bool SatelliteBackground = false;

        public GMapMarkerWithLabel(PointLatLng p, string caption, GMarkerGoogleType type, double angle = 0.0, int textX = 0, int textY = 0, int fontSize = 8, bool satelliteBackground = false) : base(p, type)
        {
            this.caption = caption;
            this.angle = angle;
            TextX = textX;
            TextY = textY;
            FontSize = fontSize;
            SatelliteBackground = satelliteBackground;
            font = new Font("Arial", FontSize);
        }

        public GMapMarkerWithLabel(PointLatLng p, string caption, string path, int AnchorOffsetX, int AnchorOffsetY, double angle = 0.0, int textX = 0, int textY = 0, int fontSize = 8, bool lightColorText = false, bool bold = false) : base(p, new Bitmap(path))
        {
            this.Offset = new Point(this.Offset.X + AnchorOffsetX, this.Offset.Y + AnchorOffsetY);
            this.caption = caption;
            this.angle = angle;
            TextX = textX;
            TextY = textY;
            FontSize = fontSize;
            SatelliteBackground = lightColorText;
            if (bold)
            {
                font = new Font("Arial", FontSize, FontStyle.Bold);
            }
            else
            {
                font = new Font("Arial", FontSize);
            }
        }

        public GMapMarkerWithLabel(PointLatLng p, string caption, Bitmap bitmap, int AnchorOffsetX, int AnchorOffsetY, double angle = 0.0, int textX = 0, int textY = 0, int fontSize = 8, bool lightColorText = false, bool bold = false) : base(p, bitmap)
        {
            this.Offset = new Point(this.Offset.X + AnchorOffsetX, this.Offset.Y + AnchorOffsetY);
            this.caption = caption;
            this.angle = angle;
            TextX = textX;
            TextY = textY;
            FontSize = fontSize;
            SatelliteBackground = lightColorText;
            if (bold)
            {
                font = new Font("Arial", FontSize, FontStyle.Bold);
            }
            else
            {
                font = new Font("Arial", FontSize);
            }
        }

        public override void OnRender(Graphics g)
        {
            base.OnRender(g);
            if (SatelliteBackground)
            {
                DrawRotatedTextAt(g, angle, caption, this.ToolTipPosition.X, this.ToolTipPosition.Y, font, Brushes.White);
            }
            else
            {
                DrawRotatedTextAt(g, angle, caption, this.ToolTipPosition.X, this.ToolTipPosition.Y, font, Brushes.Black);
            }
        }

        private void DrawRotatedTextAt(Graphics gr, double angle, string txt, int x, int y, Font the_font, Brush the_brush)
        {
            // Save the graphics state.
            GraphicsState state = gr.Save();

            // Rotate.
            gr.RotateTransform((float)angle);

            // Translate to desired position. Be sure to append
            // the rotation so it occurs after the rotation.
            gr.TranslateTransform(x, y, MatrixOrder.Append);

            // Draw the text at the origin.
            gr.DrawString(txt, the_font, the_brush, TextX, TextY);

            // Restore the graphics state.
            gr.Restore(state);
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        #region ISerializable Members

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        protected GMapMarkerWithLabel(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        #endregion

    }
}
