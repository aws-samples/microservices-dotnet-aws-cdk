// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace SampleWebApp.Entities;
public class Book
{
    public Guid? Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; }

    public int ISBN { get; set; }

    [JsonPropertyName("Authors")]
    public ReadOnlyCollection<string> BookAuthors { get; set; }
    public string CoverPage { get; set; }

    public short Year { get; set; }
}

