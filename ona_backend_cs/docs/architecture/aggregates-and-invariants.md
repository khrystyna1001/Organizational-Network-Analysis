# Value Objects and strongly typed identifiers

Implemented in Graph: `BoardName`, `NodeName`, `DepartmentName` (`Domain/ValueObjects`); the other rows below (`ConnectionWeight`, `ImportedRowCount`) are still design. Other contexts already have their own typed ids and value objects: Identity (`UserId`, `Email`, `DisplayName`, `UserRole`, `PasswordPolicy`), Analysis (`AnalysisRunId`, `BoardId`, `GraphVersionId`, `Metrics`), Insights (`RecommendationId`, `AnalysisRunId`, `BoardId`, `GraphVersionId`, `TargetNodeId`). Graph's `BoardId`, `NodeId` and `ConnectionId` are still GUID strings.

| Value Object | Component attributes and types | Hard self-validation at creation | Example immutable domain operation |
| --- | --- | --- | --- |
| `BoardName` | `Value: string` | Reject `null`, `""`, or any string consisting only of whitespace. Other strings, including those with leading or trailing whitespace, are accepted unchanged. | `RenameTo(string value)` returns `new BoardName(value)`; it rejects the same inputs as construction. |
| `NodeName` | `Value: string` | Reject `null`, `""`, or any string consisting only of whitespace. Other strings are accepted unchanged. | `RenameTo(string value)` returns `new NodeName(value)`; it rejects the same inputs as construction. |
| `DepartmentName` | `Value: string` | Reject `null`, `""`, or any string consisting only of whitespace. Other strings are accepted unchanged. | `RenameTo(string value)` returns `new DepartmentName(value)`; it rejects the same inputs as construction. |
| `ConnectionWeight` | `Value: double` | Reject values that are zero or negative, `NaN`, positive infinity, or negative infinity. A missing weight is represented by absence (`ConnectionWeight?`), not by an invalid value; the create API currently defaults an omitted weight to `1.0`. | `ScaleBy(double factor)` returns a new weight with `Value * factor`. Reject a factor that is zero/negative/non-finite, or a result that is zero/negative/non-finite (including floating-point underflow or overflow). |
| `ImportedRowCount` | `Value: int` | Reject any value less than zero. Zero is valid for an import with no data rows. | `Add(int additionalRows)` returns a new count. Reject a negative increment or a sum greater than `Int32.MaxValue`; adding zero returns an equal new value. |

## Strongly typed identifiers

| Identifier | Entity / bounded context | Planned responsibility and lifecycle |
| --- | --- | --- |
| `BoardId` | Graph `Board` aggregate root | Identifies a network board throughout its lifetime. Already represented by a GUID-formatted string in the current implementation; introduce a domain wrapper. |
| `NodeId` | Graph `Node` entity | Identifies an organization/person node within its board. Already represented by a GUID-formatted string; introduce a domain wrapper. |
| `ConnectionId` | Graph `Connection` entity | Identifies a directed relationship within its board. Already represented by a GUID-formatted string; introduce a domain wrapper. |
| `UserId` | Identity `User` entity | Identifies the local account created by `Register User`. Keep it distinct from any external OAuth/provider subject. Registration creates the user; account lifecycle and provider-linking rules belong to Identity. |
| `OrganizationImportId` | Graph-service `OrganizationImport` entity (planned) | Identifies one submitted hierarchy or communication-data import batch. Track receipt, validation outcome, and successful processing/rejection; preserve row counts and a reference to the resulting accepted data, while applying the agreed privacy policy to communication content. The import workflow belongs to Graph; no separate Organization service is planned. |
| `GraphVersionId` | Graph `GraphVersion` entity | Identifies an immutable snapshot produced when a network graph is built or updated. A version belongs to a board, records its source import/version and creation metadata, and remains available after later approved changes. |
| `AnalysisRunId` | Analysis `AnalysisRun` entity | Identifies one execution of graph metrics and bottleneck detection. Bind every run to exactly one `GraphVersionId`; record completion/failure and the resulting metrics/findings so results can be reproduced and traced. |
| `RecommendationId` | Insights `Recommendation` entity | Identifies a recommendation generated from analysis. Record its `AnalysisRunId` (and therefore the source graph version), delivery/response outcome, and any user decision. Only an explicitly approved response may initiate a Graph update. |

### Planned workflow and ownership

The current Graph CSV endpoint (`POST /api/v1/data` → `ImportBoardFromHrisCommand` → `IEmployeeDirectoryPort` ACL) is a limited first step: it validates the employee schema, creates and persists a Draft Board with employee nodes and supervisor connections, and returns the parsed rows plus generated IDs/counts. It does not persist the original import batch as an `OrganizationImport`, process communication logs, or create a versioned `GraphVersion`.

1. **Identity:** `Register User` creates a `User` with a `UserId`; external identity-provider details are linked through Identity and do not replace the domain identifier.
2. **Graph import:** each hierarchy or communication-log submission creates an `OrganizationImport` with an `OrganizationImportId` within the Graph service. Validate and normalize the batch, record its outcome, and enforce the privacy policy before accepted data is used downstream.
3. **Graph:** build a board from accepted import data and save an immutable `GraphVersion` with a `GraphVersionId`. An approved graph update creates a new version rather than overwriting the prior snapshot.
4. **Analysis:** create an `AnalysisRun` with an `AnalysisRunId` for a specific graph version. Calculate deterministic metrics and bottleneck findings against that snapshot and retain the run's provenance.
5. **Insights:** create a `Recommendation` with a `RecommendationId` from an analysis run, deliver it to the user, and record the response. A rejection does not mutate the graph; only approval can trigger a new Graph version.
