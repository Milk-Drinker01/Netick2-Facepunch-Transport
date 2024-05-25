using UnityEngine;
using Netick;
using Netick.Unity;
using Steamworks;

namespace Netick.Transports.Facepunch.Extras
{
    public class SteamVoiceClient : NetworkBehaviour
    {
        [SerializeField]
        public float ChatVolume = 1;

        [SerializeField]
        private AudioSource source;

        private SteamVoiceChat _steamVoiceChat;

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

            if (Sandbox.IsServer && Sandbox.TryGetComponent<SteamVoiceChat>(out _steamVoiceChat))
                _steamVoiceChat.ConnectionIdToPlayerObjectID.Add(InputSource.PlayerId, Object.Id);
        }

        public override void NetworkDestroy()
        {
            if (Sandbox.IsServer && _steamVoiceChat != null)
                _steamVoiceChat.ConnectionIdToPlayerObjectID.Remove(InputSource.PlayerId);
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
}
