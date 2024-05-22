// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
using System.Text.Json.Serialization;

public class Book
{
    public Guid Id { get; set; }
    public string Title { get; set; }

    public string ISBN { get; set; }

    [JsonPropertyName("Authors")]
    public List<string> BookAuthors { get; set; }
    public string CoverPage { get; set; }
}