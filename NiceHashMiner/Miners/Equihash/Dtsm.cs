﻿using Newtonsoft.Json;
using NiceHashMiner.Enums;
using NiceHashMiner.Miners.Parsing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NiceHashMiner.Algorithms;
using NiceHashMiner.Configs;
using System.IO;

namespace NiceHashMiner.Miners
{
    public class Dstm : Miner
    {
        private const double DevFee = 2.0;
        private const string LookForStart = "sol/s   ";
        private const string LookForEnd = "avg";

        private int _benchmarkTime = 120;

        public Dstm() : base("dtsm")
        {
            ConectionType = NhmConectionType.NONE;
        }
        protected override int GetMaxCooldownTimeInMilliseconds()
        {
            return 60 * 1000 * 5;
        }

        public override void Start(string url, string btcAdress, string worker)
        {
            LastCommandLine = GetStartCommand(url, btcAdress, worker);
            ProcessHandle = _Start();
        }

        private string GetStartBenchmarkCommand(string url, string btcAddress, string worker)
        {
            var urls = url.Split(':');
            var server = urls.Length > 0 ? urls[0] : "";
            var port = urls.Length > 1 ? urls[1] : "";

            return $" {GetDeviceCommand()} " +
                   $"--server {server} " +
                   $"--port {port} " +
                   $"--user {btcAddress}.{worker} " +
                   $"--telemetry=127.0.0.1:{ApiPort} ";

        }
        private string GetStartCommand(string url, string btcAddress, string worker)
        {
            var urls = url.Split(':');
            var server = urls.Length > 0 ? urls[0] : "";
            var port = urls.Length > 1 ? urls[1] : "";

            var config_body = "[GLOBAL]\r\n" +
                                 $"dev=" + string.Join(",", MiningSetup.MiningPairs.Select(p => p.Device.ID)) + "\r\n" +
                                 "time=1\r\n" +
                                 "color=1\r\n" +
                                 $"telemetry=127.0.0.1:{ApiPort}\r\n" +
                                 ExtraLaunchParametersParser.ParseForMiningSetup(MiningSetup, DeviceType.NVIDIA).Replace("--", "") + "\r\n" +
                                 "\r\n" +
                                 "[POOL]\r\n" +
                                 "server=ssl://equihash.eu.nicehash.com\r\n" +
                                 "port=33357\r\n" +
                                 $"user={btcAddress}.{worker}\r\n" +
                                 "pass=x\r\n" +
                                 "\r\n" +
                                 "[POOL]\r\n" +
                                 "server=ssl://equihash.hk.nicehash.com\r\n" +
                                 "port=33357\r\n" +
                                 $"user={btcAddress}.{worker}\r\n" +
                                 "pass=x\r\n" +
                                 "\r\n" +
                                 "[POOL]\r\n" +
                                 "server=ssl://equihash.in.nicehash.com\r\n" +
                                 "port=33357\r\n" +
                                 $"user={btcAddress}.{worker}\r\n" +
                                 "pass=x\r\n" +
                                 "\r\n" +
                                 "[POOL]\r\n" +
                                 "server=ssl://equihash.jp.nicehash.com\r\n" +
                                 "port=33357\r\n" +
                                 $"user={btcAddress}.{worker}\r\n" +
                                 "pass=x\r\n" +
                                 "\r\n" +
                                 "[POOL]\r\n" +
                                 "server=ssl://equihash.usa.nicehash.com\r\n" +
                                 "port=33357\r\n" +
                                 $"user={btcAddress}.{worker}\r\n" +
                                 "pass=x\r\n" +
                                 "\r\n" +
                                 "[POOL]\r\n" +
                                 "server=ssl://equihash.br.nicehash.com\r\n" +
                                 "port=33357\r\n" +
                                 $"user={btcAddress}.{worker}\r\n" +
                                 "pass=x\r\n" +
                                 "\r\n";

            FileStream fs_dstm = new FileStream("bin_3rdparty\\dstm\\nicehash.cfg", FileMode.Create, FileAccess.Write);
            StreamWriter w = new StreamWriter(fs_dstm);
            w.Write(config_body);
            w.Flush();
            w.Close();

            return "--cfg-file=nicehash.cfg";

        }

        private string GetDeviceCommand()
        {
            return " --dev " +
                   string.Join(" ", MiningSetup.MiningPairs.Select(p => p.Device.ID)) +
                   ExtraLaunchParametersParser.ParseForMiningSetup(MiningSetup, DeviceType.NVIDIA);
        }

        protected override void _Stop(MinerStopType willswitch)
        {
            Stop_cpu_ccminer_sgminer_nheqminer(willswitch);
        }

        #region Benchmarking

        protected override string BenchmarkCreateCommandLine(Algorithm algorithm, int time)
        {
            var url = GetServiceUrl(algorithm.NiceHashID);

            _benchmarkTime = Math.Max(time, 120);

            return GetStartBenchmarkCommand(url, Globals.DemoUser, ConfigManager.GeneralConfig.WorkerName.Trim()) +
                   $" --logfile={GetLogFileName()}";
        }

        protected override void BenchmarkThreadRoutine(object commandLine)
        {
            BenchmarkThreadRoutineAlternate(commandLine, _benchmarkTime);
        }

        protected override void ProcessBenchLinesAlternate(string[] lines)
        {
            var benchSum = 0d;
            var benchCount = 0;
            foreach (var line in lines)
            {
                BenchLines.Add(line);
                var lowered = line.ToLower();
                var start = lowered.IndexOf(LookForStart, StringComparison.Ordinal);
                if (start <= -1) continue;
                lowered = lowered.Substring(start, lowered.Length - start);
                lowered = lowered.Replace(LookForStart, "");
                var end = lowered.IndexOf(LookForEnd, StringComparison.Ordinal);
                lowered = lowered.Substring(0, end);
                if (double.TryParse(lowered, out var speed))
                {
                    benchSum += speed;
                    benchCount++;
                }
            }
            BenchmarkAlgorithm.BenchmarkSpeed = (benchSum / Math.Max(1, benchCount)) * (1 - DevFee * 0.01);
        }

        protected override void BenchmarkOutputErrorDataReceivedImpl(string outdata)
        { }

        protected override bool BenchmarkParseLine(string outdata)
        {
            return false;
        }

        #endregion

        #region API

        public override async Task<ApiData> GetSummaryAsync()
        {
            CurrentMinerReadStatus = MinerApiReadStatus.NONE;

            var ad = new ApiData(MiningSetup.CurrentAlgorithmType);
            var request = JsonConvert.SerializeObject(new
            {
                method = "getstat",
                id = 1
            });

            var response = await GetApiDataAsync(ApiPort, request);
            DtsmResponse resp = null;

            try
            {
                resp = JsonConvert.DeserializeObject<DtsmResponse>(response);
            }
            catch (Exception e)
            {
                Helpers.ConsolePrint(MinerTag(), e.Message);
            }

            if (resp?.result != null)
            {
                ad.Speed = resp.result.Sum(gpu => gpu.sol_ps);
                CurrentMinerReadStatus = MinerApiReadStatus.GOT_READ;
            }
            if (ad.Speed == 0)
            {
                CurrentMinerReadStatus = MinerApiReadStatus.READ_SPEED_ZERO;
            }

            return ad;
        }

        protected override bool IsApiEof(byte third, byte second, byte last)
        {
            return second == 125 && last == 10;
        }

        #region JSON Models
#pragma warning disable

        public class DtsmResponse
        {
            public List<DtsmGpuResult> result { get; set; }
        }

        public class DtsmGpuResult
        {
            public double sol_ps { get; set; } = 0;
        }

#pragma warning restore
        #endregion

        #endregion
    }
}