using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using NLog;
using System;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public static class RecordingClientState
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static SRClientBase? _instance;

        public static SRClientBase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Initialize();
                }
                return _instance;
            }
        }

        //allow re-initialization if needed
        public static SRClientBase Initialize(string clientGuid = "", string name = "")
        {
            _instance = new SRClientBase
            {
                ClientGuid = string.IsNullOrEmpty(clientGuid) ? ShortGuid.NewGuid() : clientGuid,
                Gateway = true,
                LineOfSightLoss = 0.0f,
                Name = string.IsNullOrEmpty(name) ? ("RecordingClient_" + Environment.MachineName) : name,
            };
            Logger.Info($"RecordingClientState re-initialized with GUID {_instance.ClientGuid} and name {_instance.Name}");

            return _instance;
        }
    }
}