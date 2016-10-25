// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLSendMessageUploadPhotoAction : TLSendMessageActionBase 
	{
		public Int32 Progress { get; set; }

		public TLSendMessageUploadPhotoAction() { }
		public TLSendMessageUploadPhotoAction(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.SendMessageUploadPhotoAction; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Progress = from.ReadInt32();
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			to.Write(0xD1D34A26);
			to.Write(Progress);
			if (cache) WriteToCache(to);
		}
	}
}