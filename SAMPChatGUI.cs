using System;
using System.Diagnostics;
using SharpDX;
using SharpDX.IO;
using SharpDX.Direct3D9;
using SharpDX.WIC;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Text.RegularExpressions;

class SAMPChatGUI {
    private readonly Device device;
    private readonly SharpDX.Direct3D9.Font font;
    private readonly Sprite sprite;
    private readonly Surface renderTarget;
    private readonly Direct3D direct3D;

    public SAMPChatGUI() {
        Console.WriteLine("Starting a Direct3D instance");

        direct3D = new Direct3D();

        var presentParams = new PresentParameters {
            BackBufferWidth = 800,
            BackBufferHeight = 600,
            DeviceWindowHandle = IntPtr.Zero,
            Windowed = true,
            SwapEffect = SwapEffect.Discard
        };


        device = new Device(direct3D, 0, DeviceType.Hardware, IntPtr.Zero, CreateFlags.SoftwareVertexProcessing, presentParams);

        var fontSize = 18; // default 14 for small screens, 16 for medium and 18 for large

        font = new SharpDX.Direct3D9.Font(device, fontSize, 0, FontWeight.Bold, 1, false, FontCharacterSet.Default, FontPrecision.Default, FontQuality.Default, FontPitchAndFamily.Default, "Arial");

        renderTarget = Surface.CreateRenderTarget(device, 800, 600, Format.A8R8G8B8, MultisampleType.None, 0, true);

        device.SetRenderTarget(0, renderTarget);

        sprite = new Sprite(device);
    }

    private RawRectangle CloneRectangle(RawRectangle rectangle, int left, int top) {
        var newRectangle = new RawRectangle(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

        newRectangle.Left += left;
        newRectangle.Top += top;

        return newRectangle;
    }

    public void Render(Data data) {
        device.BeginScene();

        device.Clear(ClearFlags.Target, new RawColorBGRA(0, 0, 0, 0), 1.0f, 0);

        sprite.Begin(SpriteFlags.AlphaBlend);

        //var rect = new RawRectangle(30, 10, 550, 110);
        var rect = new RawRectangle(30, 10, 800, 600);

        foreach (var line in data.Lines) {
            if(line.Message[0] == '{' && line.Message[7] == '}') {
                line.Color = line.Message.Substring(1, 6);
                line.Message = line.Message.Substring(8);
            }

            Regex regex = new Regex(@"(.*)\{([A-Fa-f0-9]{6})\}(.*)");
            Match match = regex.Match(Regex.Replace(line.Message, @"\s*\{([A-Fa-f0-9]{6})\}\s*", @"{${1}} "));
            
            var storedLeft = rect.Left;

            if (match.Success) {
                for(var index = 1; index < match.Groups.Count; index++) {
                    if(match.Groups[index].Value.Length == 6) {
                        Console.WriteLine("Registering color " + match.Groups[index].Value);

                        line.Color = match.Groups[index].Value;

                        continue;
                    }

                    Console.WriteLine("Adding " + match.Groups[index].Value + " (" + match.Groups[index].Value.Length + ")");

                    DrawText(line.Color, match.Groups[index].Value, rect);
                    rect.Left = MeasureText(match.Groups[index].Value, rect).Right;
                }
            }
            else {
                DrawText(line.Color, line.Message, rect);
            }

            rect.Left = storedLeft;

            rect.Top = MeasureText(line.Message, rect).Bottom;
        }

        sprite.End();

        device.EndScene();
    }

    private RawRectangle MeasureText(string message, RawRectangle rectangle) {
        return font.MeasureText(sprite, message, rectangle, FontDrawFlags.NoClip & FontDrawFlags.Left);
    }

    private void DrawText(string hex, string message, RawRectangle rectangle) {
        Color color = ColorTranslator.FromHtml('#' + hex);
        byte red = Convert.ToByte(color.R);
        byte green = Convert.ToByte(color.G);
        byte blue = Convert.ToByte(color.B);

        font.DrawText(sprite, message, CloneRectangle(rectangle, -1, 0), FontDrawFlags.NoClip & FontDrawFlags.Left, new RawColorBGRA(0, 0, 0, 255));
        font.DrawText(sprite, message, CloneRectangle(rectangle, 1, 0), FontDrawFlags.NoClip & FontDrawFlags.Left, new RawColorBGRA(0, 0, 0, 255));
        font.DrawText(sprite, message, rectangle, FontDrawFlags.NoClip & FontDrawFlags.Left, new RawColorBGRA(0, 0, 0, 255));
        font.DrawText(sprite, message, CloneRectangle(rectangle, 0, -1), FontDrawFlags.NoClip & FontDrawFlags.Left, new RawColorBGRA(0, 0, 0, 255));
        font.DrawText(sprite, message, CloneRectangle(rectangle, 0, 1), FontDrawFlags.NoClip & FontDrawFlags.Left, new RawColorBGRA(0, 0, 0, 255));

        font.DrawText(sprite, message, rectangle, FontDrawFlags.NoClip & FontDrawFlags.Left, new RawColorBGRA(blue, green, red, 255));
    }

    public void Dispose() {
        sprite.Dispose();
        font.Dispose();
        renderTarget.Dispose();
        device.Dispose();
        direct3D.Dispose();
    }

    void SaveSurfaceToPng(string filePath) {
        using (Surface surface = device.GetRenderTarget(0)) {
            var device = surface.Device;
            var description = surface.Description;

            // Copy the render target data to a system memory surface (SystemMemory pool is required for reading data)
            using (var sysMemSurface = Surface.CreateOffscreenPlain(device, description.Width, description.Height, description.Format, Pool.SystemMemory)) {
                device.GetRenderTargetData(surface, sysMemSurface);

                // Lock the surface to access the pixel data
                DataRectangle dataRectangle = sysMemSurface.LockRectangle(LockFlags.ReadOnly);

                // Create a WIC factory
                ImagingFactory imagingFactory = new ImagingFactory();

                // Create a stream to write the PNG file
                using (var stream = new WICStream(imagingFactory, filePath, NativeFileAccess.Write)) {
                    // Create a PNG encoder
                    var encoder = new PngBitmapEncoder(imagingFactory, stream);

                    // Create a new frame to encode
                    using (var frameEncoder = new BitmapFrameEncode(encoder)) {
                        frameEncoder.Initialize();
                        frameEncoder.SetSize(description.Width, description.Height);

                        // Set the pixel format, which must match the surface format (32bpp BGRA)
                        var pixelFormat = PixelFormat.Format32bppBGRA;
                        frameEncoder.SetPixelFormat(ref pixelFormat);

                        // Write pixels from the locked data rectangle
                        frameEncoder.WritePixels(description.Height, dataRectangle);


                        frameEncoder.Commit();
                        encoder.Commit();
                    }

                    encoder.Dispose();
                }

                // Unlock the surface
                sysMemSurface.UnlockRectangle();
            }
        }
    }

    public byte[] SaveSurfaceToPngInMemory() {
        using (Surface surface = device.GetRenderTarget(0)) {
            var device = surface.Device;
            var description = surface.Description;

            // Copy the render target data to a system memory surface (SystemMemory pool is required for reading data)
            using (var sysMemSurface = Surface.CreateOffscreenPlain(device, description.Width, description.Height, description.Format, Pool.SystemMemory)) {
                device.GetRenderTargetData(surface, sysMemSurface);

                // Lock the surface to access the pixel data
                DataRectangle dataRectangle = sysMemSurface.LockRectangle(LockFlags.ReadOnly);

                // Create a WIC factory
                ImagingFactory imagingFactory = new ImagingFactory();

                // Use a MemoryStream to store the PNG in memory
                using (var memoryStream = new System.IO.MemoryStream()) {
                    // Create a WIC stream using the MemoryStream
                    using (var wicStream = new WICStream(imagingFactory, memoryStream)) {
                        // Create a PNG encoder
                        var encoder = new PngBitmapEncoder(imagingFactory, wicStream);

                        // Create a new frame to encode
                        using (var frameEncoder = new BitmapFrameEncode(encoder)) {
                            frameEncoder.Initialize();
                            frameEncoder.SetSize(description.Width, description.Height);

                            // Set the pixel format, which must match the surface format (32bpp BGRA)
                            var pixelFormat = PixelFormat.Format32bppBGRA;
                            frameEncoder.SetPixelFormat(ref pixelFormat);

                            // Write pixels from the locked data rectangle
                            frameEncoder.WritePixels(description.Height, dataRectangle);

                            frameEncoder.Commit();
                            encoder.Commit();
                        }

                        // Flush the stream
                        wicStream.Dispose();
                    }

                    // Unlock the surface
                    sysMemSurface.UnlockRectangle();

                    // Return the PNG as a byte array
                    return memoryStream.ToArray();
                }
            }
        }
    }

}
