using FlashCard.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Atrium.Services
{
    internal class CircuitHandler : Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, IConnectionStateProvider
    {
        private static readonly ConcurrentDictionary<string, ConnectionMetadata> _activeCircuits = new(); 
        
        public event Action<bool, ConnectionMetadata>? OnConnectionDown;
        public event Action<bool, ConnectionMetadata>? OnConnectionUp;
        public bool IsConnected { get; private set; }
        public int ClientCount { get => _activeCircuits.Count; }

        public CircuitHandler()
        {
            Console.WriteLine("Circuit started.");
        }

        public int GlobalClientCount => _activeCircuits.Count;

        public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken ct)
        {
            var data = new ConnectionMetadata(circuit.Id, DateTime.UtcNow);

            // Add or update the circuit in the static dictionary
            _activeCircuits.TryAdd(circuit.Id, data);

            IsConnected = true;
            OnConnectionUp?.Invoke(true, data);
            await base.OnConnectionUpAsync(circuit, ct);
        }

        public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken ct)
        {
            var data = new ConnectionMetadata(circuit.Id, DateTime.UtcNow, "Circuit Disconnected");

            // Remove the circuit from the static dictionary
            _activeCircuits.TryRemove(circuit.Id, out _);

            IsConnected = false;
            OnConnectionDown?.Invoke(false, data);
            await base.OnConnectionDownAsync(circuit, ct);
        }

    }
}
