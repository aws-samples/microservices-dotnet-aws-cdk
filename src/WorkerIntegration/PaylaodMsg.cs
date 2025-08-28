// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
namespace WorkerIntegration;

public record PaylaodMsg
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
}