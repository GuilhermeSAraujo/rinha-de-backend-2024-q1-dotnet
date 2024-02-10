using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using rinha_de_backend_2024_q1_dotnet.Types;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddNpgsqlDataSource(
    //"Host=localhost;Username=admin;Password=123;Database=rinha;Pooling=true;Minimum Pool Size=50;Maximum Pool Size=2000;Multiplexing=true;Timeout=15;Command Timeout=15;Cancellation Timeout=-1;No Reset On Close=true;Max Auto Prepare=20;Auto Prepare Min Usages=1",
    "Host=db;Username=admin;Password=123;Database=rinha;Pooling=true;Minimum Pool Size=50;Maximum Pool Size=2000;Multiplexing=true;Timeout=15;Command Timeout=15;Cancellation Timeout=-1;No Reset On Close=true;Max Auto Prepare=20;Auto Prepare Min Usages=1",
    dataSourceBuilderAction: a => { a.UseLoggerFactory(NullLoggerFactory.Instance); });

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapPost("/clientes/{id}/transacoes", async Task<Results<Ok<PostTransactionResult>, NotFound, UnprocessableEntity>> ([FromRoute] int id, [FromBody] TransactionRequest request, NpgsqlConnection conn) =>
{
    if (id is (< 1 or > 5))
        return TypedResults.NotFound();

    if (string.IsNullOrEmpty(request.Descricao) || request.Descricao.Length > 10)
        return TypedResults.UnprocessableEntity();

    if (request.Tipo != 'c' && request.Tipo != 'd')
        return TypedResults.UnprocessableEntity();

    if (request.Valor <= 0)
        return TypedResults.UnprocessableEntity();

    bool connected = false;
    while (!connected)
    {
        try
        {
            await conn.OpenAsync();
            connected = true;
        }
        catch (NpgsqlException)
        {
            Console.WriteLine("Trying to reconnect.");
            await Task.Delay(1_000);
        }
    }

    var result = new TransactionResult(0, 0);

    if (request.Tipo == 'c')
    {
        var cmd = new NpgsqlCommand($"INSERT INTO transaction (value, type, description, customer_id) VALUES ({request.Valor}, '{request.Tipo}', '{request.Descricao}', {id}); UPDATE customer SET balance = balance + {request.Valor} WHERE id = {id} RETURNING \"limit\", balance", conn);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result = new TransactionResult(reader.GetInt32(0), reader.GetInt32(1));
        }
        await reader.CloseAsync();
        await reader.DisposeAsync();
        return TypedResults.Ok(new PostTransactionResult(result.Limit, result.Balance));
    }
    else
    {
        var cmd = new NpgsqlCommand($"UPDATE customer SET balance = balance - {request.Valor} WHERE id = {id} AND balance >= {request.Valor} RETURNING \"limit\", balance", conn);
        var reader = await cmd.ExecuteReaderAsync();
        if (reader.HasRows)
        {
            while (await reader.ReadAsync())
            {
                result = new TransactionResult(reader.GetInt32(0), reader.GetInt32(1));
            }

            await reader.CloseAsync();
            await reader.DisposeAsync();

            // The balance was successfully updated, so we can insert the transaction.
            cmd = new NpgsqlCommand($"INSERT INTO transaction (value, type, description, customer_id) VALUES ({request.Valor}, '{request.Tipo}', '{request.Descricao}', {id})", conn);
            await cmd.ExecuteNonQueryAsync();

            return TypedResults.Ok(new PostTransactionResult(result.Limit, result.Balance));
        }
        else
        {
            await reader.CloseAsync();
            await reader.DisposeAsync();
            return TypedResults.UnprocessableEntity();
        }
    }
});


app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id, NpgsqlConnection conn) =>
{
    if (id is (< 1 or > 5))
        return Results.NotFound(new { error = "Id not found" });

    bool connected = false;
    while (!connected)
    {
        try
        {
            await conn.OpenAsync();
            connected = true;
        }
        catch (NpgsqlException)
        {
            await Task.Delay(1_000);
        }
    }

    try
    {
        var cmd = new NpgsqlCommand($"SELECT \"limit\" as limite, balance as saldo FROM customer WHERE id = {id}", conn);
        BalanceStatementResult? saldo = null;

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                saldo = new BalanceStatementResult(reader.GetInt32(0), reader.GetInt32(1), DateTime.Now);
            }
            await reader.CloseAsync();
            await reader.DisposeAsync();
        }
        cmd = new NpgsqlCommand($"SELECT value as valor, \"type\" as tipo, description as descricao, created_at as realizada_em FROM transaction WHERE customer_id = {id} ORDER BY id LIMIT 10", conn);

        var latestTransactions = new List<TransactionStatementResult>();

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var transaction = new TransactionStatementResult(reader.GetInt32(0), reader.GetChar(1), reader.GetString(2), reader.GetDateTime(3));
                latestTransactions.Add(transaction);
            }
            await reader.CloseAsync();
            await reader.DisposeAsync();
        }

        await conn.DisposeAsync();
        return TypedResults.Ok(new
        {
            saldo,
            ultimas_transacoes = latestTransactions
        });
    }
    catch (PostgresException e)
    {
        if (e.SqlState == "23503")
        {
            return TypedResults.NotFound();
        }
    }

    await conn.DisposeAsync();
    return TypedResults.Problem();
});


app.Run();
