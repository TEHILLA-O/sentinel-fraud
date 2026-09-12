using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sentinel.Contracts;
using Sentinel.Contracts.Events;
using Sentinel.Contracts.Json;
using Sentinel.RiskEngine.Domain.Models;
using Sentinel.RiskEngine.Domain.Rules;
using Sentinel.SharedKernel;

namespace Sentinel.RiskEngine.Application;

public sealed class TransactionProcessingService
{
    private readonly ITransactionValidator _validator;
    private readonly IInboxStore _inbox;
    private readonly IVelocityStore _velocity;
    private readonly ICustomerProfileStore _profiles;
    private readonly IRuleConfigurationProvider _rulesets;
    private readonly IRiskEngine _engine;
    private readonly IRiskDecisionRepository _decisions;
    private readonly IOutboxStore _outbox;
    private readonly IClock _clock;
    private readonly ILogger<TransactionProcessingService> _logger;

    public TransactionProcessingService(
        ITransactionValidator validator,
        IInboxStore inbox,
        IVelocityStore velocity,
        ICustomerProfileStore profiles,
        IRuleConfigurationProvider rulesets,
        IRiskEngine engine,
        IRiskDecisionRepository decisions,
        IOutboxStore outbox,
        IClock clock,
        ILogger<TransactionProcessingService> logger)
    {
        _validator = validator;
        _inbox = inbox;
        _velocity = velocity;
        _profiles = profiles;
        _rulesets = rulesets;
        _engine = engine;
        _decisions = decisions;
        _outbox = outbox;
        _clock = clock;
        _logger = logger;
    }

    public async Task ProcessAsync(TransactionReceivedV1 message, CancellationToken cancellationToken)
    {
        using var activity = SentinelTelemetry.ActivitySource.StartActivity("sentinel.transaction.process");
        activity?.SetTag("transaction.id", message.TransactionId);
        activity?.SetTag("correlation.id", message.CorrelationId);

        var validation = _validator.Validate(message);
        if (validation.IsFailure)
        {
            throw new PoisonMessageException(validation.Error!.Message);
        }

        var transaction = validation.Value;
        var claimed = await _inbox.TryBeginAsync(message.EventId, ConsumerGroups.TransactionProcessor, cancellationToken)
            .ConfigureAwait(false);
        if (!claimed)
        {
            _logger.LogInformation(
                "Skipping duplicate event {EventId} for transaction {TransactionId}",
                message.EventId,
                transaction.TransactionId);
            return;
        }

        try
        {
            if (await _decisions.ExistsForTransactionAsync(transaction.TransactionId, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation(
                    "Decision already exists for {TransactionId}; treating as idempotent replay",
                    transaction.TransactionId);
                await _inbox.CompleteAsync(message.EventId, cancellationToken).ConfigureAwait(false);
                return;
            }

            var ruleset = await _rulesets.GetActiveAsync(cancellationToken).ConfigureAwait(false);
            var profile = await _profiles.GetAsync(transaction.AccountId, transaction.CustomerId, transaction.Amount.Currency, cancellationToken)
                .ConfigureAwait(false);
            var devices = await _profiles.GetDevicesAsync(transaction.CustomerId, cancellationToken).ConfigureAwait(false);
            var recent = await _profiles.GetRecentTransactionsAsync(transaction.AccountId, 20, cancellationToken)
                .ConfigureAwait(false);
            var velocity = await _velocity.RecordAndSnapshotAsync(transaction, failed: false, cancellationToken)
                .ConfigureAwait(false);

            var context = new RiskContext
            {
                Transaction = transaction,
                Profile = profile,
                Velocity = velocity,
                KnownDevices = devices,
                RecentTransactions = recent,
                RuleParameters = ruleset.Parameters,
                RulesetVersion = ruleset.Version,
                EvaluatedAt = _clock.UtcNow
            };

            var evaluation = await _engine.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            var persisted = new PersistedDecision
            {
                Id = Guid.CreateVersion7(),
                Transaction = transaction,
                Evaluation = evaluation,
                CorrelationId = transaction.CorrelationId,
                CreatedAt = _clock.UtcNow,
                ProfileSnapshotJson = JsonSerializer.Serialize(profile, SentinelJson.Options)
            };

            await _decisions.SaveAsync(persisted, cancellationToken).ConfigureAwait(false);

            var validated = new TransactionValidatedV1
            {
                EventId = Guid.CreateVersion7(),
                CorrelationId = transaction.CorrelationId,
                OccurredAt = _clock.UtcNow,
                Transaction = message
            };
            await _outbox.EnqueueAsync(
                    KafkaTopics.TransactionsValidated,
                    transaction.AccountId,
                    SentinelJson.Serialize(validated),
                    EventTypes.TransactionValidated,
                    cancellationToken)
                .ConfigureAwait(false);

            var decisionEvent = MapDecision(message, transaction, evaluation);
            await _outbox.EnqueueAsync(
                    KafkaTopics.RiskDecisions,
                    transaction.AccountId,
                    SentinelJson.Serialize(decisionEvent),
                    EventTypes.RiskDecision,
                    cancellationToken)
                .ConfigureAwait(false);

            if (evaluation.Decision is RiskDecision.Review or RiskDecision.Block)
            {
                var alert = new AlertRaisedV1
                {
                    EventId = Guid.CreateVersion7(),
                    CorrelationId = transaction.CorrelationId,
                    OccurredAt = _clock.UtcNow,
                    TransactionId = transaction.TransactionId,
                    AccountId = transaction.AccountId,
                    CustomerId = transaction.CustomerId,
                    Decision = evaluation.Decision,
                    RiskScore = evaluation.RiskScore,
                    Summary = string.Join("; ", evaluation.Reasons)
                };
                await _outbox.EnqueueAsync(
                        KafkaTopics.Alerts,
                        transaction.AccountId,
                        SentinelJson.Serialize(alert),
                        EventTypes.AlertRaised,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await _inbox.CompleteAsync(message.EventId, cancellationToken).ConfigureAwait(false);

            SentinelTelemetry.TransactionsProcessed.Add(1);
            if (evaluation.Decision == RiskDecision.Block)
            {
                SentinelTelemetry.TransactionsBlocked.Add(1);
            }
            else if (evaluation.Decision == RiskDecision.Review)
            {
                SentinelTelemetry.TransactionsReview.Add(1);
            }

            _logger.LogInformation(
                "Scored {TransactionId} as {Decision} score={Score} ruleset={Ruleset}",
                transaction.TransactionId,
                evaluation.Decision,
                evaluation.RiskScore,
                evaluation.RulesetVersion);
        }
        catch (PoisonMessageException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentinelTelemetry.ProcessingErrors.Add(1);
            throw;
        }
    }

    private static RiskDecisionV1 MapDecision(
        TransactionReceivedV1 original,
        TransactionSnapshot transaction,
        Domain.Rules.RiskEvaluation evaluation) =>
        new()
        {
            EventId = Guid.CreateVersion7(),
            CorrelationId = transaction.CorrelationId,
            OccurredAt = evaluation.EvaluatedAt,
            TransactionId = transaction.TransactionId,
            AccountId = transaction.AccountId,
            CustomerId = transaction.CustomerId,
            RiskScore = evaluation.RiskScore,
            RiskLevel = evaluation.RiskLevel,
            Decision = evaluation.Decision,
            TriggeredRules = evaluation.TriggeredRules.Select(r => new TriggeredRuleV1
            {
                RuleCode = r.RuleCode,
                Score = r.Score,
                Reason = r.Reason,
                Evidence = r.Evidence
            }).ToArray(),
            Reasons = evaluation.Reasons,
            Evidence = evaluation.Evidence,
            ProcessingTimeMs = evaluation.ProcessingTimeMs,
            ModelVersion = evaluation.ModelVersion,
            RulesetVersion = evaluation.RulesetVersion,
            Transaction = original
        };
}
