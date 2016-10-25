// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLUpdateShortSentMessage : TLUpdatesBase, ITLMultiPts 
	{
		[Flags]
		public enum Flag : Int32
		{
			Out = (1 << 1),
			Media = (1 << 9),
			Entities = (1 << 7),
		}

		public bool IsOut { get { return Flags.HasFlag(Flag.Out); } set { Flags = value ? (Flags | Flag.Out) : (Flags & ~Flag.Out); } }
		public bool HasMedia { get { return Flags.HasFlag(Flag.Media); } set { Flags = value ? (Flags | Flag.Media) : (Flags & ~Flag.Media); } }
		public bool HasEntities { get { return Flags.HasFlag(Flag.Entities); } set { Flags = value ? (Flags | Flag.Entities) : (Flags & ~Flag.Entities); } }

		public Flag Flags { get; set; }
		public Int32 Id { get; set; }
		public Int32 Pts { get; set; }
		public Int32 PtsCount { get; set; }
		public Int32 Date { get; set; }
		public TLMessageMediaBase Media { get; set; }
		public TLVector<TLMessageEntityBase> Entities { get; set; }

		public TLUpdateShortSentMessage() { }
		public TLUpdateShortSentMessage(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.UpdateShortSentMessage; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Flags = (Flag)from.ReadInt32();
			Id = from.ReadInt32();
			Pts = from.ReadInt32();
			PtsCount = from.ReadInt32();
			Date = from.ReadInt32();
			if (HasMedia) Media = TLFactory.Read<TLMessageMediaBase>(from, cache);
			if (HasEntities) Entities = TLFactory.Read<TLVector<TLMessageEntityBase>>(from, cache);
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			UpdateFlags();

			to.Write(0x11F1331C);
			to.Write((Int32)Flags);
			to.Write(Id);
			to.Write(Pts);
			to.Write(PtsCount);
			to.Write(Date);
			if (HasMedia) to.WriteObject(Media, cache);
			if (HasEntities) to.WriteObject(Entities, cache);
			if (cache) WriteToCache(to);
		}

		private void UpdateFlags()
		{
			HasMedia = Media != null;
			HasEntities = Entities != null;
		}
	}
}