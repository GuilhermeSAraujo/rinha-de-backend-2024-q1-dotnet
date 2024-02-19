namespace rinha_de_backend_2024_q1_dotnet.Pools;

public class PoolStartupService(
    CreditTransactionPool creditTransactionPool,
    DebitTransactionPool debitTransactionPool,
    BalanceSatementPool balanceSatementPool) : IHostedService
{
    private readonly CreditTransactionPool _creditTransactionPool = creditTransactionPool;
    private readonly DebitTransactionPool _debitTransactionPool = debitTransactionPool;
    private readonly BalanceSatementPool _balanceSatementPool = balanceSatementPool;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // This hosted service is used to initialize the constructors
        // inside each pool and fill them up

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}


