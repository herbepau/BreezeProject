﻿using NBitcoin;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public enum PromiseServerStates
	{
		WaitingEscrow,
		WaitingHashes,
		WaitingRevelation,
		Completed
	}

	public class PromiseServerSession
	{
		public class EncryptedSignature
		{
			public EncryptedSignature()
			{

			}
			public EncryptedSignature(ECDSASignature ecdsa, uint256 signedHash, PuzzleSolution solution)
			{
				this.Signature = ecdsa;
				this.PuzzleSolution = solution;
				this.SignedHash = signedHash;
			}

			public uint256 SignedHash
			{
				get; set;
			}
			public ECDSASignature Signature
			{
				get; set;
			}

			public PuzzleSolution PuzzleSolution
			{
				get; set;
			}
		}
		public PromiseServerSession(PromiseParameters parameters)
		{
			_Parameters = parameters ?? new PromiseParameters();
		}

		public PromiseServerSession(InternalState state, PromiseParameters parameters)
		{
			if(state == null)
				throw new ArgumentNullException("state");
			_Parameters = parameters ?? new PromiseParameters();
			this._InternalState = state;
		}

		public class InternalState
		{
			public EncryptedSignature[] EncryptedSignatures
			{
				get; set;
			}			

			public PromiseServerStates State
			{
				get; set;
			}
			public uint256 FakeIndexesHash
			{
				get;
				set;
			}
			public ScriptCoin EscrowedCoin
			{
				get;
				set;
			}
			public Key EscrowKey
			{
				get;
				set;
			}
			public Key RedeemKey
			{
				get;
				set;
			}
		}

		public InternalState GetInternalState()
		{
			var state = Serializer.Clone(_InternalState);
			return state;
		}

		InternalState _InternalState = new InternalState();
		

		readonly PromiseParameters _Parameters;
		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		public void ConfigureEscrowedCoin(ScriptCoin escrowedCoin, Key escrowKey, Key redeemKey)
		{
			if(escrowedCoin == null)
				throw new ArgumentNullException("escrowedCoin");
			if(escrowKey == null)
				throw new ArgumentNullException("escrowKey");
			if(redeemKey == null)
				throw new ArgumentNullException("redeemKey");
			AssertState(PromiseServerStates.WaitingEscrow);
			var escrow = EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(escrowedCoin.Redeem);
			if(escrow == null || !escrow.EscrowKeys.Any(e => e == escrowKey.PubKey))
				throw new PuzzleException("Invalid escrow");
			_InternalState.EscrowedCoin = escrowedCoin;
			_InternalState.EscrowKey = escrowKey;
			_InternalState.RedeemKey = redeemKey;
			_InternalState.State = PromiseServerStates.WaitingHashes;
		}

		public string Id
		{
			get
			{
				return _InternalState.EscrowedCoin.ScriptPubKey.ToHex();
			}
		}

		public ServerCommitment[] SignHashes(SignaturesRequest sigRequest)
		{
			if(sigRequest == null)
				throw new ArgumentNullException("sigRequest");
			if(sigRequest.Hashes.Length != Parameters.GetTotalTransactionsCount())
				throw new ArgumentException("Incorrect number of hashes, expected " + sigRequest.Hashes.Length);
			AssertState(PromiseServerStates.WaitingHashes);
			List<ServerCommitment> promises = new List<ServerCommitment>();
			List<EncryptedSignature> encryptedSignatures = new List<EncryptedSignature>();
			foreach(var hash in sigRequest.Hashes)
			{
				var ecdsa = _InternalState.EscrowKey.Sign(hash);
				var ecdsaDER = ecdsa.ToDER();
				var key = new XORKey(Parameters.ServerKey);
				var promise = key.XOR(ecdsaDER);
				PuzzleSolution solution = new PuzzleSolution(key.ToBytes());
				var puzzle = Parameters.ServerKey.GeneratePuzzle(ref solution);
				promises.Add(new ServerCommitment(puzzle.PuzzleValue, promise));
				encryptedSignatures.Add(new EncryptedSignature(ecdsa, hash, solution));
			}
			_InternalState.State = PromiseServerStates.WaitingRevelation;
			_InternalState.EncryptedSignatures = encryptedSignatures.ToArray();
			_InternalState.FakeIndexesHash = sigRequest.FakeIndexesHash;
			return promises.ToArray();
		}



		public ServerCommitmentsProof CheckRevelation(ClientRevelation revelation)
		{
			if(revelation == null)
				throw new ArgumentNullException("revelation");
			if(revelation.Salts.Length != Parameters.FakeTransactionCount || revelation.FakeIndexes.Length != Parameters.FakeTransactionCount)
				throw new ArgumentNullException("The revelation should contains " + Parameters.FakeTransactionCount + " indexes and salts");
			AssertState(PromiseServerStates.WaitingRevelation);

			var indexSalt = revelation.IndexesSalt;
			if(_InternalState.FakeIndexesHash != PromiseUtils.HashIndexes(ref indexSalt, revelation.FakeIndexes))
			{
				throw new PuzzleException("Invalid index salt");
			}

			List<PuzzleSolution> solutions = new List<PuzzleSolution>();
			for(int i = 0; i < Parameters.FakeTransactionCount; i++)
			{
				var salt = revelation.Salts[i];
				var encrypted = _InternalState.EncryptedSignatures[revelation.FakeIndexes[i]];
				var actualSignedHash = Parameters.CreateFakeHash(salt);
				if(actualSignedHash != encrypted.SignedHash)
					throw new PuzzleException("Incorrect salt provided");
				solutions.Add(encrypted.PuzzleSolution);
			}

			// We can throw away the fake puzzles
			_InternalState.EncryptedSignatures = _InternalState.EncryptedSignatures
										.Where((e, i) => !revelation.FakeIndexes.Contains(i)).ToArray();

			Quotient[] quotients = new Quotient[Parameters.RealTransactionCount - 1];
			for(int i = 0; i < _InternalState.EncryptedSignatures.Length - 1; i++)
			{
				var a = _InternalState.EncryptedSignatures[i].PuzzleSolution._Value;
				var b = _InternalState.EncryptedSignatures[i + 1].PuzzleSolution._Value;
				quotients[i] = new Quotient(b.Multiply(a.ModInverse(Parameters.ServerKey._Key.Modulus)).Mod(Parameters.ServerKey._Key.Modulus));
			}
			_InternalState.FakeIndexesHash = null;
			_InternalState.State = PromiseServerStates.Completed;
			return new ServerCommitmentsProof(solutions.ToArray(), quotients);
		}


		public PromiseServerStates State
		{
			get
			{
				return _InternalState.State;
			}
		}

		public ScriptCoin EscrowedCoin
		{
			get
			{
				return _InternalState.EscrowedCoin;
			}
		}

		private void AssertState(PromiseServerStates state)
		{
			if(state != _InternalState.State)
				throw new InvalidOperationException("Invalid state, actual " + _InternalState.State + " while expected is " + state);
		}		
	}
}
