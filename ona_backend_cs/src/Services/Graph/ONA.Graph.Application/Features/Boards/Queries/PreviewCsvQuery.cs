using ONA.Graph.Application.Abstractions.Messaging;
using ONA.Graph.Application.Ports;

namespace ONA.Graph.Application.Features.Boards.Queries;

/// <summary>READ: columns, first rows and a suggested column mapping of an uploaded CSV. Nothing is saved.</summary>
public sealed record PreviewCsvQuery(Stream Csv) : IQuery<CsvPreview>;

public sealed class PreviewCsvQueryHandler(IEmployeeDirectoryPort employeeDirectory)
    : IQueryHandler<PreviewCsvQuery, CsvPreview>
{
    public Task<CsvPreview> HandleAsync(PreviewCsvQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query.Csv);
        return employeeDirectory.PreviewAsync(query.Csv, cancellationToken);
    }
}
