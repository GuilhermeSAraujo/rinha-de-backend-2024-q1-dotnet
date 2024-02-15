using Npgsql;
using NpgsqlTypes;

namespace rinha_de_backend_2024_q1_dotnet;

public class InsertTransactionPool
{
    private readonly Queue<NpgsqlCommand> _pool;
    private const int POOL_SIZE = 3000;

    public InsertTransactionPool()
    {
        _pool = FillPool();
    }

    public Queue<NpgsqlCommand> FillPool()
    {
        var pool = new Queue<NpgsqlCommand>();
        for (int i = 0; i < POOL_SIZE; i++)
        {
            var cmd = Create();
            pool.Enqueue(cmd);
        }
        return pool;
    }

    public NpgsqlCommand Create()
    {
        /*
         * var cmd = new NpgsqlCommand("INSERT INTO transaction(value, type, description, customer_id) VALUES($1, $2, $3, $4); ");
         * UPDATE customer SET balance = balance + $5 WHERE id = $6 RETURNING \"limit\", balance
         **/
        //var cmd = new NpgsqlCommand("INSERT INTO transaction(value, type, description, customer_id) VALUES($1, $2, $3, $4)");
        var cmd = new NpgsqlCommand("select * from criar_transacao($1, $2, $3, $4)");
        cmd.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });
        cmd.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });
        cmd.Parameters.Add(new NpgsqlParameter<char>() { NpgsqlDbType = NpgsqlDbType.Char });
        cmd.Parameters.Add(new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlDbType.Varchar });

        return cmd;
    }

    public NpgsqlCommand GetCommand()
    {
        Console.WriteLine($"The InsertTransactionPool queue has: {_pool.Count}");

        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }

        return Create();
    }

    public void ReturnCommand(NpgsqlCommand command)
    {
        // Reset the command before returning it to the pool
        command.Parameters[0] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
        command.Parameters[1] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
        command.Parameters[2] = new NpgsqlParameter<char>() { NpgsqlDbType = NpgsqlDbType.Char };
        command.Parameters[3] = new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlDbType.Varchar };

        _pool.Enqueue(command);
    }
}
