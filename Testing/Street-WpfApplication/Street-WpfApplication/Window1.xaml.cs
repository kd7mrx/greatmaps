﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.IO;
using System.ComponentModel;
using System.Windows.Media.Media3D;
using System.Diagnostics;

namespace Street_WpfApplication
{
   /// <summary>
   /// Interaction logic for Window1.xaml
   /// </summary>
   public partial class Window1 : Window
   {
      BackgroundWorker loader = new BackgroundWorker();
      Model3DGroup _model3dGroup = new Model3DGroup();

      public Window1()
      {
         InitializeComponent();

         // removes white lines between tiles!
         //SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

         loader.DoWork += new DoWorkEventHandler(loader_DoWork);
         loader.ProgressChanged += new ProgressChangedEventHandler(loader_ProgressChanged);
         loader.WorkerReportsProgress = true;

         ModelVisual3D model3d = new ModelVisual3D();
         {
            model3d.Content = _model3dGroup;

            AmbientLight light = new AmbientLight(Colors.White);
            _model3dGroup.Children.Add(light);

            view.Children.Add(model3d);
         }
      }

      private void AddImage(ImageSource img, double zOffset, double xOffset, double yOffset)
      {
         try
         {
            MeshGeometry3D mesh3d = new MeshGeometry3D();
            mesh3d.Positions.Add(new Point3D(-0.5, 0.5, 0));
            mesh3d.Positions.Add(new Point3D(0.5, 0.5, 0));
            mesh3d.Positions.Add(new Point3D(-0.5, -0.5, 0));
            mesh3d.Positions.Add(new Point3D(0.5, -0.5, 0));

            mesh3d.TriangleIndices.Add(0);
            mesh3d.TriangleIndices.Add(2);
            mesh3d.TriangleIndices.Add(1);
            mesh3d.TriangleIndices.Add(1);
            mesh3d.TriangleIndices.Add(2);
            mesh3d.TriangleIndices.Add(3);

            mesh3d.TextureCoordinates.Add(new Point(0, 0));
            mesh3d.TextureCoordinates.Add(new Point(1, 0));
            mesh3d.TextureCoordinates.Add(new Point(0, 1));
            mesh3d.TextureCoordinates.Add(new Point(1, 1));

            DiffuseMaterial side5Material = new DiffuseMaterial(new SolidColorBrush(Colors.DarkGray));
            DiffuseMaterial imgBrush = new DiffuseMaterial(new ImageBrush(img));

            GeometryModel3D gm3d = new GeometryModel3D();
            gm3d.Geometry = mesh3d;
            gm3d.Material = imgBrush;

            Transform3DGroup transformGroup = new Transform3DGroup();
            {
               // gotta set where we want to rotate around. We don't want to rotate around the world axis, which would be the default ...
               RotateTransform3D rotateTransform = new RotateTransform3D();
               rotateTransform.CenterX = xOffset;
               rotateTransform.CenterZ = zOffset;
               rotateTransform.CenterY = yOffset;

               // we'll need a default axis angle so that we can animate it later ...
               rotateTransform.Rotation = new AxisAngleRotation3D(new Vector3D(0, 0, 0), 0);

               // move the object into the proper location in 3d space ...
               transformGroup.Children.Add(new TranslateTransform3D(xOffset, yOffset, zOffset));

               // add the rotation 
               transformGroup.Children.Add(rotateTransform);
            }

            // throw them together and put them into the group
            gm3d.Transform = transformGroup;

            _model3dGroup.Children.Add(gm3d);
         }
         catch(Exception ex)
         {
            Debug.WriteLine(ex);
         }
      }

      void loader_ProgressChanged(object sender, ProgressChangedEventArgs e)
      {
         if(e.ProgressPercentage == 0)
         {
            Pass p = e.UserState as Pass;

            Image i = new Image();
            i.Source = p.src;
            i.Stretch = Stretch.UniformToFill;

            // add to viewport
            {
               AddImage(p.src, -10, p.X-6, -p.Y+2);
            }
         }
      }

      void loader_DoWork(object sender, DoWorkEventArgs e)
      {
         string panoId = "4fe6hEN9GJC6thoQBcgv0Q";
         int zoom = 4;

         //0, 1
         //1, 2   
         //2, 4
         //3, 7   
         //4, 13  
         //5, 26  

         for(int y = 0; y <= zoom+1; y++)
         {
            for(int x = 0; x < 13; x++)
            {
               Pass p = new Pass();                
               p.Y = y;
               p.X = x;

               string fl = "Tiles\\" + zoom + "\\img_" + x + "_" + y + ".jpg";
               string dr = System.IO.Path.GetDirectoryName(fl);
               if(!Directory.Exists(dr))
               {
                  Directory.CreateDirectory(dr);
               }
               if(!File.Exists(fl))
               {
                  ImageSource src = Get(string.Format("http://cbk{0}.google.com/cbk?output=tile&panoid={1}&zoom={2}&x={3}&y={4}&cb_client=maps_sv", (x + 2 * y) % 3, panoId, zoom, x, y));
                  p.src = src;
                  SaveImg(src, fl);
               }
               else
               {
                  using(Stream s = File.OpenRead(fl))
                  {
                     p.src = FromStream(s);
                  }
               }

               loader.ReportProgress(0, p);
            }
         }
         GC.Collect();
         GC.WaitForPendingFinalizers();
      }

      void SaveImg(ImageSource src, string file)
      {
         using(Stream s = File.OpenWrite(file))
         {
            JpegBitmapEncoder e = new JpegBitmapEncoder();
            e.Frames.Add(BitmapFrame.Create(src as BitmapSource));
            e.Save(s);
         }
      }

      private void Window_Loaded(object sender, RoutedEventArgs e)
      {
         loader.RunWorkerAsync();
      }

      public Stream CopyStream(Stream inputStream)
      {
         const int readSize = 256;
         byte[] buffer = new byte[readSize];
         MemoryStream ms = new MemoryStream();

         using(inputStream)
         {
            int count = inputStream.Read(buffer, 0, readSize);
            while(count > 0)
            {
               ms.Write(buffer, 0, count);
               count = inputStream.Read(buffer, 0, readSize);
            }
         }
         buffer = null;
         ms.Seek(0, SeekOrigin.Begin);
         return ms;
      }

      ImageSource FromStream(Stream stream)
      {
         ImageSource ret = null;
         if(stream != null)
         {
            {
               // try png decoder
               try
               {
                  JpegBitmapDecoder bitmapDecoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                  ImageSource m = bitmapDecoder.Frames[0];

                  if(m != null)
                  {
                     ret = m;
                  }
               }
               catch
               {
                  ret = null;
               }

               // try jpeg decoder
               if(ret == null)
               {
                  try
                  {
                     stream.Seek(0, SeekOrigin.Begin);

                     PngBitmapDecoder bitmapDecoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                     ImageSource m = bitmapDecoder.Frames[0];

                     if(m != null)
                     {
                        ret = m;
                     }
                  }
                  catch
                  {
                     ret = null;
                  }
               }
            }
         }
         return ret;
      }

      ImageSource Get(string url)
      {
         ImageSource ret = null;
         try
         {
            {
               HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
               request.ServicePoint.ConnectionLimit = 50;
               request.Proxy = WebRequest.DefaultWebProxy;

               request.UserAgent = "Opera/9.62 (Windows NT 5.1; U; en) Presto/2.1.1";
               request.Timeout = 10*1000;
               request.ReadWriteTimeout = request.Timeout*6;

               //request.Accept = "text/html, application/xml;q=0.9, application/xhtml+xml, image/png, image/jpeg, image/gif, image/x-xbitmap, */*;q=0.1";
               //request.Headers["Accept-Encoding"] = "deflate, gzip, x-gzip, identity, *;q=0";
               request.Referer = "http://maps.google.com/";
               request.KeepAlive = true;

               using(HttpWebResponse response = request.GetResponse() as HttpWebResponse)
               {
                  using(Stream responseStream = CopyStream(response.GetResponseStream()))
                  {
                     ret = FromStream(responseStream);
                  }
               }
            }
         }
         catch(Exception)
         {
            ret = null;
         }
         return ret;
      }

      Point lastPos;
      private void Window_MouseMove(object sender, MouseEventArgs e)
      {
         if(e.LeftButton == MouseButtonState.Pressed)
         {
            Point p = Mouse.GetPosition(view);

            if(lastPos.X != 0 && lastPos.Y != 0)
            {
               Vector3D look = cam.LookDirection;
               look.X -= (lastPos.X - p.X)/500.0;
               look.Y += (lastPos.Y - p.Y)/500.0;
               cam.LookDirection = look;
            }

            lastPos = p;
         }
      }

      private void Window_MouseUp(object sender, MouseButtonEventArgs e)
      {
         lastPos = new Point();
      }
   }

   class Pass
   {
      public ImageSource src;
      public int Y;
      public int X;
   }
}