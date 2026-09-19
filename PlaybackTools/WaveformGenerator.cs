using System;
using System.Diagnostics;
using System.IO;

namespace PlaybackTools
{
    public static class WaveformGenerator
    {

        private const int SampleRate = 22050;

        private static string ResolveFfmpegPath()
        {
            string bundled = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
            return File.Exists(bundled) ? bundled : "ffmpeg";
        }


        public static (float[] min, float[] max) GeneratePeaks(string filePath, int bucketCount)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveFfmpegPath(),
                Arguments = $"-i \"{filePath}\" -vn -ac 1 -ar {SampleRate} -f f32le -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start ffmpeg - is it installed and on PATH?");

            var stderrTask = process.StandardError.ReadToEndAsync();

            byte[] rawBytes;
            using (var buffer = new MemoryStream())
            {
                process.StandardOutput.BaseStream.CopyTo(buffer);
                rawBytes = buffer.ToArray();
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                string stderr = stderrTask.Result;
                Debug.WriteLine($"[WaveformGenerator] ffmpeg exited {process.ExitCode}: {stderr}");
            }

            int floatCount = rawBytes.Length / 4;
            var samples = new float[floatCount];
            Buffer.BlockCopy(rawBytes, 0, samples, 0, floatCount * 4);

            var min = new float[bucketCount];
            var max = new float[bucketCount];
            BucketPeaks(samples, bucketCount, min, max);
            return (min, max);
        }

        private static void BucketPeaks(float[] samples, int bucketCount, float[] min, float[] max)
        {
            if (samples.Length == 0) return;

            long samplesPerBucket = Math.Max(1, samples.Length / bucketCount);
            int bucketIndex = 0;
            long sampleCount = 0;
            float curMin = 0, curMax = 0;

            for (int i = 0; i < samples.Length && bucketIndex < bucketCount; i++)
            {
                float v = samples[i];
                if (v > curMax) curMax = v;
                if (v < curMin) curMin = v;

                sampleCount++;
                if (sampleCount >= samplesPerBucket)
                {
                    min[bucketIndex] = curMin;
                    max[bucketIndex] = curMax;
                    bucketIndex++;
                    sampleCount = 0;
                    curMin = 0;
                    curMax = 0;
                }
            }
        }
    }
}