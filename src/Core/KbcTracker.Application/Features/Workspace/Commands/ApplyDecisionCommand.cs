using KbcTracker.Application.Features.Workspace.Contracts;
using Pivot.Framework.Application.Abstractions.Messaging.Commands;

namespace KbcTracker.Application.Features.Workspace.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Commits one UI decision against the central history.
/// </summary>
/// <param name="Decision">The action and its required fields.</param>
public sealed record ApplyDecisionCommand(DecisionRequest Decision) : ICommand<DecisionResponse>;
