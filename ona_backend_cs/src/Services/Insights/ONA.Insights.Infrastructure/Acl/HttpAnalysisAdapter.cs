using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ONA.Insights.Application.Ports;
using ONA.Insights.Infrastructure.Acl.ExternalContracts;

namespace ONA.Insights.Infrastructure.Acl;

/// <summary>I/O only: GET /api/v1/analysis-runs/{id} on the Analysis service.</summary>
internal sealed class HttpAnalysisAdapter(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public async Task<ANALYSIS_RUN_DTO?> ReadRunAsync(string analysisRunId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"api/v1/analysis-runs/{Uri.EscapeDataString(analysisRunId)}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new AnalysisIntegrationException(
                    $"Analysis service answered {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<ANALYSIS_RUN_DTO>(JsonOptions, cancellationToken)
                ?? throw new AnalysisIntegrationException("Analysis service returned an empty body.");
        }
        catch (HttpRequestException exception)
        {
            throw new AnalysisIntegrationException("Analysis service is unreachable.", exception);
        }
        catch (JsonException exception)
        {
            throw new AnalysisIntegrationException("Analysis service returned an unreadable body.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AnalysisIntegrationException("Analysis service timed out.", exception);
        }
    }
}
