using System.Text.Json.Serialization;

public class Book
{
    public Guid Id { get; set; }
    public string Title { get; set; }

    public int ISBN { get; set; }

    [JsonPropertyName("Authors")]
    public List<string> BookAuthors { get; set; }
    public string CoverPage { get; set; }
}