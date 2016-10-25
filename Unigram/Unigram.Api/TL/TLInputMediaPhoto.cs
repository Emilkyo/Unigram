// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLInputMediaPhoto : TLInputMediaBase, ITLMediaCaption 
	{
		public TLInputPhotoBase Id { get; set; }
		public String Caption { get; set; }

		public TLInputMediaPhoto() { }
		public TLInputMediaPhoto(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.InputMediaPhoto; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Id = TLFactory.Read<TLInputPhotoBase>(from, cache);
			Caption = from.ReadString();
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			to.Write(0xE9BFB4F3);
			to.WriteObject(Id, cache);
			to.Write(Caption);
			if (cache) WriteToCache(to);
		}
	}
}