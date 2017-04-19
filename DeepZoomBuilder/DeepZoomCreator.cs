using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Media.Imaging;
using Path = System.IO.Path;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace DeepZoomBuilder
{
    public enum ImageType
    {
        Png,
        Jpeg
    }

    public class DeepZoomCreator
    {
        /// <summary>
        /// Default public constructor
        /// </summary>
        public DeepZoomCreator() { }
 
        /// <summary>
        /// Create a deep zoom image from a single source image
        /// </summary>
        /// <param name = "sourceImage" > Source image path</param>
        /// <param name = "destinationImage" > Destination path (must be .dzi or .xml)</param>
        public void CreateSingleComposition(string sourceImage, string destinationImage, ImageType type)
        {
            imageType = type;
            string source = sourceImage;
            string destDirectory = Path.GetDirectoryName(destinationImage);
            string leafname = Path.GetFileNameWithoutExtension(destinationImage);
            string root = Path.Combine(destDirectory, leafname); ;
            string filesdir = root + "_files";

            Directory.CreateDirectory(filesdir);
            BitmapImage img = new BitmapImage(new Uri(source));
            double dWidth = img.PixelWidth;
            double dHeight = img.PixelHeight;
            double AspectRatio = dWidth / dHeight;

            // The Maximum level for the pyramid of images is
            // Log2(maxdimension)

            double maxdimension = Math.Max(dWidth, dHeight);
            double logvalue = Math.Log(maxdimension, 2);
            int MaxLevel = (int)Math.Ceiling(logvalue);
            string topleveldir = Path.Combine(filesdir, MaxLevel.ToString());

            // Create the directory for the top level tiles
            Directory.CreateDirectory(topleveldir);

            // Calculate how many tiles across and down
            int maxcols = img.PixelWidth / 256;
            int maxrows = img.PixelHeight / 256;

            // Get the bounding rectangle of the source image, for clipping
            Rect MainRect = new Rect(0, 0, img.PixelWidth, img.PixelHeight);
            for (int j = 0; j <= maxrows; j++)
            {
                for (int i = 0; i <= maxcols; i++)
                {
                    // Calculate the bounds of the tile
                    // including a 1 pixel overlap each side
                    Rect smallrect = new Rect((double)(i * 256) - 1, (double)(j * 256) - 1, 258.0, 258.0);

                    // Adjust for the rectangles at the edges by intersecting
                    smallrect.Intersect(MainRect);

                    // We want a RenderTargetBitmap to render this tile into
                    // Create one with the dimensions of this tile
                    RenderTargetBitmap outbmp = new RenderTargetBitmap((int)smallrect.Width, (int)smallrect.Height, 96, 96, PixelFormats.Pbgra32);
                    DrawingVisual visual = new DrawingVisual();
                    DrawingContext context = visual.RenderOpen();

                    // Set the offset of the source image into the destination bitmap
                    // and render it
                    Rect rect = new Rect(-smallrect.Left, -smallrect.Top, img.PixelWidth, img.PixelHeight);
                    context.DrawImage(img, rect);
                    context.Close();
                    outbmp.Render(visual);

                    // Save the bitmap tile
                    string destination = Path.Combine(topleveldir, string.Format("{0}_{1}", i, j));
                    EncodeBitmap(outbmp, destination);

                    // null out everything we've used so the Garbage Collector
                    // knows they're free. This could easily be voodoo since they'll go
                    // out of scope, but it can't hurt.
                    outbmp = null;
                    context = null;
                    visual = null;
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // clear the source image since we don't need it anymore
            img = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Now render the lower levels by rendering the tiles from the level
            // above to the next level down
            for (int level = MaxLevel - 1; level >= 0; level--)
            {
                RenderSubtiles(filesdir, dWidth, dHeight, MaxLevel, level);
            }

            // Now generate the .dzi file

            string format = "png";
            if (imageType == ImageType.Jpeg)
            {
                format = "jpg";
            }

            XElement dzi = new XElement("Image",
                new XAttribute("TileSize", 256),
                new XAttribute("Overlap", 1),
                new XAttribute("Format", format), // xmlns="http://schemas.microsoft.com/deepzoom/2008">
                new XElement("Size",
                    new XAttribute("Width", dWidth),
                    new XAttribute("Height", dHeight)),
                new XElement("DisplayRects",
                    new XElement("DisplayRect",
                        new XAttribute("MinLevel", 1),
                        new XAttribute("MaxLevel", MaxLevel),
                        new XElement("Rect",
                            new XAttribute("X", 0),
                            new XAttribute("Y", 0),
                            new XAttribute("Width", dWidth),
                            new XAttribute("Height", dHeight)))));
            dzi.Save(destinationImage);

        }
 
        /// <summary>
        /// Save the output bitmap as either Png or Jpeg
        /// </summary>
        /// <param name = "outbmp" > Bitmap to save</param>
        /// <param name = "destination" > Path to save to, without the file extension</param>
        private void EncodeBitmap(RenderTargetBitmap outbmp, string destination)
        {
            if (imageType == ImageType.Png)
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(outbmp));
                FileStream fs = new FileStream(destination + ".png", FileMode.Create);
                encoder.Save(fs);
                fs.Close();
            }
            else
            {
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.QualityLevel = 95;
                encoder.Frames.Add(BitmapFrame.Create(outbmp));
                FileStream fs = new FileStream(destination + ".jpg", FileMode.Create);
                encoder.Save(fs);
                fs.Close();
            }
        }

        /// <summary>
        /// Specifies the output filetype
        /// </summary>
        ImageType imageType = ImageType.Jpeg;
 
        /// <summary>
        /// Render the subtiles given a fully rendered top-level
        /// </summary>
        /// <param name = "subfiles" > Path to the xxx_files directory</param>
        /// <param name = "imageWidth" > Width of the source image</param>
        /// <param name = "imageHeight" > Height of the source image</param>
        /// <param name = "maxlevel" > Top level of the tileset</param>
        /// <param name = "desiredlevel" > Level we want to render. Note it requires
        /// that the level above this has already been rendered.</param>
        private void RenderSubtiles(string subfiles, double imageWidth, double imageHeight, int maxlevel, int desiredlevel)
        {
            string formatextension = ".png";
            if (imageType == ImageType.Jpeg)
            {
                formatextension = ".jpg";
            }
            int uponelevel = desiredlevel + 1;
            double desiredfactor = Math.Pow(2, maxlevel - desiredlevel);
            double higherfactor = Math.Pow(2, maxlevel - (desiredlevel + 1));
            string renderlevel = Path.Combine(subfiles, desiredlevel.ToString());
            Directory.CreateDirectory(renderlevel);
            string upperlevel = Path.Combine(subfiles, (desiredlevel + 1).ToString());

            // Calculate the tiles we want to translate down
            Rect MainBounds = new Rect(0, 0, imageWidth, imageHeight);
            Rect OriginalRect = new Rect(0, 0, imageWidth, imageHeight);

            // Scale down this rectangle to the scale factor of the level we want
            MainBounds.X = Math.Ceiling(MainBounds.X / desiredfactor);
            MainBounds.Y = Math.Ceiling(MainBounds.Y / desiredfactor);
            MainBounds.Width = Math.Ceiling(MainBounds.Width / desiredfactor);
            MainBounds.Height = Math.Ceiling(MainBounds.Height / desiredfactor);

            int lowx = (int)Math.Floor(MainBounds.X / 256);
            int lowy = (int)Math.Floor(MainBounds.Y / 256);
            int highx = (int)Math.Floor(MainBounds.Right / 256);
            int highy = (int)Math.Floor(MainBounds.Bottom / 256);

            for (int x = lowx; x <= highx; x++)
            {
                for (int y = lowy; y <= highy; y++)
                {
                    Rect smallrect = new Rect((double)(x * 256) - 1, (double)(y * 256) - 1, 258.0, 258.0);
                    smallrect.Intersect(MainBounds);
                    RenderTargetBitmap outbmp = new RenderTargetBitmap((int)smallrect.Width, (int)smallrect.Height, 96, 96, PixelFormats.Pbgra32);
                    DrawingVisual visual = new DrawingVisual();
                    DrawingContext context = visual.RenderOpen();

                    // Calculate the bounds of this tile

                    Rect rect = smallrect;
                    // This is the rect of this tile. Now render any appropriate tiles onto it
                    // The upper level tiles are twice as big, so they have to be shrunk down

                    Rect scaledRect = new Rect(rect.X * 2, rect.Y * 2, rect.Width * 2, rect.Height * 2);
                    for (int tx = lowx * 2; tx <= highx * 2 + 1; tx++)
                    {
                        for (int ty = lowy * 2; ty <= highy * 2 + 1; ty++)
                        {
                            // See if this tile overlaps
                            Rect subrect = GetTileRectangle(tx, ty);
                            if (scaledRect.IntersectsWith(subrect))
                            {
                                subrect.X -= scaledRect.X;
                                subrect.Y -= scaledRect.Y;
                                RenderTile(context, Path.Combine(upperlevel, tx.ToString() + "_" + ty.ToString() + formatextension), subrect);
                            }
                        }
                    }
                    context.Close();
                    outbmp.Render(visual);

                    // Render the completed tile and clear all resources used
                    string destination = Path.Combine(renderlevel, string.Format(@"{0}_{1}", x, y));
                    EncodeBitmap(outbmp, destination);
                    outbmp = null;
                    visual = null;
                    context = null;
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

        }
 
        /// <summary>
        /// Get the bounds of the given tile rectangle
        /// </summary>
        /// <param name = "x" > x index of the tile</param>
        /// <param name = "y" > y index of the tile</param>
        /// <returns>Bounding rectangle for the tile at the given indices</returns>
        private static Rect GetTileRectangle(int x, int y)
        {
            Rect rect = new Rect(256 * x - 1, 256 * y - 1, 258, 258);
            if (x == 0)
            {
                rect.X = 0;
                rect.Width = rect.Width - 1;
            }
            if (y == 0)
            {
                rect.Y = 0;
                rect.Width = rect.Width - 1;
            }

            return rect;
        }
 
        /// <summary>
        /// Render the given tile rectangle, shrunk down by half to fit the next
        /// lower level
        /// </summary>
        /// <param name = "context" > DrawingContext for the DrawingVisual to render into</param>
        /// <param name = "path" > path to the tile we're rendering</param>
        /// <param name = "rect" > Rectangle to render this tile.</param>
        private void RenderTile(DrawingContext context, string path, Rect rect)
        {
            if (File.Exists(path))
            {
                BitmapImage img = new BitmapImage(new Uri(path));
                rect = new Rect(rect.X / 2.0, rect.Y / 2.0, ((double)img.PixelWidth) / 2.0, ((double)img.PixelHeight) / 2.0);
                context.DrawImage(img, rect);
            }
        }

    }
}