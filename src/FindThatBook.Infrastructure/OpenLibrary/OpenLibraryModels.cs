using System.Text.Json.Serialization;

namespace FindThatBook.Infrastructure.OpenLibrary;

/// <summary>Open Library /search.json response envelope.</summary>
public record SearchResponse(
    [property: JsonPropertyName("numFound")] int NumFound,
    [property: JsonPropertyName("docs")] List<SearchDoc> Docs
);

public record SearchDoc(
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("author_name")] List<string>? AuthorName,
    [property: JsonPropertyName("author_key")] List<string>? AuthorKey,
    [property: JsonPropertyName("first_publish_year")] int? FirstPublishYear,
    [property: JsonPropertyName("cover_i")] long? CoverId,
    [property: JsonPropertyName("edition_count")] int? EditionCount,
    [property: JsonPropertyName("subject")] List<string>? Subject
);

/// <summary>Open Library /works/{id}.json response.</summary>
public record WorkResponse(
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] object? Description,
    [property: JsonPropertyName("authors")] List<WorkAuthorEntry>? Authors,
    [property: JsonPropertyName("covers")] List<long>? Covers,
    [property: JsonPropertyName("first_publish_date")] string? FirstPublishDate,
    [property: JsonPropertyName("subjects")] List<string>? Subjects
);

public record WorkAuthorEntry(
    [property: JsonPropertyName("author")] AuthorRef? Author,
    [property: JsonPropertyName("role")] string? Role
);

public record AuthorRef(
    [property: JsonPropertyName("key")] string? Key
);

/// <summary>Open Library /authors/{id}.json response.</summary>
public record AuthorResponse(
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("personal_name")] string? PersonalName
);
