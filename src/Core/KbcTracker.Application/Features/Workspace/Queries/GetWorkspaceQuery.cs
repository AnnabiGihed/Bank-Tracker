using KbcTracker.Application.Features.Workspace.Contracts;
using Pivot.Framework.Application.Abstractions.Messaging.Queries;

namespace KbcTracker.Application.Features.Workspace.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Requests the central computed display model used by both UI clients.
/// </summary>
public sealed record GetWorkspaceQuery : IQuery<WorkspaceSnapshot>;
