﻿// using System;
// using System.Collections.Concurrent;
// using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
// using Newtonsoft.Json;
// using NLog.Layouts;
//
// namespace Ciribob.DCS.SimpleRadio.Standalone.Common
// {
//     public class RadioReceivingState
//     {
//         [JsonIgnore]
//         public long LastReceivedAt { get; set; }
//
//         public bool IsSecondary { get; set; }
//         public bool IsSimultaneous { get; set; }
//         public int ReceivedOn { get; set; }
// 
//         public string SentBy { get; set; }
//
//         public bool IsReceiving
//         {
//             get
//             {
//                 return (DateTime.Now.Ticks - LastReceviedAt) < 3500000;
//             }
//         }
//     }
// }

