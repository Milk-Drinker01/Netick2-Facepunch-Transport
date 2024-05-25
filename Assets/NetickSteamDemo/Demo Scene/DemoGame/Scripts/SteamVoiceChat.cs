using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Netick;
using Netick.Unity;
using Steamworks;
using System.IO;
using System;

public class SteamVoiceChat : NetickBehaviour
{
    const int VoiceDataID = 0;

    private MemoryStream localVoiceStream;
    private MemoryStream compressedDataReceived;
    private MemoryStream uncompressedDataReceived;

    private byte[] compressedVoiceData;
    //private byte[] myCompressedVoiceData;
    //private byte[] receivedCompressedVoiceData;

    private uint optimalRate;
    private uint voiceBufferSize;
    private float[] voiceBuffer;

    private int playbackBuffer;
    private int dataPosition;
    private int dataReceived;

    public override unsafe void NetworkStart()
    {
        Sandbox.Events.OnDataReceived += OnDataReceived;

        compressedVoiceData = new byte[1024];
        //receivedCompressedVoiceData = new byte[1024];
        //myCompressedVoiceData = new byte[1024];
        localVoiceStream = new MemoryStream(compressedVoiceData);
        compressedDataReceived = new MemoryStream();
        uncompressedDataReceived = new MemoryStream();

        optimalRate = SteamUser.OptimalSampleRate;
        voiceBufferSize = optimalRate * 5;
        voiceBuffer = new float[voiceBufferSize];
    }

    public override void NetworkFixedUpdate()
    {
        if (!SteamClient.IsValid)
            return;

        //UnityEngine.Profiling.Profiler.BeginSample("vc");
        if (Steamworks.SteamUser.HasVoiceData)
        {
            int numBytes = Steamworks.SteamUser.ReadVoiceData(localVoiceStream);
            localVoiceStream.Position = 0;
            //myCompressedVoiceData = voiceStream.GetBuffer();
            if (Sandbox.IsServer)
                SendVoiceDataToClients(Sandbox, 0, numBytes);
            else
                Sandbox.ConnectedServer.SendData(VoiceDataID, compressedVoiceData, numBytes, TransportDeliveryMethod.Unreliable);
        }

        SteamUser.VoiceRecord = Input.GetKey(KeyCode.V);
        //UnityEngine.Profiling.Profiler.EndSample();
    }

    unsafe void OnDataReceived(NetworkSandbox sandbox, NetworkConnection sender, byte id, byte* data, int len, TransportDeliveryMethod transportDeliveryMethod)
    {
        if (id == VoiceDataID)
        {
            if (sandbox.IsServer)
            {
                for (int i = 0; i < len; i++)
                    compressedVoiceData[i] = data[i];

                NetworkObject NO = (NetworkObject)(sandbox.ConnectedPlayers[sender.PlayerId].PlayerObject);
                int userId = NO.Id;

                DecompressVoice(userId, len);

                SendVoiceDataToClients(sandbox, userId, len);
            }
            else
            {
                Debug.Log("received");
                for (int i = 0; i < len; i++)
                    compressedVoiceData[i] = data[i];

                int userId = BitConverter.ToInt32(compressedVoiceData, len - 4);
                Debug.Log(userId);
                DecompressVoice(userId, len - 4);
            }
        }
    }

    unsafe void SendVoiceDataToClients(NetworkSandbox sandbox, int playerID, int length)
    {
        //append player id to the end of the voice data buffer
        byte* idPointer = (byte*)&playerID;
        for (int i = 0; i < 4; i++)
            compressedVoiceData[length + i] = idPointer[i];

        //send the voice chat data
        foreach (NetworkConnection conn in sandbox.ConnectedClients)
            conn.SendData(VoiceDataID, compressedVoiceData, length + 4, TransportDeliveryMethod.Unreliable);
    }

    void DecompressVoice(int clientID, int length)
    {
        compressedDataReceived.Write(compressedVoiceData, 0, length);
        compressedDataReceived.Position = 0;

        int uncompressedWritten = SteamUser.DecompressVoice(compressedDataReceived, length, uncompressedDataReceived);
        compressedDataReceived.Position = 0;

        byte[] outputBuffer = uncompressedDataReceived.GetBuffer();
        
        if (Sandbox.TryGetBehaviour<SteamVoiceClient>(clientID, out SteamVoiceClient cl))
            cl.VoiceDataReceived(outputBuffer, uncompressedWritten);
        uncompressedDataReceived.Position = 0;
    }
}
