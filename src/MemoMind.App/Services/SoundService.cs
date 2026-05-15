using System.IO;
using System.Media;

namespace MemoMind.App.Services;

public class SoundService
{
    private const int SampleRate = 44100;
    private const short BitsPerSample = 16;
    private const short Channels = 1;

    public void PlayWorkToBreak()
    {
        PlayToneSequence([
            (523.25f, 0.15f),
            (659.25f, 0.15f),
            (783.99f, 0.25f)
        ]);
    }

    public void PlayBreakToWork()
    {
        PlayToneSequence([
            (783.99f, 0.15f),
            (659.25f, 0.15f),
            (523.25f, 0.25f)
        ]);
    }

    public void PlayAlarm()
    {
        PlayToneSequence([
            (880f, 0.12f), (0, 0.06f),
            (880f, 0.12f), (0, 0.06f),
            (880f, 0.12f), (0, 0.06f),
            (1108.73f, 0.3f)
        ]);
    }

    public void PlayCountdownEnd()
    {
        PlayToneSequence([
            (659.25f, 0.12f), (0, 0.05f),
            (659.25f, 0.12f), (0, 0.05f),
            (659.25f, 0.12f), (0, 0.05f),
            (523.25f, 0.4f)
        ]);
    }

    public void PlayCustomWav(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        Task.Run(() =>
        {
            try
            {
                using var player = new SoundPlayer(filePath);
                player.PlaySync();
            }
            catch
            {
                // Custom sound playback failure is non-critical
            }
        });
    }

    private static void PlayToneSequence((float frequency, float durationSec)[] notes)
    {
        Task.Run(() =>
        {
            try
            {
                var samples = GenerateToneSequence(notes);
                var wavBytes = BuildWav(samples);
                using var stream = new MemoryStream(wavBytes);
                using var player = new SoundPlayer(stream);
                player.PlaySync();
            }
            catch
            {
                // Sound playback failure is non-critical
            }
        });
    }

    private static short[] GenerateToneSequence((float frequency, float durationSec)[] notes)
    {
        var totalSamples = (int)(notes.Sum(n => n.durationSec) * SampleRate);
        var samples = new short[totalSamples];
        var offset = 0;

        foreach (var (frequency, durationSec) in notes)
        {
            var noteSamples = (int)(durationSec * SampleRate);

            if (frequency <= 0)
            {
                offset += noteSamples;
                continue;
            }

            for (var i = 0; i < noteSamples; i++)
            {
                var t = (double)(offset + i) / SampleRate;
                var amplitude = 0.3;

                var envelopeAttack = Math.Min(1.0, (double)i / (SampleRate * 0.01));
                var envelopeRelease = Math.Min(1.0, (double)(noteSamples - i) / (SampleRate * 0.02));
                var envelope = Math.Min(envelopeAttack, envelopeRelease);
                amplitude *= envelope;

                var sample = (short)(Math.Sin(2 * Math.PI * frequency * t) * amplitude * short.MaxValue);
                samples[offset + i] = sample;
            }

            offset += noteSamples;
        }

        return samples;
    }

    private static byte[] BuildWav(short[] samples)
    {
        var dataSize = samples.Length * 2;
        var fileSize = 44 + dataSize;

        using var ms = new MemoryStream(fileSize);
        using var writer = new BinaryWriter(ms);

        writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
        writer.Write(fileSize - 8);
        writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });

        writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(Channels);
        writer.Write(SampleRate);
        writer.Write(SampleRate * Channels * BitsPerSample / 8);
        writer.Write((short)(Channels * BitsPerSample / 8));
        writer.Write(BitsPerSample);

        writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
        writer.Write(dataSize);
        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return ms.ToArray();
    }
}
