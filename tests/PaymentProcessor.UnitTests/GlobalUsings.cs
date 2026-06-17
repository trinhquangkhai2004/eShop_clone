global using System;
global using System.Threading;
global using System.Threading.Tasks;
global using eShop.EventBus.Abstractions;
global using eShop.EventBus.Events;
global using eShop.PaymentProcessor;
global using eShop.PaymentProcessor.Chaos;
global using eShop.PaymentProcessor.Domain;
global using eShop.PaymentProcessor.IntegrationEvents.EventHandling;
global using eShop.PaymentProcessor.IntegrationEvents.Events;
global using eShop.PaymentProcessor.Services;
global using eShop.PaymentProcessor.Telemetry;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Microsoft.Extensions.Hosting;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using NSubstitute;

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
