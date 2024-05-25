using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Netick;
using Netick.Unity;
using Steamworks;
using System.IO;

public class SteamVoiceClient : NetworkBehaviour
{
    const int VoiceDataID = 0;

    [SerializeField]
    public float ChatVolume = 1;

    [SerializeField]
    private AudioSource source;

    private uint optimalRate;
    private uint voiceBufferSize;
    private float[] voiceBuffer;

    private int playbackBuffer;
    private int dataPosition;
    private int dataReceived;

    public override unsafe void NetworkStart()
    {
        optimalRate = SteamUser.OptimalSampleRate;
        voiceBufferSize = optimalRate * 5;
        voiceBuffer = new float[voiceBufferSize];

        source.clip = AudioClip.Create("VoiceData", (int)256, 1, (int)optimalRate, true, OnAudioRead, null);
        source.loop = true;
        source.Play();
    }

    private void OnAudioRead(float[] data)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = 0;

            if (playbackBuffer > 0)
            {
                dataPosition++;
                playbackBuffer -= 1;

                data[i] = voiceBuffer[dataPosition % voiceBufferSize];
                data[i] *= ChatVolume;
            }
        }
    }

    public void VoiceDataReceived(byte[] uncompressed, int iSize)
    {
        WriteToClip(uncompressed, iSize);
    }

    void WriteToClip(byte[] uncompressed, int iSize)
    {
        for (int i = 0; i < iSize; i += 2)
        {
            WriteToClip((short)(uncompressed[i] | uncompressed[i + 1] << 8) / 32767.0f);
        }
    }

    void WriteToClip(float f)
    {
        voiceBuffer[dataReceived % voiceBufferSize] = f;
        dataReceived++;
        playbackBuffer++;
    }
}
