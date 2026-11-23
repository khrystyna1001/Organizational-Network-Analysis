using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ONA.Analysis.Application.Ports;
using ONA.Analysis.Domain.Exceptions;
using ONA.Analysis.Infrastructure.Acl.ExternalContracts;

namespace ONA.Analysis.Infrastructure.Acl;

/// <summary>I/O only: calls the Graph snapshot endpoint and maps HTTP failures to our errors.</summary>
internal sealed class GraphHttpAdapter(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<GRAPH_SNAPSHOT_DTO> ReadSnapshotAsync(string boardId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"api/v1/boards/{Uri.EscapeDataString(boardId)}/snapshot", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<GRAPH_SNAPSHOT_DTO>(Json, cancellationToken)
                    ?? throw new GraphIntegrationException("The Graph snapshot response was empty.");
            }

            var code = await ReadProblemCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound && code == "board.not_found")
            {
                throw new BoardNotFoundException(boardId);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity && code == "board.graph_not_published")
            {
                throw new GraphNotPublishedException(boardId);
            }

            throw new GraphIntegrationException($"Graph service answered {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            throw new GraphIntegrationException("The Graph service could not be reached.", exception);
        }
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<GRAPH_PROBLEM_DTO>(Json, ct);
            return problem?.Code;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
