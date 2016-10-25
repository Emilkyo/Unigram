// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLUpdateReadHistoryOutbox : TLUpdateBase, ITLMultiPts 
	{
		public TLPeerBase Peer { get; set; }
		public Int32 MaxId { get; set; }
		public Int32 Pts { get; set; }
		public Int32 PtsCount { get; set; }

		public TLUpdateReadHistoryOutbox() { }
		public TLUpdateReadHistoryOutbox(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.UpdateReadHistoryOutbox; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Peer = TLFactory.Read<TLPeerBase>(from, cache);
			MaxId = from.ReadInt32();
			Pts = from.ReadInt32();
			PtsCount = from.ReadInt32();
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			to.Write(0x2F2F21BF);
			to.WriteObject(Peer, cache);
			to.Write(MaxId);
			to.Write(Pts);
			to.Write(PtsCount);
			if (cache) WriteToCache(to);
		}
	}
}