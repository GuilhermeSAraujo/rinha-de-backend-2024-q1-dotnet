﻿using Npgsql;
using NpgsqlTypes;

namespace rinha_de_backend_2024_q1_dotnet;

public class BalanceSatementPool
{
    private readonly Queue<Dictionary<string, NpgsqlCommand>> _pool;
    private const int POOL_SIZE = 300;

    public BalanceSatementPool()
    {
        _pool = FillPool();
    }

    public Queue<Dictionary<string, NpgsqlCommand>> FillPool()
    {
        var pool = new Queue<Dictionary<string, NpgsqlCommand>>();
        for (int i = 0; i < POOL_SIZE; i++)
        {
            var cmd = Create();
            pool.Enqueue(cmd);
        }
        return pool;
    }

    public Dictionary<string, NpgsqlCommand> Create()
    {
        var selectCustomer = new NpgsqlCommand($"SELECT limite, saldo FROM cliente WHERE id = $1");
        selectCustomer.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });

        var selectTransaction = new NpgsqlCommand($"SELECT valor, tipo as tipo, descricao, hora_criacao FROM transacao WHERE id_cliente = $1 ORDER BY id DESC LIMIT 10");
        selectTransaction.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });

        return new Dictionary<string, NpgsqlCommand>
        {
            { "selectCustomer", selectCustomer },
            {"selectTransaction", selectTransaction }
        };
    }

    public Dictionary<string, NpgsqlCommand> GetCommand()
    {
        Console.WriteLine($"The BalanceSatementPool queue has: {_pool.Count}");

        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }

        return Create();
    }

    public void ReturnCommand(Dictionary<string, NpgsqlCommand> dict)
    {
        dict["selectCustomer"].Parameters[0] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
        dict["selectTransaction"].Parameters[0] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };

        _pool.Enqueue(dict);
    }
}
