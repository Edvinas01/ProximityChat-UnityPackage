using System;
using Unity.Netcode;
using UnityEngine;

namespace ProximityChat
{
    /// <summary>
    /// Networks voice audio -- recording, encoding and sending it over the network if owner,
    /// otherwise receiving, decoding and playing it back as 3D spatial audio.
    /// </summary>
    public class VoiceNetworker : NetworkBehaviour
    {
        [Header("Recorder")]
        [SerializeField] private VoiceRecorder _voiceRecorder;
        [Header("Emitter")]
        [SerializeField] private VoiceEmitter _voiceEmitter;
        [Header("Debug")]
        [SerializeField] private bool _playbackOwnVoice;

        // Encode/decode
        private VoiceEncoder _voiceEncoder;
        private VoiceDecoder _voiceDecoder;

        void Start()
        {
            // Owner should record voice and encode it
            if (IsOwner)
            {
                // Disable voice emitter
                _voiceEmitter.enabled = _playbackOwnVoice;
                // Initialize voice recorder
                _voiceRecorder.Init();
                // Initialize voice encoder
                _voiceEncoder = new VoiceEncoder(_voiceRecorder.RecordedSamplesQueue);
            }
            // Non-owners should receive encoded voice,
            // decode it and play it as audio
            if (!IsOwner || _playbackOwnVoice)
            {
                // Disable voice recorder
                _voiceRecorder.enabled = _playbackOwnVoice;
                // Initialize voice decoder
                _voiceDecoder = new VoiceDecoder();
                // Initialize voice emitter
                _voiceEmitter.Init(VoiceConsts.OpusSampleRate);
            }
        }

        [Rpc(SendTo.NotMe)]
        public void SendEncodedVoiceToOtherClients(byte[] encodedVoiceData)
        {
            if (!IsOwner || _playbackOwnVoice)
            {
                Span<short> decodedVoiceSamples = _voiceDecoder.DecodeVoiceSamples(encodedVoiceData);
                _voiceEmitter.EnqueueSamplesForPlayback(decodedVoiceSamples);
            }
        }

        /// <summary>
        /// Starts recording and sending voice data over the network.
        /// </summary>
        public void StartRecording()
        {
            if (!IsOwner) return;
            _voiceRecorder.StartRecording();
        }

        /// <summary>
        /// Stops recording and sending voice data over the network.
        /// </summary>
        public void StopRecording()
        {
            if (!IsOwner) return;
            _voiceRecorder.StopRecording();
        }

        /// <summary>
        /// Sets the output volume of the voice emitter.
        /// </summary>
        /// <param name="volume">Volume from 0 to 1</param>
        public void SetOutputVolume(float volume)
        {
            if (IsOwner && !_playbackOwnVoice) return;
            _voiceEmitter.SetVolume(volume);
        }

        void LateUpdate()
        {
            if (IsOwner)
            {
                // Encode as much queued voice as possible
                while (_voiceEncoder.HasVoiceLeftToEncode)
                {
                    Span<byte> encodedVoice = _voiceEncoder.GetEncodedVoice();
                    SendEncodedVoiceToOtherClients(encodedVoice.ToArray());
                }
                // If we've stopped recording but there's still more left to be cleared,
                // force encode it with silence
                if (!_voiceRecorder.IsRecording && !_voiceEncoder.QueueIsEmpty)
                {
                    Span<byte> encodedVoice = _voiceEncoder.GetEncodedVoice(true);
                    SendEncodedVoiceToOtherClients(encodedVoice.ToArray());
                }
            }
        }
    }
}
