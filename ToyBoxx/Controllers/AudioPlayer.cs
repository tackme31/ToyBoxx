using NAudio.Wave;
using System.IO;

namespace ToyBoxx.Controllers;

public class AudioPlayer : IDisposable
{
    private WasapiOut? _output;

    public async Task Play(IWaveProvider waveProvider, int latency, int delay)
    {
        var output = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Exclusive, latency);
        output.Init(waveProvider);

        await Task.Delay(delay);

        output.Play();
        _output = output;
    }

    public static IWaveProvider FromInt16(Stream stream, int sampleRate, int channel)
    {
        var provider = new RawSourceWaveStream(stream, new WaveFormat(sampleRate, bits: 16, channel));
        return provider;
    }

    public void Dispose()
    {
        if (_output is not null)
        {
            _output.Dispose();
            _output = null;
        }
    }
}
