﻿//  * **************************************************************************
//  * Copyright (c) McCreary, Veselka, Bragg & Allen, P.C.
//  * This source code is subject to terms and conditions of the MIT License.
//  * A copy of the license can be found in the License.txt file
//  * at the root of this distribution. 
//  * By using this source code in any fashion, you are agreeing to be bound by 
//  * the terms of the MIT License.
//  * You must not remove this notice from this software.
//  * **************************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Web;

using MvbaCore.Extensions;
using MvbaCore.ThirdParty.Json;

namespace MvbaCore.ThirdParty
{
	public interface IHipChatNotificationService
	{
		Notification<HipChatResult> Notify(HipChatMessage hipChatMessage);
		Notification<HipChatResult> PrivateMessage(PrivateHipChatMessage hipChatMessage);
	}

	public class HipChatNotificationService : IHipChatNotificationService
	{
		private const string ApiMessageRoomUrl = "http://api.hipchat.com/v1/rooms/message?auth_token={0}&format=json";
		private const string ApiMessageUserUrl = "http://api.hipchat.com/v2/user/{0}/message?auth_token={1}&format=json";

		public HipChatNotificationService(IWebServiceClient webServiceClient)
		{
			_webServiceClient = webServiceClient;
		}

		private readonly IWebServiceClient _webServiceClient;

		public Notification<HipChatResult> Notify(HipChatMessage hipChatMessage)
		{
			try
			{
				var notification  = new Notification<HipChatResult>();
				foreach (var partialMessage in hipChatMessage.Message.InSetsOf(5000))
				{
					var message = HttpUtility.UrlEncode(new string(partialMessage.ToArray()));
					var content = "room_id=" + HttpUtility.UrlEncode(hipChatMessage.RoomId)
						+ "&from=" + HttpUtility.UrlEncode(hipChatMessage.From)
						+ "&message=" + message
						+ "&color=" + hipChatMessage.Color.OrDefault().Key
						+ "&message_format=" + hipChatMessage.MessageFormat.OrDefault().Key
						+ "&notify=" + (hipChatMessage.Notify ? 1 : 0);
					var result = _webServiceClient.Post(string.Format(ApiMessageRoomUrl, hipChatMessage.ApiKey), content, "application/x-www-form-urlencoded");
					if (!result.IsValid)
					{
						notification.Add(result);
						break;
					}
					var r = JsonUtility.Deserialize<HipChatResult>(result);
					notification.Item = r;
				}
				return notification;
			}
			catch (WebException exception)
			{
				// from http://stackoverflow.com/a/1140193/102536
				if (exception.Response != null)
				{
					if (exception.Response.ContentLength != 0)
					{
						using (var stream = exception.Response.GetResponseStream())
						{
							// ReSharper disable once AssignNullToNotNullAttribute
							using (var reader = new StreamReader(stream))
							{
								var message = reader.ReadToEnd();
								return new Notification<HipChatResult>(Notification.ErrorFor(message));
							}
						}
					}
				}
				return new Notification<HipChatResult>(Notification.ErrorFor(exception.ToString()));
			}
			catch (Exception exception)
			{
				return new Notification<HipChatResult>(Notification.ErrorFor(exception.ToString()));
			}
		}

		public Notification<HipChatResult> PrivateMessage(PrivateHipChatMessage hipChatMessage)
		{
			try
			{
				var notification  = new Notification<HipChatResult>();
				foreach (var partialMessage in hipChatMessage.Message.InSetsOf(5000))
				{
					var message = EscapeJsonSpecials(new string(partialMessage.ToArray()));
					var content = string.Format(@"{{
""message"": {0},
""message_format"": ""{1}"",
""notify"": ""{2}""
}}", message, hipChatMessage.MessageFormat.OrDefault().Key, hipChatMessage.Notify.ToString().ToLower());
					var result = _webServiceClient.Post(string.Format(ApiMessageUserUrl, hipChatMessage.To, hipChatMessage.ApiKey), content, "application/json");
					if (!result.IsValid)
					{
						notification.Add(result);
						break;
					}
					notification.Item = JsonUtility.Deserialize<HipChatResult>(result);
				}
				return notification;
			}
			catch (WebException exception)
			{
				// from http://stackoverflow.com/a/1140193/102536
				if (exception.Response != null)
				{
					if (exception.Response.ContentLength != 0)
					{
						using (var stream = exception.Response.GetResponseStream())
						{
							// ReSharper disable once AssignNullToNotNullAttribute
							using (var reader = new StreamReader(stream))
							{
								var message = reader.ReadToEnd();
								return new Notification<HipChatResult>(Notification.ErrorFor(message));
							}
						}
					}
				}
				return new Notification<HipChatResult>(Notification.ErrorFor(exception.ToString()));
			}
			catch (Exception exception)
			{
				return new Notification<HipChatResult>(Notification.ErrorFor(exception.ToString()));
			}
		}

		private static string EscapeJsonSpecials(string input)
		{
			var stream = new MemoryStream();
			var serializer = new DataContractJsonSerializer(typeof(string));
			serializer.WriteObject(stream, input);
			stream.Position = 0;
			var sr = new StreamReader(stream);
			return sr.ReadToEnd();
		}
	}

	public class HipChatResult
	{
		public string status;
	}

	public class HipChatTextColor : NamedConstant<HipChatTextColor>
	{
		private HipChatTextColor(string key)
		{
			Add(key, this);
		}

		[DefaultKey]
		public static readonly HipChatTextColor Gray = new HipChatTextColor("gray");
		public static readonly HipChatTextColor Green = new HipChatTextColor("green");
		public static readonly HipChatTextColor Purple = new HipChatTextColor("purple");
		public static readonly HipChatTextColor Red = new HipChatTextColor("red");
		public static readonly HipChatTextColor Yellow = new HipChatTextColor("yellow");
	}

	public class HipChatMessageFormat : NamedConstant<HipChatMessageFormat>
	{
		private HipChatMessageFormat(string key)
		{
			Add(key, this);
		}

		[DefaultKey]
		public static readonly HipChatMessageFormat Html = new HipChatMessageFormat("html");
		public static readonly HipChatMessageFormat Text = new HipChatMessageFormat("text");
	}

	public class HipChatMessage
	{
		public HipChatTextColor Color { get; set; }
		public string From { get; set; }
		public string Message { get; set; }
		public HipChatMessageFormat MessageFormat { get; set; }
		public string RoomId { get; set; }
		public string ApiKey { get; set; }
		public bool Notify { get; set; }
	}

	public class PrivateHipChatMessage
	{
		public string From { get; set; }
		public string Message { get; set; }
		public HipChatMessageFormat MessageFormat { get; set; }
		public string ApiKey { get; set; }
		public bool Notify { get; set; }
		public string To { get; set; }
	}
}