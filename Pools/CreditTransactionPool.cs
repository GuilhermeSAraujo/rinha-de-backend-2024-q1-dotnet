using Npgsql;
using NpgsqlTypes;
using System.Collections.Concurrent;

namespace rinha_de_backend_2024_q1_dotnet.Pools;

public class CreditTransactionPool
{
    private readonly ConcurrentQueue<NpgsqlCommand> _pool;
    private const int POOL_SIZE = 4000;

    public ConcurrentQueue<NpgsqlCommand> FillPool()
    {
        Console.WriteLine("Starting to fill CreditTransactionPool pool");

        var pool = new ConcurrentQueue<NpgsqlCommand>();
        for (int i = 0; i < POOL_SIZE; i++)
        {
            var cmd = Create();
            pool.Enqueue(cmd);
        }

        Console.WriteLine("Pool filled");
        return pool;
    }

    public NpgsqlCommand Create()
    {
        var cmd = new NpgsqlCommand("select * from criar_transacao_credito($1, $2, $3)");
        cmd.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });
        cmd.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });
        cmd.Parameters.Add(new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlDbType.Varchar });

        return cmd;
    }

    public NpgsqlCommand GetCommand()
    {
        //Console.WriteLine($"The InsertTransactionPool queue has: {_pool.Count}");

        if (!_pool.IsEmpty)
        {

            if (_pool.TryDequeue(out var result))
                return result;
            return Create();
        }

        return Create();
    }

    public void ReturnCommand(NpgsqlCommand command)
    {
        // Reset the command before returning it to the pool
        command.Parameters[0] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
        command.Parameters[1] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
        command.Parameters[2] = new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlDbType.Varchar };

        _pool.Enqueue(command);
    }
}
