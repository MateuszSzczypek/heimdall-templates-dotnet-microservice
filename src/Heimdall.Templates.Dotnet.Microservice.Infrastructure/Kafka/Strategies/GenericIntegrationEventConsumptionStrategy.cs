﻿namespace Heimdall.Templates.Dotnet.Microservice.Infrastructure.Kafka.Strategies;

using AutoMapper;
using BeHeroes.CodeOps.Abstractions.Aggregates;
using BeHeroes.CodeOps.Abstractions.Commands;
using BeHeroes.CodeOps.Abstractions.Events;
using BeHeroes.CodeOps.Abstractions.Strategies;
using BeHeroes.CodeOps.Infrastructure.Kafka.Strategies;
using Heimdall.Templates.DotNet.Microservice.Application;
using Confluent.Kafka;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Represents a strategy for consuming generic integration events from Kafka.
/// </summary>
public sealed class GenericIntegrationEventConsumptionStrategy : ConsumptionStrategy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenericIntegrationEventConsumptionStrategy"/> class.
    /// </summary>
    /// <param name="mapper">The mapper used for object mapping.</param>
    /// <param name="applicationFacade">The application facade for executing commands.</param>
    public GenericIntegrationEventConsumptionStrategy(IMapper mapper, IApplicationFacade applicationFacade) : base(mapper, applicationFacade)
    {
    }

    /// <summary>
    /// Applies the consumption strategy to the specified Kafka message.
    /// </summary>
    /// <param name="target">The Kafka message to apply the strategy to.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task Apply(ConsumeResult<string, string> target, CancellationToken ct = default)
    {
        var payload = target.Message.Value;

        if (!string.IsNullOrEmpty(payload))
        {
            var @event = JsonSerializer.Deserialize<IntegrationEvent>(payload);
            var aggregateRoot = _mapper.Map<IAggregateRoot>(@event);
            var command = _mapper.Map<IAggregateRoot, ICommand<IAggregateRoot>>(aggregateRoot);

            if (command != null)
            {
                await _applicationFacade.Execute(command, ct);
            }
        }

        return;
    }
}