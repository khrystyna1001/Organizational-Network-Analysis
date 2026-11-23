using ONA.Analysis.Application.Ports;

namespace ONA.Analysis.Infrastructure.Acl;

/// <summary>Facade implementing the port: adapter (I/O) + translator (mapping).</summary>
internal sealed class GraphSnapshotAcl(GraphHttpAdapter adapter, GraphSnapshotTranslator translator) : IGraphSnapshotPort
{
    public async Task<GraphSnapshot> GetSnapshotAsync(string boardId, CancellationToken cancellationToken = default)
    {
        var dto = await adapter.ReadSnapshotAsync(boardId, cancellationToken);
        return translator.ToSnapshot(dto, boardId);
    }
}
