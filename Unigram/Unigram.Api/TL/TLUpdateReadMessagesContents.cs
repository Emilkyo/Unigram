// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLUpdateReadMessagesContents : TLUpdateBase, ITLMultiPts 
	{
		public TLVector<Int32> Messages { get; set; }
		public Int32 Pts { get; set; }
		public Int32 PtsCount { get; set; }

		public TLUpdateReadMessagesContents() { }
		public TLUpdateReadMessagesContents(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.UpdateReadMessagesContents; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Messages = TLFactory.Read<TLVector<Int32>>(from, cache);
			Pts = from.ReadInt32();
			PtsCount = from.ReadInt32();
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			to.Write(0x68C13933);
			to.WriteObject(Messages, cache);
			to.Write(Pts);
			to.Write(PtsCount);
			if (cache) WriteToCache(to);
		}
	}
}