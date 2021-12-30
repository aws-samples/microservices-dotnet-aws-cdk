

using System;
using System.Collections.Generic;

public class PaylaodMsg
{
    public string Type { get; set; }
    public string MessageId { get; set; }
    public string TopicArn { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public string SignatureVersion { get; set; }
    public string Signature { get; set; }
    public string SigningCertURL { get; set; }
    public string UnsubscribeURL { get; set; }
    public Dictionary<string, PaylaodMsg.MessageAttributeValue> MessageAttributes { get; set; }

    public class MessageAttributeValue
    {
        public string Type { get; set; }
        public string Value { get; set; }
    }
}