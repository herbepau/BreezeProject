﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.Client;

namespace NTumbleBit
{
	public class CycleProgressInfo
	{
		public CyclePeriod Period { get; private set; }
		public int Height { get; private set; }
		public int BlocksLeft { get; private set; }
		public int Start { get; private set; }
		public bool Failed { get; private set; } = false;

		[JsonIgnore]
		public PaymentStateMachineStatus StatusEnum { get; private set; }

		[JsonIgnore]
		public CyclePhase PhaseEnum { get; private set; }

		public string AsciiArt { get; private set; }

		public string Status
		{
			get { return this.StatusEnum.ToString(); }
		}

		public string Phase {
			get { return this.PhaseEnum.ToString(); }
		}

		public CycleProgressInfo(CyclePeriod period, int height, int blocksLeft, int start, PaymentStateMachineStatus status, CyclePhase phase, string asciiArt)
		{
			this.Period = period;
			this.Height = height;
			this.BlocksLeft = blocksLeft;
			this.Start = start;
			this.StatusEnum = status;
			this.PhaseEnum = phase;
			this.AsciiArt = asciiArt;

		    this.CheckForFailedState();
		}

	    private void CheckForFailedState()
	    {
            //CyclePhases
            //
	        //Registration,
	        //ClientChannelEstablishment,
	        //TumblerChannelEstablishment,
	        //PaymentPhase,
	        //TumblerCashoutPhase,
	        //ClientCashoutPhase

            if (this.PhaseEnum > CyclePhase.TumblerChannelEstablishment && this.StatusEnum == PaymentStateMachineStatus.Registered)
	            this.Failed = true;
	        if (this.PhaseEnum > CyclePhase.PaymentPhase && this.StatusEnum < PaymentStateMachineStatus.TumblerChannelConfirmed)
	            this.Failed = true;
	        if (this.PhaseEnum > CyclePhase.TumblerCashoutPhase && this.StatusEnum < PaymentStateMachineStatus.PuzzleSolutionObtained)
	            this.Failed = true;
	        if (this.PhaseEnum == CyclePhase.ClientCashoutPhase && this.StatusEnum < PaymentStateMachineStatus.PuzzleSolutionObtained)
	            this.Failed = true;
        }
    }

	public class ProgressInfo
    {
		public int Height { get; private set; }

		public List<CycleProgressInfo> CycleProgressInfoList = new List<CycleProgressInfo>();

	    public ProgressInfo(int height)
	    {
		    this.Height = height;
	    }

	    public void Save()
        {
            try
            {
                if (!Directory.Exists(ProgressInfo.Folder)) Directory.CreateDirectory(ProgressInfo.Folder);
                string filename = Path.Combine(ProgressInfo.Folder, "tb_progress.json");

                using (var file = File.CreateText(filename))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, this);
                }
            }
            //if any issue with the save we isolate it here
            //so there is no interference with tumbling
            catch (Exception){}
        }

	    public static void RemoveProgressFile()
	    {
		    string filename = Path.Combine(ProgressInfo.Folder, "tb_progress.json");
			if (File.Exists(filename)) File.Delete(filename);
		}

        private static string Folder
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StratisNode\\bitcoin\\TumbleBit");
                return Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".stratisnode", "bitcoin", "TumbleBit");
            }
        }
	}
}
