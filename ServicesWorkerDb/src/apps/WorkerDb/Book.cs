using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DataModel;
using System.Text.Json.Serialization;


/// <summary>
/// A class representing book information to be added to the Amazon DynamoDB
/// ProductCatalog table.
/// </summary>
[DynamoDBTable("BooksCatalog")]
public class Book
{
    [DynamoDBHashKey] // Partition key
    public Guid Id { get; set; }

    [DynamoDBProperty]
    public string Title { get; set; }

    [DynamoDBProperty]
    public string Isbn { get; set; }

    [JsonPropertyName("Authors")]
    [DynamoDBProperty("Authors")] // String Set datatype
    public List<string> BookAuthors { get; set; }
}
