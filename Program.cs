using System;
using System.IO;
using System.Net.Sockets;
using System.Drawing;
using System.Drawing.Imaging;

namespace DistributedClient
{
    class Program
    {
        static void Main(string[] args)
        {
            string serverIp = "127.0.0.1";
            int port = 2040;

            try
            {
                using TcpClient client = new TcpClient(serverIp, port);
                Console.WriteLine($"🔗 Połączono z serwerem {serverIp}:{port}");

                using NetworkStream stream = client.GetStream();

                // === Odbierz dane ===
                byte[] lengthBytes = new byte[4];
                stream.Read(lengthBytes, 0, 4);
                int length = BitConverter.ToInt32(lengthBytes.Reverse().ToArray(), 0);

                byte[] data = new byte[length];
                int bytesRead = 0;
                while (bytesRead < length)
                {
                    int r = stream.Read(data, bytesRead, length - bytesRead);
                    if (r == 0) break;
                    bytesRead += r;
                }

                // Dekoduj fragment obrazu z bajtów (format pickle w Pythonie → PNG bytes)
                // Dla prostoty zakładamy, że serwer może wysyłać fragment jako PNG bytes
                // Jeżeli używasz pickle w Pythonie, trzeba by dodać prostą warstwę do wysyłania jako PNG
                using var ms = new MemoryStream(data);
                Bitmap fragment = new Bitmap(ms);
                Console.WriteLine("📥 Otrzymano fragment obrazu");

                // === Przetwarzanie filtrem Sobela ===
                Bitmap processed = ApplySobelFilter(fragment);
                Console.WriteLine("⚙️ Przetworzono fragment");

                // === Odesłanie wyniku ===
                using var outStream = new MemoryStream();
                processed.Save(outStream, ImageFormat.Png);
                byte[] outData = outStream.ToArray();

                byte[] outLen = BitConverter.GetBytes(outData.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(outLen);

                stream.Write(outLen, 0, 4);
                stream.Write(outData, 0, outData.Length);

                Console.WriteLine("📤 Odesłano przetworzony fragment do serwera");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd: {ex.Message}");
            }
        }

        // === Filtr Sobela dla Bitmap ===
        static Bitmap ApplySobelFilter(Bitmap src)
        {
            Bitmap gray = new Bitmap(src.Width, src.Height);
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    Color c = src.GetPixel(x, y);
                    int g = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                    gray.SetPixel(x, y, Color.FromArgb(g, g, g));
                }
            }

            int[,] gx = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            int[,] gy = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

            Bitmap result = new Bitmap(src.Width, src.Height);
            for (int y = 1; y < src.Height - 1; y++)
            {
                for (int x = 1; x < src.Width - 1; x++)
                {
                    int sx = 0, sy = 0;
                    for (int j = -1; j <= 1; j++)
                        for (int i = -1; i <= 1; i++)
                        {
                            int g = gray.GetPixel(x + i, y + j).R;
                            sx += gx[j + 1, i + 1] * g;
                            sy += gy[j + 1, i + 1] * g;
                        }
                    int val = (int)Math.Min(255, Math.Sqrt(sx * sx + sy * sy));
                    result.SetPixel(x, y, Color.FromArgb(val, val, val));
                }
            }
            return result;
        }
    }
}
