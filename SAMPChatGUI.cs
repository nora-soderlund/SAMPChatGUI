using System;
using System.Diagnostics;
using SharpDX;
using SharpDX.IO;
using SharpDX.Direct3D9;
using SharpDX.WIC;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Collections.Generic;

class SAMPChatGUI {
    private readonly Device device;
    private readonly Sprite sprite;
    private readonly Surface renderTarget;
    private readonly Direct3DEx direct3D;

    public SAMPChatGUI() {
        direct3D = new Direct3DEx();

        var presentParams = new PresentParameters {
            BackBufferWidth = 800,
            BackBufferHeight = 600,
            DeviceWindowHandle = IntPtr.Zero,
            Windowed = true,
            SwapEffect = SwapEffect.Discard
        };

        device = new DeviceEx(direct3D, 0, DeviceType.Hardware, IntPtr.Zero, CreateFlags.SoftwareVertexProcessing, presentParams);

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

    private void RenderLines(SharpDX.Direct3D9.Font font, Line[] lines, RawRectangle rectangle, FontDrawFlags flags) {
        if(flags.HasFlag(FontDrawFlags.Bottom)) {
            for(var index = lines.Length - 1; index != -1; index--) {
                if (lines[index].Message.Length == 0) {
                    continue;
                }
            }
        }

        foreach (var line in lines) {
            if(line.Message.Length == 0) {
                line.Message = " ";
            }

            if (line.Message.Length >= 8 && line.Message[0] == '{' && line.Message[7] == '}') {
                line.Color = line.Message.Substring(1, 6);
                line.Message = line.Message.Substring(8);
            }

            Regex regex = new Regex(@"(.*)(\{[A-Fa-f0-9]{6}\})(.*)");
            Match match = regex.Match(Regex.Replace(line.Message, @"\s*\{([A-Fa-f0-9]{6})\}\s*", @"{${1}} "));

            var storedLeft = rectangle.Left;

            if (match.Success) {
                for (var index = 1; index < match.Groups.Count; index++) {
                    if (match.Groups[index].Value.Length == 8 && match.Groups[index].Value[0] == '{' && match.Groups[index].Value[7] == '}') {
                        string value = match.Groups[index].Value.Substring(1, 6);

#if DEBUG
                        Console.WriteLine("Registering color " + value);
#endif

                        line.Color = value;

                        continue;
                    }

#if DEBUG
                    Console.WriteLine("Adding " + match.Groups[index].Value + " (" + match.Groups[index].Value.Length + ")");
#endif

                    DrawText(font, line.Color, match.Groups[index].Value, rectangle, flags);
                    rectangle.Left = MeasureText(font, match.Groups[index].Value, rectangle, flags).Right;
                }
            }
            else {
#if DEBUG
                Console.WriteLine("Adding " + line.Message + " (" + line.Message.Length + ")");
#endif

                DrawText(font, line.Color, line.Message, rectangle, flags);
            }

            rectangle.Left = storedLeft;

            if (!flags.HasFlag(FontDrawFlags.Bottom)) {
                rectangle.Top = MeasureText(font, line.Message, rectangle, flags).Bottom;
            }
            else {
                rectangle.Bottom = MeasureText(font, line.Message, rectangle, flags).Top;
            }
        }
    }


    public void Render(Data data) {
        var font = new SharpDX.Direct3D9.Font(device, data.FontSize, 0, FontWeight.Bold, 1, false, FontCharacterSet.Default, FontPrecision.Default, FontQuality.Default, FontPitchAndFamily.Default, "Arial");

        device.BeginScene();

        device.Clear(ClearFlags.Target, new RawColorBGRA(0, 0, 0, 0), 1.0f, 0);

        sprite.Begin(SpriteFlags.AlphaBlend);

        //var rect = new RawRectangle(30, 10, 550, 110);

        if (data.Top.Length != 0) {
            RenderLines(font, data.Top, new RawRectangle(30, 10, 800, 600), FontDrawFlags.NoClip & FontDrawFlags.Left);
        }
        
        if(data.Bottom.Length != 0) {
            var list = new List<Line>(data.Bottom);

            list.Reverse();

            RenderLines(font, list.ToArray(), new RawRectangle(30, 10, 800, 590), FontDrawFlags.Bottom);
        }

        sprite.End();

        device.EndScene();

        font.Dispose();
    }

    private RawRectangle MeasureText(SharpDX.Direct3D9.Font font, string message, RawRectangle rectangle, FontDrawFlags flags) {
        return font.MeasureText(sprite, message, rectangle, flags);
    }

    private void DrawText(SharpDX.Direct3D9.Font font, string hex, string message, RawRectangle rectangle, FontDrawFlags flags) {
        Color color = ColorTranslator.FromHtml('#' + hex);
        byte red = Convert.ToByte(color.R);
        byte green = Convert.ToByte(color.G);
        byte blue = Convert.ToByte(color.B);

        font.DrawText(sprite, message, CloneRectangle(rectangle, -1, 0), flags, new RawColorBGRA(0, 0, 0, 255));
        font.DrawText(sprite, message, CloneRectangle(rectangle, 1, 0), flags, new RawColorBGRA(0, 0, 0, 255));
        font.DrawText(sprite, message, rectangle, flags, new RawColorBGRA(0, 0, 0, 255));
        font.DrawText(sprite, message, CloneRectangle(rectangle, 0, -1), flags, new RawColorBGRA(0, 0, 0, 255));
        font.DrawText(sprite, message, CloneRectangle(rectangle, 0, 1), flags, new RawColorBGRA(0, 0, 0, 255));

        font.DrawText(sprite, message, rectangle, flags, new RawColorBGRA(blue, green, red, 255));
    }

    public void Dispose() {
        sprite.Dispose();
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
