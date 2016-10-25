// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLUpdateShort : TLUpdatesBase 
	{
		public TLUpdateBase Update { get; set; }
		public Int32 Date { get; set; }

		public TLUpdateShort() { }
		public TLUpdateShort(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.UpdateShort; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Update = TLFactory.Read<TLUpdateBase>(from, cache);
			Date = from.ReadInt32();
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			to.Write(0x78D4DEC1);
			to.WriteObject(Update, cache);
			to.Write(Date);
			if (cache) WriteToCache(to);
		}
	}
}